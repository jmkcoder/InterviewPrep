using System.Diagnostics;

namespace Interview1.Worker.QueueService.Middleware.Examples
{
    /// <summary>
    /// Middleware that adds a unique request ID to each message processing operation.
    /// Similar to ASP.NET Core's correlation ID middleware.
    /// </summary>
    public class RequestIdMiddleware : ITaskMiddleware
    {
        private readonly ILogger<RequestIdMiddleware> _logger;

        public RequestIdMiddleware(ILogger<RequestIdMiddleware> logger)
        {
            _logger = logger;
        }

        public async Task InvokeAsync(TaskContext context, TaskDelegate next)
        {
            // RequestId is already generated in TaskContext constructor
            // But we can override it with a correlation ID from the message if present
            if (!string.IsNullOrEmpty(context.Message.CorrelationId))
            {
                context.RequestId = context.Message.CorrelationId;
            }

            // Store in Items for access by other middleware/filters
            context.Items["RequestId"] = context.RequestId;

            _logger.LogDebug(
                "Request ID assigned: {RequestId} for MessageId: {MessageId}",
                context.RequestId,
                context.Message.MessageId);

            await next(context);
        }
    }

    /// <summary>
    /// Middleware that measures execution time.
    /// Similar to ASP.NET Core's response time middleware.
    /// </summary>
    public class TimingMiddleware : ITaskMiddleware
    {
        private readonly ILogger<TimingMiddleware> _logger;

        public TimingMiddleware(ILogger<TimingMiddleware> logger)
        {
            _logger = logger;
        }

        public async Task InvokeAsync(TaskContext context, TaskDelegate next)
        {
            var stopwatch = Stopwatch.StartNew();
            context.Items["Stopwatch"] = stopwatch;

            try
            {
                await next(context);
            }
            finally
            {
                stopwatch.Stop();
                
                var level = stopwatch.ElapsedMilliseconds switch
                {
                    < 100 => LogLevel.Debug,
                    < 1000 => LogLevel.Information,
                    < 5000 => LogLevel.Warning,
                    _ => LogLevel.Error
                };

                _logger.Log(
                    level,
                    "[{RequestId}] Message processing took {ElapsedMs}ms",
                    context.RequestId,
                    stopwatch.ElapsedMilliseconds);

                // Store timing for potential use by other middleware
                context.Items["ElapsedMilliseconds"] = stopwatch.ElapsedMilliseconds;
            }
        }
    }

    /// <summary>
    /// Middleware that adds distributed tracing support with parent context propagation.
    /// Extracts trace context from Service Bus message properties to maintain correlation
    /// across service boundaries.
    /// </summary>
    public class DistributedTracingMiddleware : ITaskMiddleware
    {
        private readonly ILogger<DistributedTracingMiddleware> _logger;
        private static readonly ActivitySource ActivitySource = new("Interview1.Worker.QueueService");

        public DistributedTracingMiddleware(ILogger<DistributedTracingMiddleware> logger)
        {
            _logger = logger;
        }

        public async Task InvokeAsync(TaskContext context, TaskDelegate next)
        {
            // Extract trace context from message properties
            string? traceParent = null;
            string? traceState = null;

            if (context.Message.ApplicationProperties.TryGetValue("traceparent", out var traceParentObj))
            {
                traceParent = traceParentObj?.ToString();
            }
            else if (context.Message.ApplicationProperties.TryGetValue("Diagnostic-Id", out var diagnosticIdObj))
            {
                traceParent = diagnosticIdObj?.ToString();
            }

            if (context.Message.ApplicationProperties.TryGetValue("tracestate", out var traceStateObj))
            {
                traceState = traceStateObj?.ToString();
            }

            Activity? activity = null;

            // If trace parent exists, create activity linked to parent
            if (!string.IsNullOrEmpty(traceParent) && ActivityContext.TryParse(traceParent, traceState, out var parentContext))
            {
                activity = ActivitySource.StartActivity(
                    $"ProcessMessage: {context.Message.Subject}",
                    ActivityKind.Consumer,
                    parentContext);

                if (activity != null)
                {
                    _logger.LogDebug(
                        "Created activity {ActivityId} linked to parent {ParentId}",
                        activity.Id,
                        traceParent);
                }
            }
            else
            {
                // No parent context, create new root activity
                activity = ActivitySource.StartActivity(
                    $"ProcessMessage: {context.Message.Subject}",
                    ActivityKind.Consumer);

                if (activity != null)
                {
                    _logger.LogDebug("Created new root activity {ActivityId}", activity.Id);
                }
            }

            if (activity != null)
            {
                // Add semantic tags following OpenTelemetry conventions
                activity.SetTag("messaging.system", "azureservicebus");
                activity.SetTag("messaging.destination", "orders");
                activity.SetTag("messaging.operation", "process");
                activity.SetTag("messaging.message_id", context.Message.MessageId);
                activity.SetTag("messaging.correlation_id", context.Message.CorrelationId);
                activity.SetTag("messaging.delivery_count", context.Message.DeliveryCount);
                activity.SetTag("request.id", context.RequestId);
                activity.SetTag("message.subject", context.Message.Subject);

                context.Items["Activity"] = activity;
            }

            try
            {
                await next(context);

                if (activity != null)
                {
                    activity.SetTag("result.type", context.Result?.GetType().Name);
                    activity.SetStatus(ActivityStatusCode.Ok);
                }
            }
            catch (Exception ex)
            {
                if (activity != null)
                {
                    activity.SetTag("error", true);
                    activity.SetTag("exception.type", ex.GetType().FullName);
                    activity.SetTag("exception.message", ex.Message);
                    activity.SetStatus(ActivityStatusCode.Error, ex.Message);
                }
                throw;
            }
            finally
            {
                activity?.Dispose();
            }
        }
    }
}
