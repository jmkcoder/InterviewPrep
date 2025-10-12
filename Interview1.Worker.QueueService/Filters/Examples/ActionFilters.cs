using System.Text.Json;

namespace Interview1.Worker.QueueService.Filters.Examples
{
    /// <summary>
    /// Example: Action filter that logs before and after task execution.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class LogActionAttribute : FilterAttribute, IActionFilter
    {
        public Task OnActionExecutingAsync(ActionExecutingContext context)
        {
            var logger = context.ServiceProvider.GetRequiredService<ILogger<LogActionAttribute>>();
            var message = context.MessageEventArgs.Message;
            
            logger.LogInformation(
                "Executing task {TaskName} for message {MessageId}. Body: {Body}",
                context.Task.GetType().Name,
                message.MessageId,
                message.Body.ToString());

            return Task.CompletedTask;
        }

        public Task OnActionExecutedAsync(ActionExecutedContext context)
        {
            var logger = context.ServiceProvider.GetRequiredService<ILogger<LogActionAttribute>>();
            
            if (context.Exception != null)
            {
                logger.LogError(
                    "Task {TaskName} threw exception: {Exception}",
                    context.Task.GetType().Name,
                    context.Exception.Message);
            }
            else
            {
                logger.LogInformation(
                    "Task {TaskName} completed successfully with result: {ResultType}",
                    context.Task.GetType().Name,
                    context.Result?.GetType().Name ?? "null");
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Example: Action filter that validates message body is valid JSON.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class ValidateJsonBodyAttribute : FilterAttribute, IActionFilter
    {
        public Task OnActionExecutingAsync(ActionExecutingContext context)
        {
            var body = context.MessageEventArgs.Message.Body.ToString();
            
            try
            {
                // Try to parse as JSON
                JsonDocument.Parse(body);
            }
            catch (JsonException ex)
            {
                var logger = context.ServiceProvider.GetRequiredService<ILogger<ValidateJsonBodyAttribute>>();
                logger.LogWarning(
                    "Message {MessageId} has invalid JSON body: {Error}",
                    context.MessageEventArgs.Message.MessageId,
                    ex.Message);

                // Short-circuit: Send to dead-letter queue
                context.Result = new DeadLetterResult(
                    "InvalidMessageFormat",
                    $"Message body is not valid JSON: {ex.Message}");
            }

            return Task.CompletedTask;
        }

        public Task OnActionExecutedAsync(ActionExecutedContext context)
        {
            // No-op for this filter
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Example: Action filter that adds telemetry or custom properties.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class AddTelemetryAttribute : FilterAttribute, IActionFilter
    {
        public Task OnActionExecutingAsync(ActionExecutingContext context)
        {
            // Store start time in Items dictionary to share between executing and executed
            context.Items["ActionStartTime"] = DateTime.UtcNow;
            return Task.CompletedTask;
        }

        public Task OnActionExecutedAsync(ActionExecutedContext context)
        {
            if (context.Items.TryGetValue("ActionStartTime", out var startTimeObj) && 
                startTimeObj is DateTime startTime)
            {
                var duration = DateTime.UtcNow - startTime;
                var logger = context.ServiceProvider.GetRequiredService<ILogger<AddTelemetryAttribute>>();
                
                logger.LogInformation(
                    "Task execution duration: {DurationMs}ms",
                    duration.TotalMilliseconds);
            }

            return Task.CompletedTask;
        }
    }
}
