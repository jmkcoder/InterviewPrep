using Interview1.Worker.QueueService.Filters;

namespace Interview1.Worker.QueueService.Middleware.Examples
{
    /// <summary>
    /// Middleware that catches all exceptions and handles them gracefully.
    /// Similar to ASP.NET Core's exception handler middleware.
    /// This should typically be the first middleware in the pipeline.
    /// </summary>
    public class ExceptionHandlingMiddleware : ITaskMiddleware
    {
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger)
        {
            _logger = logger;
        }

        public async Task InvokeAsync(TaskContext context, TaskDelegate next)
        {
            try
            {
                await next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[{RequestId}] Unhandled exception caught by exception middleware: {ExceptionType} - {Message}",
                    context.RequestId,
                    ex.GetType().Name,
                    ex.Message);

                context.Exception = ex;
                context.ExceptionHandled = true;

                // Decide how to handle the exception based on type
                if (ex is InvalidOperationException or ArgumentException or ArgumentNullException)
                {
                    // Poison message - dead letter it
                    context.Result = new DeadLetterResult(
                        ex.GetType().Name,
                        ex.Message);
                }
                else if (ex is TimeoutException or HttpRequestException)
                {
                    // Transient error - abandon for retry
                    context.Result = new AbandonResult();
                }
                else
                {
                    // Unknown error - abandon for retry with max retry check
                    if (context.Message.DeliveryCount >= 5)
                    {
                        context.Result = new DeadLetterResult(
                            "MaxRetriesExceeded",
                            $"Message abandoned {context.Message.DeliveryCount} times: {ex.Message}");
                    }
                    else
                    {
                        context.Result = new AbandonResult();
                    }
                }
            }
        }
    }

    /// <summary>
    /// Middleware that catches specific exception types.
    /// </summary>
    public class TypedExceptionHandlingMiddleware<TException> : ITaskMiddleware where TException : Exception
    {
        private readonly ILogger<TypedExceptionHandlingMiddleware<TException>> _logger;
        private readonly Func<TException, TaskContext, ITaskResult> _handler;

        public TypedExceptionHandlingMiddleware(
            ILogger<TypedExceptionHandlingMiddleware<TException>> logger,
            Func<TException, TaskContext, ITaskResult> handler)
        {
            _logger = logger;
            _handler = handler;
        }

        public async Task InvokeAsync(TaskContext context, TaskDelegate next)
        {
            try
            {
                await next(context);
            }
            catch (TException ex)
            {
                _logger.LogWarning(
                    ex,
                    "[{RequestId}] Caught {ExceptionType}: {Message}",
                    context.RequestId,
                    typeof(TException).Name,
                    ex.Message);

                context.Exception = ex;
                context.ExceptionHandled = true;
                context.Result = _handler(ex, context);
            }
        }
    }
}
