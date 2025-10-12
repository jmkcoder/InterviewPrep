namespace Interview1.Worker.QueueService.Middleware
{
    /// <summary>
    /// Delegate representing the next middleware in the pipeline.
    /// Matches ASP.NET Core's RequestDelegate pattern.
    /// </summary>
    public delegate Task TaskDelegate(TaskContext context);

    /// <summary>
    /// Defines the contract for task middleware.
    /// Middleware can inspect, modify, or short-circuit the message processing pipeline.
    /// Similar to ASP.NET Core IMiddleware.
    /// </summary>
    public interface ITaskMiddleware
    {
        /// <summary>
        /// Invokes the middleware.
        /// Call await next(context) to continue to the next middleware or return early to short-circuit.
        /// </summary>
        /// <param name="context">The task context containing message and service information.</param>
        /// <param name="next">The delegate to invoke the next middleware in the pipeline.</param>
        Task InvokeAsync(TaskContext context, TaskDelegate next);
    }

    /// <summary>
    /// Base class for middleware that provides common functionality.
    /// </summary>
    public abstract class TaskMiddleware : ITaskMiddleware
    {
        public abstract Task InvokeAsync(TaskContext context, TaskDelegate next);
    }
}
