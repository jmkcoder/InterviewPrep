using Azure.Messaging.ServiceBus;

namespace Interview1.Worker.QueueService.Filters
{
    /// <summary>
    /// Base class for task results.
    /// </summary>
    public abstract class TaskResult : ITaskResult
    {
        public abstract Task ExecuteResultAsync(ProcessMessageEventArgs args);
    }

    /// <summary>
    /// Result that completes the message (removes from queue).
    /// </summary>
    public class CompleteResult : TaskResult
    {
        public override async Task ExecuteResultAsync(ProcessMessageEventArgs args)
        {
            await args.CompleteMessageAsync(args.Message);
        }
    }

    /// <summary>
    /// Result that abandons the message (returns to queue for retry).
    /// </summary>
    public class AbandonResult : TaskResult
    {
        private readonly IDictionary<string, object>? _propertiesToModify;

        public AbandonResult(IDictionary<string, object>? propertiesToModify = null)
        {
            _propertiesToModify = propertiesToModify;
        }

        public override async Task ExecuteResultAsync(ProcessMessageEventArgs args)
        {
            await args.AbandonMessageAsync(args.Message, _propertiesToModify);
        }
    }

    /// <summary>
    /// Result that sends the message to the dead-letter queue.
    /// </summary>
    public class DeadLetterResult : TaskResult
    {
        private readonly string _deadLetterReason;
        private readonly string _deadLetterErrorDescription;

        public DeadLetterResult(string reason, string errorDescription)
        {
            _deadLetterReason = reason;
            _deadLetterErrorDescription = errorDescription;
        }

        public override async Task ExecuteResultAsync(ProcessMessageEventArgs args)
        {
            await args.DeadLetterMessageAsync(args.Message, _deadLetterReason, _deadLetterErrorDescription);
        }
    }

    /// <summary>
    /// Result that defers the message for later processing.
    /// </summary>
    public class DeferResult : TaskResult
    {
        private readonly IDictionary<string, object>? _propertiesToModify;

        public DeferResult(IDictionary<string, object>? propertiesToModify = null)
        {
            _propertiesToModify = propertiesToModify;
        }

        public override async Task ExecuteResultAsync(ProcessMessageEventArgs args)
        {
            await args.DeferMessageAsync(args.Message, _propertiesToModify);
        }
    }
}
