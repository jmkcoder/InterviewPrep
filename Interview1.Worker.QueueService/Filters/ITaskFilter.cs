namespace Interview1.Worker.QueueService.Filters
{
    /// <summary>
    /// Authorization filters run first and are used to determine whether the task should execute.
    /// Short-circuits the pipeline by setting context.Result.
    /// </summary>
    public interface IAuthorizationFilter
    {
        Task OnAuthorizationAsync(AuthorizationFilterContext context);
    }

    /// <summary>
    /// Resource filters run after authorization and surround the rest of the pipeline.
    /// Useful for caching, performance measurement, or resource management.
    /// Short-circuits by setting context.Result in OnResourceExecuting.
    /// </summary>
    public interface IResourceFilter
    {
        Task OnResourceExecutingAsync(ResourceExecutingContext context);
        Task OnResourceExecutedAsync(ResourceExecutedContext context);
    }

    /// <summary>
    /// Action filters run before and after task execution.
    /// Can modify task execution or handle task results.
    /// Short-circuits by setting context.Result in OnActionExecuting.
    /// </summary>
    public interface IActionFilter
    {
        Task OnActionExecutingAsync(ActionExecutingContext context);
        Task OnActionExecutedAsync(ActionExecutedContext context);
    }

    /// <summary>
    /// Result filters run before and after the result execution.
    /// Can modify the result or prevent result execution.
    /// Short-circuits by setting context.Cancel = true in OnResultExecuting.
    /// </summary>
    public interface IResultFilter
    {
        Task OnResultExecutingAsync(ResultExecutingContext context);
        Task OnResultExecutedAsync(ResultExecutedContext context);
    }

    /// <summary>
    /// Exception filters handle exceptions that occur during task or result execution.
    /// Run if an unhandled exception occurs anywhere in the pipeline.
    /// Can handle exception by setting context.ExceptionHandled = true and optionally context.Result.
    /// </summary>
    public interface IExceptionFilter
    {
        Task OnExceptionAsync(ExceptionContext context);
    }

    /// <summary>
    /// Base class for filter attributes that can be applied to tasks.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public abstract class FilterAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the order in which filters execute.
        /// Lower values execute first. Default is 0.
        /// </summary>
        public int Order { get; set; } = 0;
    }
}
