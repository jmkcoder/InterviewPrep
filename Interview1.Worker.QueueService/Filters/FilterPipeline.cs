using Azure.Messaging.ServiceBus;
using Interview1.Worker.QueueService.Utilities;

namespace Interview1.Worker.QueueService.Filters
{
    /// <summary>
    /// Manages the execution of filters in the correct order with proper short-circuiting behavior.
    /// Mimics ASP.NET Core MVC filter pipeline.
    /// </summary>
    public class FilterPipeline
    {
        private readonly ProcessMessageEventArgs _messageEventArgs;
        private readonly IServiceProvider _serviceProvider;
        private readonly BaseTask _task;
        private readonly List<IAuthorizationFilter> _authorizationFilters;
        private readonly List<IResourceFilter> _resourceFilters;
        private readonly List<IActionFilter> _actionFilters;
        private readonly List<IResultFilter> _resultFilters;
        private readonly List<IExceptionFilter> _exceptionFilters;

        public FilterPipeline(
            ProcessMessageEventArgs messageEventArgs,
            IServiceProvider serviceProvider,
            BaseTask task,
            IEnumerable<object> filters)
        {
            _messageEventArgs = messageEventArgs;
            _serviceProvider = serviceProvider;
            _task = task;

            // Separate filters by type and order them
            _authorizationFilters = filters.OfType<IAuthorizationFilter>()
                .OrderBy(GetOrder)
                .ToList();

            _resourceFilters = filters.OfType<IResourceFilter>()
                .OrderBy(GetOrder)
                .ToList();

            _actionFilters = filters.OfType<IActionFilter>()
                .OrderBy(GetOrder)
                .ToList();

            _resultFilters = filters.OfType<IResultFilter>()
                .OrderBy(GetOrder)
                .ToList();

            _exceptionFilters = filters.OfType<IExceptionFilter>()
                .OrderBy(GetOrder)
                .ToList();
        }

        private static int GetOrder(object filter)
        {
            return filter is FilterAttribute attr ? attr.Order : 0;
        }

        /// <summary>
        /// Executes the complete filter pipeline with the task.
        /// Returns the final ITaskResult to be executed.
        /// </summary>
        public async Task<ITaskResult> ExecuteAsync()
        {
            try
            {
                // 1. Authorization filters
                var authResult = await ExecuteAuthorizationFiltersAsync();
                if (authResult != null)
                    return authResult; // Short-circuit

                // 2. Resource filters (surrounding)
                return await ExecuteResourceFiltersAsync();
            }
            catch (Exception ex)
            {
                // Handle any unhandled exceptions from the pipeline
                var exceptionHandled = await ExecuteExceptionFiltersAsync(ex);
                if (exceptionHandled.result != null)
                    return exceptionHandled.result;

                // If no exception filter handled it, abandon the message
                return new AbandonResult();
            }
        }

        private async Task<ITaskResult?> ExecuteAuthorizationFiltersAsync()
        {
            var context = new AuthorizationFilterContext(_messageEventArgs, _serviceProvider, _task);

            foreach (var filter in _authorizationFilters)
            {
                await filter.OnAuthorizationAsync(context);
                if (context.Result != null)
                    return context.Result; // Short-circuit
            }

            return null;
        }

        private async Task<ITaskResult> ExecuteResourceFiltersAsync()
        {
            var executingContext = new ResourceExecutingContext(_messageEventArgs, _serviceProvider, _task);
            var executedContext = new ResourceExecutedContext(_messageEventArgs, _serviceProvider, _task);

            // Copy Items dictionary to share state
            executedContext.Items.Clear();
            foreach (var item in executingContext.Items)
                executedContext.Items[item.Key] = item.Value;

            var resourceFilterIndex = 0;

            // Execute OnResourceExecuting for all filters
            for (; resourceFilterIndex < _resourceFilters.Count; resourceFilterIndex++)
            {
                var filter = _resourceFilters[resourceFilterIndex];
                await filter.OnResourceExecutingAsync(executingContext);

                if (executingContext.Result != null)
                {
                    // Short-circuit - skip remaining resource filters' executing phase
                    executedContext.Result = executingContext.Result;
                    executedContext.Canceled = true;
                    break;
                }
            }

            ITaskResult result;

            if (executedContext.Canceled)
            {
                // Short-circuited, use the result from executing context
                result = executedContext.Result!;
            }
            else
            {
                // Continue with action filters and task execution
                try
                {
                    result = await ExecuteActionFiltersAndTaskAsync();
                    executedContext.Result = result;
                }
                catch (Exception ex)
                {
                    executedContext.Exception = ex;
                    executedContext.ExceptionHandled = false;

                    // Try exception filters
                    var exceptionHandled = await ExecuteExceptionFiltersAsync(ex);
                    executedContext.ExceptionHandled = exceptionHandled.handled;
                    if (exceptionHandled.result != null)
                    {
                        executedContext.Result = exceptionHandled.result;
                        result = exceptionHandled.result;
                    }
                    else if (!exceptionHandled.handled)
                    {
                        throw; // Re-throw if not handled
                    }
                    else
                    {
                        result = new AbandonResult();
                    }
                }
            }

            // Execute OnResourceExecuted in reverse order (only for filters that executed OnResourceExecuting)
            for (var i = resourceFilterIndex - 1; i >= 0; i--)
            {
                var filter = _resourceFilters[i];
                await filter.OnResourceExecutedAsync(executedContext);
            }

            return result;
        }

        private async Task<ITaskResult> ExecuteActionFiltersAndTaskAsync()
        {
            var executingContext = new ActionExecutingContext(_messageEventArgs, _serviceProvider, _task);
            var executedContext = new ActionExecutedContext(_messageEventArgs, _serviceProvider, _task);

            // Copy Items
            executedContext.Items.Clear();
            foreach (var item in executingContext.Items)
                executedContext.Items[item.Key] = item.Value;

            var actionFilterIndex = 0;

            // Execute OnActionExecuting for all filters
            for (; actionFilterIndex < _actionFilters.Count; actionFilterIndex++)
            {
                var filter = _actionFilters[actionFilterIndex];
                await filter.OnActionExecutingAsync(executingContext);

                if (executingContext.Result != null)
                {
                    // Short-circuit
                    executedContext.Result = executingContext.Result;
                    executedContext.Canceled = true;
                    break;
                }
            }

            ITaskResult result;

            if (executedContext.Canceled)
            {
                result = executedContext.Result!;
            }
            else
            {
                // Execute the actual task
                try
                {
                    result = await _task.ExecuteAsync(_messageEventArgs);
                    executedContext.Result = result;
                }
                catch (Exception ex)
                {
                    executedContext.Exception = ex;
                    executedContext.ExceptionHandled = false;
                    throw; // Will be caught by resource filter or root handler
                }
            }

            // Execute OnActionExecuted in reverse order
            for (var i = actionFilterIndex - 1; i >= 0; i--)
            {
                var filter = _actionFilters[i];
                await filter.OnActionExecutedAsync(executedContext);
            }

            // Execute result filters
            return await ExecuteResultFiltersAsync(result);
        }

        private async Task<ITaskResult> ExecuteResultFiltersAsync(ITaskResult result)
        {
            var executingContext = new ResultExecutingContext(_messageEventArgs, _serviceProvider, _task, result);
            var executedContext = new ResultExecutedContext(_messageEventArgs, _serviceProvider, _task, result);

            var resultFilterIndex = 0;

            // Execute OnResultExecuting
            for (; resultFilterIndex < _resultFilters.Count; resultFilterIndex++)
            {
                var filter = _resultFilters[resultFilterIndex];
                await filter.OnResultExecutingAsync(executingContext);

                if (executingContext.Cancel)
                {
                    executedContext.Canceled = true;
                    break;
                }
            }

            // Use the potentially modified result
            var finalResult = executingContext.Result;
            executedContext.Result = finalResult;

            // Execute OnResultExecuted in reverse order
            for (var i = resultFilterIndex - 1; i >= 0; i--)
            {
                var filter = _resultFilters[i];
                await filter.OnResultExecutedAsync(executedContext);
            }

            return finalResult;
        }

        private async Task<(bool handled, ITaskResult? result)> ExecuteExceptionFiltersAsync(Exception exception)
        {
            var context = new ExceptionContext(_messageEventArgs, _serviceProvider, _task, exception);

            foreach (var filter in _exceptionFilters)
            {
                await filter.OnExceptionAsync(context);

                if (context.ExceptionHandled)
                    return (true, context.Result);
            }

            return (false, null);
        }
    }
}
