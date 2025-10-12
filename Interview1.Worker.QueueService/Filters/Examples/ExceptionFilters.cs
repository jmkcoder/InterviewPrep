namespace Interview1.Worker.QueueService.Filters.Examples
{
    /// <summary>
    /// Example: Exception filter that handles specific exception types.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public class HandleExceptionAttribute : FilterAttribute, IExceptionFilter
    {
        private readonly Type _exceptionType;
        private readonly bool _deadLetter;

        public HandleExceptionAttribute(Type exceptionType, bool deadLetter = false)
        {
            if (!typeof(Exception).IsAssignableFrom(exceptionType))
            {
                throw new ArgumentException("Type must be an Exception type", nameof(exceptionType));
            }
            
            _exceptionType = exceptionType;
            _deadLetter = deadLetter;
        }

        public Task OnExceptionAsync(ExceptionContext context)
        {
            if (_exceptionType.IsInstanceOfType(context.Exception))
            {
                var logger = context.ServiceProvider.GetRequiredService<ILogger<HandleExceptionAttribute>>();
                
                logger.LogError(
                    context.Exception,
                    "Handled exception {ExceptionType} for message {MessageId}",
                    _exceptionType.Name,
                    context.MessageEventArgs.Message.MessageId);

                // Mark as handled
                context.ExceptionHandled = true;

                // Choose appropriate result
                if (_deadLetter)
                {
                    context.Result = new DeadLetterResult(
                        _exceptionType.Name,
                        context.Exception.Message);
                }
                else
                {
                    // Abandon for retry
                    context.Result = new AbandonResult();
                }
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Example: Generic exception filter that logs all exceptions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class LogExceptionAttribute : FilterAttribute, IExceptionFilter
    {
        public Task OnExceptionAsync(ExceptionContext context)
        {
            var logger = context.ServiceProvider.GetRequiredService<ILogger<LogExceptionAttribute>>();
            
            logger.LogError(
                context.Exception,
                "Unhandled exception in task {TaskName} for message {MessageId}",
                context.Task.GetType().Name,
                context.MessageEventArgs.Message.MessageId);

            // Don't handle the exception, just log it
            // Let other exception filters handle it
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Example: Exception filter that catches all exceptions and dead-letters them.
    /// Should have high Order value to run last.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class DeadLetterOnErrorAttribute : FilterAttribute, IExceptionFilter
    {
        public DeadLetterOnErrorAttribute()
        {
            // Run last by default
            Order = int.MaxValue;
        }

        public Task OnExceptionAsync(ExceptionContext context)
        {
            if (!context.ExceptionHandled)
            {
                var logger = context.ServiceProvider.GetRequiredService<ILogger<DeadLetterOnErrorAttribute>>();
                
                logger.LogError(
                    context.Exception,
                    "Dead-lettering message {MessageId} due to unhandled exception",
                    context.MessageEventArgs.Message.MessageId);

                context.ExceptionHandled = true;
                context.Result = new DeadLetterResult(
                    "UnhandledException",
                    context.Exception.ToString());
            }

            return Task.CompletedTask;
        }
    }
}
