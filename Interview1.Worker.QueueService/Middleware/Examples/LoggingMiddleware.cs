namespace Interview1.Worker.QueueService.Middleware.Examples
{
    /// <summary>
    /// Middleware that logs the start and end of message processing.
    /// Similar to ASP.NET Core's logging middleware.
    /// </summary>
    public class LoggingMiddleware : ITaskMiddleware
    {
        private readonly ILogger<LoggingMiddleware> _logger;

        public LoggingMiddleware(ILogger<LoggingMiddleware> logger)
        {
            _logger = logger;
        }

        public async Task InvokeAsync(TaskContext context, TaskDelegate next)
        {
            var message = context.Message;
            
            _logger.LogInformation(
                "[{RequestId}] Starting message processing. MessageId: {MessageId}, Subject: {Subject}, DeliveryCount: {DeliveryCount}",
                context.RequestId,
                message.MessageId,
                message.Subject,
                message.DeliveryCount);

            try
            {
                await next(context);

                _logger.LogInformation(
                    "[{RequestId}] Message processing completed. Result: {ResultType}",
                    context.RequestId,
                    context.Result?.GetType().Name ?? "None");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[{RequestId}] Message processing failed with exception: {ExceptionType}",
                    context.RequestId,
                    ex.GetType().Name);
                throw;
            }
        }
    }

    /// <summary>
    /// Middleware that logs detailed request information.
    /// </summary>
    public class DetailedLoggingMiddleware : ITaskMiddleware
    {
        private readonly ILogger<DetailedLoggingMiddleware> _logger;

        public DetailedLoggingMiddleware(ILogger<DetailedLoggingMiddleware> logger)
        {
            _logger = logger;
        }

        public async Task InvokeAsync(TaskContext context, TaskDelegate next)
        {
            var message = context.Message;
            
            _logger.LogDebug(
                "[{RequestId}] Message Details - Body: {Body}, CorrelationId: {CorrelationId}, ContentType: {ContentType}",
                context.RequestId,
                message.Body.ToString(),
                message.CorrelationId,
                message.ContentType);

            if (message.ApplicationProperties.Any())
            {
                _logger.LogDebug(
                    "[{RequestId}] Application Properties: {Properties}",
                    context.RequestId,
                    string.Join(", ", message.ApplicationProperties.Select(kvp => $"{kvp.Key}={kvp.Value}")));
            }

            await next(context);
        }
    }
}
