using Azure.Messaging.ServiceBus;
using Interview1.Worker.QueueService.Filters;

namespace Interview1.Worker.QueueService.Utilities
{
    public abstract class BaseTask
    {
        /// <summary>
        /// Executes the task logic and returns a result indicating how the message should be handled.
        /// </summary>
        /// <param name="eventArgs">The Service Bus message event arguments.</param>
        /// <returns>An ITaskResult that determines the message disposition (Complete, Abandon, DeadLetter, Defer).</returns>
        public abstract Task<ITaskResult> ExecuteAsync(ProcessMessageEventArgs eventArgs);

        /// <summary>
        /// Gets all filters applied to this task via attributes.
        /// </summary>
        internal IEnumerable<object> GetFilters()
        {
            var type = GetType();
            var attributes = type.GetCustomAttributes(true);
            
            // Return all attributes that implement any filter interface
            return attributes.Where(attr =>
                attr is IAuthorizationFilter ||
                attr is IResourceFilter ||
                attr is IActionFilter ||
                attr is IResultFilter ||
                attr is IExceptionFilter);
        }
    }
}