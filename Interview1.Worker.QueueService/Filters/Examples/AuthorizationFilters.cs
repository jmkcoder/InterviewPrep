namespace Interview1.Worker.QueueService.Filters.Examples
{
    /// <summary>
    /// Example: Authorization filter that checks if message has required claim.
    /// Runs FIRST in the pipeline.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class RequireClaimAttribute : FilterAttribute, IAuthorizationFilter
    {
        private readonly string _claimName;
        private readonly string _claimValue;

        public RequireClaimAttribute(string claimName, string claimValue)
        {
            _claimName = claimName;
            _claimValue = claimValue;
        }

        public Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var message = context.MessageEventArgs.Message;
            
            // Check if message has the required application property (claim)
            if (!message.ApplicationProperties.TryGetValue(_claimName, out var value) ||
                value?.ToString() != _claimValue)
            {
                var logger = context.ServiceProvider.GetRequiredService<ILogger<RequireClaimAttribute>>();
                logger.LogWarning(
                    "Authorization failed: Message {MessageId} missing required claim '{ClaimName}={ClaimValue}'",
                    message.MessageId, _claimName, _claimValue);

                // Short-circuit: Send to dead-letter queue
                context.Result = new DeadLetterResult(
                    "AuthorizationFailed",
                    $"Missing required claim: {_claimName}={_claimValue}");
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Example: Simple authorization filter that checks message subject.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class ValidateMessageSubjectAttribute : FilterAttribute, IAuthorizationFilter
    {
        public Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var message = context.MessageEventArgs.Message;
            
            if (string.IsNullOrWhiteSpace(message.Subject))
            {
                var logger = context.ServiceProvider.GetRequiredService<ILogger<ValidateMessageSubjectAttribute>>();
                logger.LogWarning("Authorization failed: Message {MessageId} has no subject", message.MessageId);

                context.Result = new DeadLetterResult(
                    "InvalidMessage",
                    "Message subject is required");
            }

            return Task.CompletedTask;
        }
    }
}
