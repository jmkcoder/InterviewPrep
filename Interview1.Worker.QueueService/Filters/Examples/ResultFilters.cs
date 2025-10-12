namespace Interview1.Worker.QueueService.Filters.Examples
{
    /// <summary>
    /// Example: Result filter that logs the result type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class LogResultAttribute : FilterAttribute, IResultFilter
    {
        public Task OnResultExecutingAsync(ResultExecutingContext context)
        {
            var logger = context.ServiceProvider.GetRequiredService<ILogger<LogResultAttribute>>();
            
            logger.LogInformation(
                "Executing result {ResultType} for message {MessageId}",
                context.Result.GetType().Name,
                context.MessageEventArgs.Message.MessageId);

            return Task.CompletedTask;
        }

        public Task OnResultExecutedAsync(ResultExecutedContext context)
        {
            var logger = context.ServiceProvider.GetRequiredService<ILogger<LogResultAttribute>>();
            
            if (context.Exception != null)
            {
                logger.LogError(
                    "Result execution failed: {Exception}",
                    context.Exception.Message);
            }
            else
            {
                logger.LogInformation(
                    "Result {ResultType} executed successfully",
                    context.Result.GetType().Name);
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Example: Result filter that converts AbandonResult to DeadLetterResult after max retries.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class MaxRetryAttribute : FilterAttribute, IResultFilter
    {
        private readonly int _maxRetries;

        public MaxRetryAttribute(int maxRetries = 3)
        {
            _maxRetries = maxRetries;
        }

        public Task OnResultExecutingAsync(ResultExecutingContext context)
        {
            // Check if result is Abandon and delivery count exceeds max retries
            if (context.Result is AbandonResult)
            {
                var deliveryCount = context.MessageEventArgs.Message.DeliveryCount;
                
                if (deliveryCount >= _maxRetries)
                {
                    var logger = context.ServiceProvider.GetRequiredService<ILogger<MaxRetryAttribute>>();
                    logger.LogWarning(
                        "Message {MessageId} exceeded max retries ({MaxRetries}). Moving to dead-letter queue.",
                        context.MessageEventArgs.Message.MessageId,
                        _maxRetries);

                    // Replace result with DeadLetterResult
                    context.Result = new DeadLetterResult(
                        "MaxRetriesExceeded",
                        $"Message was abandoned {deliveryCount} times, exceeding max retries of {_maxRetries}");
                }
            }

            return Task.CompletedTask;
        }

        public Task OnResultExecutedAsync(ResultExecutedContext context)
        {
            return Task.CompletedTask;
        }
    }
}
