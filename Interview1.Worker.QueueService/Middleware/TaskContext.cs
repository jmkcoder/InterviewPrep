using Azure.Messaging.ServiceBus;
using Interview1.Worker.QueueService.Filters;
using Interview1.Worker.QueueService.Utilities;

namespace Interview1.Worker.QueueService.Middleware
{
    /// <summary>
    /// Context that flows through the middleware pipeline.
    /// Contains message information, services, and shared state.
    /// Similar to HttpContext in ASP.NET Core.
    /// </summary>
    public class TaskContext
    {
        /// <summary>
        /// The Service Bus message event arguments.
        /// </summary>
        public ProcessMessageEventArgs MessageEventArgs { get; }

        /// <summary>
        /// The Service Bus message being processed.
        /// </summary>
        public ServiceBusReceivedMessage Message => MessageEventArgs.Message;

        /// <summary>
        /// The service provider for dependency injection.
        /// </summary>
        public IServiceProvider ServiceProvider { get; }

        /// <summary>
        /// The task that will process this message (may be null in early middleware).
        /// </summary>
        public BaseTask? Task { get; set; }

        /// <summary>
        /// The result of task execution (set by task or middleware).
        /// If set by middleware, it short-circuits the task execution.
        /// </summary>
        public ITaskResult? Result { get; set; }

        /// <summary>
        /// Dictionary for sharing data between middleware components.
        /// Similar to HttpContext.Items.
        /// </summary>
        public IDictionary<string, object?> Items { get; } = new Dictionary<string, object?>();

        /// <summary>
        /// Indicates whether the message processing was canceled or short-circuited.
        /// </summary>
        public bool IsCanceled { get; set; }

        /// <summary>
        /// The exception that occurred during processing, if any.
        /// </summary>
        public Exception? Exception { get; set; }

        /// <summary>
        /// Indicates whether an exception has been handled by middleware.
        /// </summary>
        public bool ExceptionHandled { get; set; }

        /// <summary>
        /// A cancellation token for the operation.
        /// </summary>
        public CancellationToken CancellationToken { get; }

        /// <summary>
        /// Unique identifier for this message processing operation.
        /// </summary>
        public string RequestId { get; set; }

        public TaskContext(
            ProcessMessageEventArgs messageEventArgs,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken = default)
        {
            MessageEventArgs = messageEventArgs;
            ServiceProvider = serviceProvider;
            CancellationToken = cancellationToken;
            RequestId = Guid.NewGuid().ToString();
        }
    }
}
