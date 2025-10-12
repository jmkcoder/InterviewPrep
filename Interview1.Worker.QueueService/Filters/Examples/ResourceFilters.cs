using System.Diagnostics;

namespace Interview1.Worker.QueueService.Filters.Examples
{
    /// <summary>
    /// Example: Resource filter that measures execution time.
    /// Surrounds the entire execution pipeline.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class MeasureExecutionTimeAttribute : FilterAttribute, IResourceFilter
    {
        private const string StopwatchKey = "ExecutionTimeStopwatch";

        public Task OnResourceExecutingAsync(ResourceExecutingContext context)
        {
            // Start timing before the rest of the pipeline
            var stopwatch = Stopwatch.StartNew();
            context.Items[StopwatchKey] = stopwatch;

            var logger = context.ServiceProvider.GetRequiredService<ILogger<MeasureExecutionTimeAttribute>>();
            logger.LogInformation(
                "Started processing message {MessageId} with subject '{Subject}'",
                context.MessageEventArgs.Message.MessageId,
                context.MessageEventArgs.Message.Subject);

            return Task.CompletedTask;
        }

        public Task OnResourceExecutedAsync(ResourceExecutedContext context)
        {
            // Stop timing after everything completes
            if (context.Items.TryGetValue(StopwatchKey, out var value) && value is Stopwatch stopwatch)
            {
                stopwatch.Stop();
                
                var logger = context.ServiceProvider.GetRequiredService<ILogger<MeasureExecutionTimeAttribute>>();
                
                if (context.Exception != null)
                {
                    logger.LogError(
                        "Message {MessageId} processing failed after {ElapsedMs}ms with exception: {Exception}",
                        context.MessageEventArgs.Message.MessageId,
                        stopwatch.ElapsedMilliseconds,
                        context.Exception.Message);
                }
                else if (context.Canceled)
                {
                    logger.LogWarning(
                        "Message {MessageId} processing was canceled after {ElapsedMs}ms",
                        context.MessageEventArgs.Message.MessageId,
                        stopwatch.ElapsedMilliseconds);
                }
                else
                {
                    logger.LogInformation(
                        "Message {MessageId} processing completed in {ElapsedMs}ms",
                        context.MessageEventArgs.Message.MessageId,
                        stopwatch.ElapsedMilliseconds);
                }
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Example: Resource filter for caching or rate limiting.
    /// Can short-circuit if rate limit is exceeded.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class RateLimitAttribute : FilterAttribute, IResourceFilter
    {
        private static readonly Dictionary<string, (DateTime lastCall, int count)> _rateLimitCache = new();
        private static readonly object _lock = new();
        
        private readonly int _maxCallsPerMinute;

        public RateLimitAttribute(int maxCallsPerMinute = 10)
        {
            _maxCallsPerMinute = maxCallsPerMinute;
        }

        public Task OnResourceExecutingAsync(ResourceExecutingContext context)
        {
            var taskName = context.Task.GetType().Name;
            var now = DateTime.UtcNow;

            lock (_lock)
            {
                if (_rateLimitCache.TryGetValue(taskName, out var entry))
                {
                    if (now - entry.lastCall < TimeSpan.FromMinutes(1))
                    {
                        if (entry.count >= _maxCallsPerMinute)
                        {
                            var logger = context.ServiceProvider.GetRequiredService<ILogger<RateLimitAttribute>>();
                            logger.LogWarning(
                                "Rate limit exceeded for task {TaskName}. Deferring message {MessageId}",
                                taskName,
                                context.MessageEventArgs.Message.MessageId);

                            // Short-circuit: Defer the message for later processing
                            context.Result = new DeferResult();
                            return Task.CompletedTask;
                        }
                        
                        _rateLimitCache[taskName] = (entry.lastCall, entry.count + 1);
                    }
                    else
                    {
                        // Reset counter after a minute
                        _rateLimitCache[taskName] = (now, 1);
                    }
                }
                else
                {
                    _rateLimitCache[taskName] = (now, 1);
                }
            }

            return Task.CompletedTask;
        }

        public Task OnResourceExecutedAsync(ResourceExecutedContext context)
        {
            // Cleanup or additional logic after execution
            return Task.CompletedTask;
        }
    }
}
