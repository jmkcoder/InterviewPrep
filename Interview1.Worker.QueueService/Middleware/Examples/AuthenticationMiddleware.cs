using Interview1.Worker.QueueService.Filters;

namespace Interview1.Worker.QueueService.Middleware.Examples
{
    /// <summary>
    /// Middleware that validates message properties.
    /// Similar to ASP.NET Core's authorization middleware.
    /// </summary>
    public class AuthenticationMiddleware : ITaskMiddleware
    {
        private readonly ILogger<AuthenticationMiddleware> _logger;

        public AuthenticationMiddleware(ILogger<AuthenticationMiddleware> logger)
        {
            _logger = logger;
        }

        public async Task InvokeAsync(TaskContext context, TaskDelegate next)
        {
            var message = context.Message;

            // Check for authentication token in message properties
            if (!message.ApplicationProperties.TryGetValue("Authorization", out var authValue) ||
                string.IsNullOrWhiteSpace(authValue?.ToString()))
            {
                _logger.LogWarning(
                    "[{RequestId}] Authentication failed: Missing Authorization property",
                    context.RequestId);

                context.Result = new DeadLetterResult(
                    "AuthenticationFailed",
                    "Missing Authorization property");
                return; // Short-circuit
            }

            // Store authentication info for use by tasks/filters
            context.Items["Authorization"] = authValue.ToString();

            _logger.LogDebug(
                "[{RequestId}] Authentication successful",
                context.RequestId);

            await next(context);
        }
    }

    /// <summary>
    /// Middleware that validates message has required subject.
    /// </summary>
    public class ValidateSubjectMiddleware : ITaskMiddleware
    {
        private readonly ILogger<ValidateSubjectMiddleware> _logger;

        public ValidateSubjectMiddleware(ILogger<ValidateSubjectMiddleware> logger)
        {
            _logger = logger;
        }

        public async Task InvokeAsync(TaskContext context, TaskDelegate next)
        {
            if (string.IsNullOrWhiteSpace(context.Message.Subject))
            {
                _logger.LogWarning(
                    "[{RequestId}] Message validation failed: Missing subject",
                    context.RequestId);

                context.Result = new DeadLetterResult(
                    "InvalidMessage",
                    "Message subject is required");
                return; // Short-circuit
            }

            await next(context);
        }
    }

    /// <summary>
    /// Middleware that validates message body size.
    /// </summary>
    public class ValidateMessageSizeMiddleware : ITaskMiddleware
    {
        private readonly ILogger<ValidateMessageSizeMiddleware> _logger;
        private readonly long _maxSizeInBytes;

        public ValidateMessageSizeMiddleware(
            ILogger<ValidateMessageSizeMiddleware> logger,
            long maxSizeInBytes = 1024 * 1024) // 1MB default
        {
            _logger = logger;
            _maxSizeInBytes = maxSizeInBytes;
        }

        public async Task InvokeAsync(TaskContext context, TaskDelegate next)
        {
            var bodySize = context.Message.Body.ToArray().Length;

            if (bodySize > _maxSizeInBytes)
            {
                _logger.LogWarning(
                    "[{RequestId}] Message validation failed: Body size {BodySize} exceeds maximum {MaxSize}",
                    context.RequestId,
                    bodySize,
                    _maxSizeInBytes);

                context.Result = new DeadLetterResult(
                    "MessageTooLarge",
                    $"Message body size {bodySize} exceeds maximum {_maxSizeInBytes} bytes");
                return; // Short-circuit
            }

            await next(context);
        }
    }

    /// <summary>
    /// Middleware that checks if message is expired based on TimeToLive.
    /// </summary>
    public class MessageExpirationMiddleware : ITaskMiddleware
    {
        private readonly ILogger<MessageExpirationMiddleware> _logger;

        public MessageExpirationMiddleware(ILogger<MessageExpirationMiddleware> logger)
        {
            _logger = logger;
        }

        public async Task InvokeAsync(TaskContext context, TaskDelegate next)
        {
            var message = context.Message;

            // Check if message has expired based on EnqueuedTime and TimeToLive
            if (message.ExpiresAt < DateTimeOffset.UtcNow)
            {
                _logger.LogWarning(
                    "[{RequestId}] Message expired. EnqueuedTime: {EnqueuedTime}, ExpiresAt: {ExpiresAt}",
                    context.RequestId,
                    message.EnqueuedTime,
                    message.ExpiresAt);

                context.Result = new DeadLetterResult(
                    "MessageExpired",
                    $"Message expired at {message.ExpiresAt}");
                return; // Short-circuit
            }

            await next(context);
        }
    }
}
