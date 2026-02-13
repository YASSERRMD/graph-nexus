using System.Runtime.ExceptionServices;

namespace GraphNexus.Execution.Resilience;

public sealed class RetryPolicy
{
    private readonly int _maxRetries;
    private readonly TimeSpan _baseDelay;
    private readonly double _exponentialBackoffMultiplier;
    private readonly bool _useExponentialBackoff;

    public RetryPolicy(
        int maxRetries = 3,
        TimeSpan? baseDelay = null,
        double exponentialBackoffMultiplier = 2.0,
        bool useExponentialBackoff = true)
    {
        _maxRetries = maxRetries;
        _baseDelay = baseDelay ?? TimeSpan.FromMilliseconds(200);
        _exponentialBackoffMultiplier = exponentialBackoffMultiplier;
        _useExponentialBackoff = useExponentialBackoff;
    }

    public async Task<T> ExecuteAsync<T>(
        Func<int, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default,
        Action<RetryAttemptContext>? onRetry = null)
    {
        Exception? lastException = null;

        for (int attempt = 0; attempt <= _maxRetries; attempt++)
        {
            try
            {
                return await action(attempt, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (IsTransient(ex) && attempt < _maxRetries)
            {
                lastException = ex;

                var delay = _useExponentialBackoff
                    ? TimeSpan.FromTicks((long)(_baseDelay.Ticks * Math.Pow(_exponentialBackoffMultiplier, attempt)))
                    : _baseDelay;

                var context = new RetryAttemptContext(attempt + 1, _maxRetries, ex, delay);
                onRetry?.Invoke(context);

                try
                {
                    await Task.Delay(delay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw lastException;
                }
            }
        }

        throw lastException!;
    }

    private static bool IsTransient(Exception ex)
    {
        return ex is TimeoutException ||
               ex is HttpRequestException ||
               ex is IOException ||
               ex is TaskCanceledException;
    }
}

public sealed record RetryAttemptContext(
    int AttemptNumber,
    int MaxRetries,
    Exception Exception,
    TimeSpan Delay);

public sealed class CircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _recoveryTimeout;
    private readonly TimeSpan _halfOpenRetryInterval;

    private int _failureCount;
    private CircuitState _state;
    private DateTime _lastFailureTime;
    private readonly object _lock = new();

    public string Name { get; }

    public CircuitBreaker(
        string name,
        int failureThreshold = 5,
        TimeSpan? recoveryTimeout = null,
        TimeSpan? halfOpenRetryInterval = null)
    {
        Name = name;
        _failureThreshold = failureThreshold;
        _recoveryTimeout = recoveryTimeout ?? TimeSpan.FromSeconds(30);
        _halfOpenRetryInterval = halfOpenRetryInterval ?? TimeSpan.FromSeconds(5);
        _state = CircuitState.Closed;
    }

    public CircuitState State
    {
        get
        {
            lock (_lock)
            {
                if (_state == CircuitState.Open && DateTime.UtcNow - _lastFailureTime > _recoveryTimeout)
                {
                    _state = CircuitState.HalfOpen;
                }
                return _state;
            }
        }
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        if (State == CircuitState.Open)
        {
            throw new CircuitBreakerOpenException($"Circuit breaker '{Name}' is open");
        }

        try
        {
            var result = await action(cancellationToken);
            OnSuccess();
            return result;
        }
        catch (Exception ex)
        {
            OnFailure();
            throw;
        }
    }

    private void OnSuccess()
    {
        lock (_lock)
        {
            _failureCount = 0;
            _state = CircuitState.Closed;
        }
    }

    private void OnFailure()
    {
        lock (_lock)
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;

            if (_failureCount >= _failureThreshold)
            {
                _state = CircuitState.Open;
            }
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _failureCount = 0;
            _state = CircuitState.Closed;
        }
    }
}

public enum CircuitState
{
    Closed,
    Open,
    HalfOpen
}

public sealed class CircuitBreakerOpenException : Exception
{
    public CircuitBreakerOpenException(string message) : base(message) { }
}

public sealed class CircuitBreakerRegistry
{
    private readonly ConcurrentDictionary<string, CircuitBreaker> _breakers = new();
    private readonly int _defaultFailureThreshold;
    private readonly TimeSpan _defaultRecoveryTimeout;

    public CircuitBreakerRegistry(int defaultFailureThreshold = 5, TimeSpan? defaultRecoveryTimeout = null)
    {
        _defaultFailureThreshold = defaultFailureThreshold;
        _defaultRecoveryTimeout = defaultRecoveryTimeout ?? TimeSpan.FromSeconds(30);
    }

    public CircuitBreaker GetOrCreate(string name)
    {
        return _breakers.GetOrAdd(name, n => new CircuitBreaker(n, _defaultFailureThreshold, _defaultRecoveryTimeout));
    }

    public CircuitBreaker GetOrCreate(string name, int failureThreshold, TimeSpan recoveryTimeout)
    {
        return _breakers.GetOrAdd(name, n => new CircuitBreaker(n, failureThreshold, recoveryTimeout));
    }

    public void ResetAll()
    {
        foreach (var breaker in _breakers.Values)
        {
            breaker.Reset();
        }
    }
}
