using Azure.Messaging.ServiceBus;
using Interview1.Worker.QueueService.Utilities;

namespace Interview1.Worker.QueueService.Filters
{
    /// <summary>
    /// Base context for all filter operations.
    /// </summary>
    public abstract class FilterContext
    {
        public ProcessMessageEventArgs MessageEventArgs { get; }
        public IServiceProvider ServiceProvider { get; }
        public BaseTask Task { get; }
        public IDictionary<string, object?> Items { get; } = new Dictionary<string, object?>();

        protected FilterContext(
            ProcessMessageEventArgs messageEventArgs,
            IServiceProvider serviceProvider,
            BaseTask task)
        {
            MessageEventArgs = messageEventArgs;
            ServiceProvider = serviceProvider;
            Task = task;
        }
    }

    /// <summary>
    /// Context for authorization filters.
    /// </summary>
    public class AuthorizationFilterContext : FilterContext
    {
        public ITaskResult? Result { get; set; }

        public AuthorizationFilterContext(
            ProcessMessageEventArgs messageEventArgs,
            IServiceProvider serviceProvider,
            BaseTask task)
            : base(messageEventArgs, serviceProvider, task)
        {
        }
    }

    /// <summary>
    /// Context for resource filters (executing phase).
    /// </summary>
    public class ResourceExecutingContext : FilterContext
    {
        public ITaskResult? Result { get; set; }

        public ResourceExecutingContext(
            ProcessMessageEventArgs messageEventArgs,
            IServiceProvider serviceProvider,
            BaseTask task)
            : base(messageEventArgs, serviceProvider, task)
        {
        }
    }

    /// <summary>
    /// Context for resource filters (executed phase).
    /// </summary>
    public class ResourceExecutedContext : FilterContext
    {
        public bool Canceled { get; set; }
        public Exception? Exception { get; set; }
        public bool ExceptionHandled { get; set; }
        public ITaskResult? Result { get; set; }

        public ResourceExecutedContext(
            ProcessMessageEventArgs messageEventArgs,
            IServiceProvider serviceProvider,
            BaseTask task)
            : base(messageEventArgs, serviceProvider, task)
        {
        }
    }

    /// <summary>
    /// Context for action filters (executing phase).
    /// </summary>
    public class ActionExecutingContext : FilterContext
    {
        public ITaskResult? Result { get; set; }

        public ActionExecutingContext(
            ProcessMessageEventArgs messageEventArgs,
            IServiceProvider serviceProvider,
            BaseTask task)
            : base(messageEventArgs, serviceProvider, task)
        {
        }
    }

    /// <summary>
    /// Context for action filters (executed phase).
    /// </summary>
    public class ActionExecutedContext : FilterContext
    {
        public bool Canceled { get; set; }
        public Exception? Exception { get; set; }
        public bool ExceptionHandled { get; set; }
        public ITaskResult? Result { get; set; }

        public ActionExecutedContext(
            ProcessMessageEventArgs messageEventArgs,
            IServiceProvider serviceProvider,
            BaseTask task)
            : base(messageEventArgs, serviceProvider, task)
        {
        }
    }

    /// <summary>
    /// Context for result filters (executing phase).
    /// </summary>
    public class ResultExecutingContext : FilterContext
    {
        public ITaskResult Result { get; set; }
        public bool Cancel { get; set; }

        public ResultExecutingContext(
            ProcessMessageEventArgs messageEventArgs,
            IServiceProvider serviceProvider,
            BaseTask task,
            ITaskResult result)
            : base(messageEventArgs, serviceProvider, task)
        {
            Result = result;
        }
    }

    /// <summary>
    /// Context for result filters (executed phase).
    /// </summary>
    public class ResultExecutedContext : FilterContext
    {
        public ITaskResult Result { get; set; }
        public bool Canceled { get; set; }
        public Exception? Exception { get; set; }
        public bool ExceptionHandled { get; set; }

        public ResultExecutedContext(
            ProcessMessageEventArgs messageEventArgs,
            IServiceProvider serviceProvider,
            BaseTask task,
            ITaskResult result)
            : base(messageEventArgs, serviceProvider, task)
        {
            Result = result;
        }
    }

    /// <summary>
    /// Context for exception filters.
    /// </summary>
    public class ExceptionContext : FilterContext
    {
        public Exception Exception { get; }
        public bool ExceptionHandled { get; set; }
        public ITaskResult? Result { get; set; }

        public ExceptionContext(
            ProcessMessageEventArgs messageEventArgs,
            IServiceProvider serviceProvider,
            BaseTask task,
            Exception exception)
            : base(messageEventArgs, serviceProvider, task)
        {
            Exception = exception;
        }
    }
}
