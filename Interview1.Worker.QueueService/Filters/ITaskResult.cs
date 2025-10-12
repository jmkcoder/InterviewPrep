using Azure.Messaging.ServiceBus;

namespace Interview1.Worker.QueueService.Filters
{
    /// <summary>
    /// Defines the contract for task execution results.
    /// Similar to IActionResult in MVC.
    /// </summary>
    public interface ITaskResult
    {
        /// <summary>
        /// Executes the result operation on the message.
        /// </summary>
        Task ExecuteResultAsync(ProcessMessageEventArgs args);
    }
}
