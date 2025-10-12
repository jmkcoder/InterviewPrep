using Azure.Messaging.ServiceBus;
using Interview1.Worker.QueueService.Filters;
using Interview1.Worker.QueueService.Utilities;

namespace Interview1.Worker.QueueService.Tasks
{
    [Task("Test")]
    public class TestTask : BaseTask
    {
        private readonly ILogger<TestTask> _logger;

        public TestTask(ILogger<TestTask> logger)
        {
            _logger = logger;
        }

        public override Task<ITaskResult> ExecuteAsync(ProcessMessageEventArgs eventArgs)
        {
            _logger.LogInformation("Executing TestTask");
            _logger.LogInformation("Message Body: {Body}", eventArgs.Message.Body.ToString());
            
            // Return CompleteResult to remove the message from the queue
            return Task.FromResult<ITaskResult>(new CompleteResult());
        }
    }
}
