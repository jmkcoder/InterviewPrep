using Interview1.Worker.QueueService.Filters;
using System.Collections.Concurrent;

namespace Interview1.Worker.QueueService.Middleware.Examples
{
    /// <summary>
    /// Middleware that implements rate limiting.
    /// Similar to ASP.NET Core's rate limiting middleware.
    /// </summary>
    public class RateLimitingMiddleware : ITaskMiddleware
    {
        private readonly ILogger<RateLimitingMiddleware> _logger;
        private readonly int _maxRequestsPerMinute;
        private static readonly ConcurrentDictionary<string, (DateTime WindowStart, int Count)> _rateLimitCache = new();

        public RateLimitingMiddleware(
            ILogger<RateLimitingMiddleware> logger,
            int maxRequestsPerMinute = 100)
        {
            _logger = logger;
            _maxRequestsPerMinute = maxRequestsPerMinute;
        }

        public async Task InvokeAsync(TaskContext context, TaskDelegate next)
        {
            var key = context.Message.Subject ?? "default";
            var now = DateTime.UtcNow;

            var isAllowed = _rateLimitCache.AddOrUpdate(
                key,
                _ => (now, 1),
                (_, existing) =>
                {
                    if (now - existing.WindowStart > TimeSpan.FromMinutes(1))
                    {
                        // Reset window
                        return (now, 1);
                    }
                    return (existing.WindowStart, existing.Count + 1);
                });

            if (isAllowed.Count > _maxRequestsPerMinute)
            {
                _logger.LogWarning(
                    "[{RequestId}] Rate limit exceeded for subject '{Subject}'. Count: {Count}/{Max}. Deferring message.",
                    context.RequestId,
                    key,
                    isAllowed.Count,
                    _maxRequestsPerMinute);

                // Defer the message for later processing
                context.Result = new DeferResult();
                return; // Short-circuit
            }

            _logger.LogDebug(
                "[{RequestId}] Rate limit check passed for subject '{Subject}'. Count: {Count}/{Max}",
                context.RequestId,
                key,
                isAllowed.Count,
                _maxRequestsPerMinute);

            await next(context);
        }
    }

    /// <summary>
    /// Middleware that implements a simple circuit breaker pattern.
    /// </summary>
    public class CircuitBreakerMiddleware : ITaskMiddleware
    {
        private readonly ILogger<CircuitBreakerMiddleware> _logger;
        private readonly int _failureThreshold;
        private readonly TimeSpan _breakDuration;
        private int _consecutiveFailures;
        private DateTime? _circuitOpenedAt;
        private readonly object _lock = new();

        public CircuitBreakerMiddleware(
            ILogger<CircuitBreakerMiddleware> logger,
            int failureThreshold = 5,
            TimeSpan? breakDuration = null)
        {
            _logger = logger;
            _failureThreshold = failureThreshold;
            _breakDuration = breakDuration ?? TimeSpan.FromMinutes(1);
        }

        public async Task InvokeAsync(TaskContext context, TaskDelegate next)
        {
            // Check if circuit is open
            lock (_lock)
            {
                if (_circuitOpenedAt.HasValue)
                {
                    if (DateTime.UtcNow - _circuitOpenedAt.Value < _breakDuration)
                    {
                        _logger.LogWarning(
                            "[{RequestId}] Circuit breaker is OPEN. Deferring message.",
                            context.RequestId);

                        context.Result = new DeferResult();
                        return; // Short-circuit
                    }
                    else
                    {
                        // Try to close the circuit (half-open state)
                        _logger.LogInformation(
                            "[{RequestId}] Circuit breaker entering HALF-OPEN state",
                            context.RequestId);
                        _circuitOpenedAt = null;
                    }
                }
            }

            try
            {
                await next(context);

                // Success - reset failure count
                lock (_lock)
                {
                    if (_consecutiveFailures > 0)
                    {
                        _logger.LogInformation(
                            "[{RequestId}] Circuit breaker reset after success",
                            context.RequestId);
                        _consecutiveFailures = 0;
                    }
                }
            }
            catch (Exception)
            {
                lock (_lock)
                {
                    _consecutiveFailures++;

                    if (_consecutiveFailures >= _failureThreshold)
                    {
                        _circuitOpenedAt = DateTime.UtcNow;
                        _logger.LogError(
                            "[{RequestId}] Circuit breaker OPENED after {FailureCount} consecutive failures",
                            context.RequestId,
                            _consecutiveFailures);
                    }
                }
                throw;
            }
        }
    }
}
