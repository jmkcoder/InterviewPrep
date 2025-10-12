namespace Interview1.Worker.QueueService.Middleware
{
    /// <summary>
    /// Builds and executes a middleware pipeline.
    /// Middleware are executed in the order they are registered.
    /// Similar to ASP.NET Core middleware pipeline.
    /// </summary>
    public class MiddlewarePipeline
    {
        private readonly List<Func<TaskDelegate, TaskDelegate>> _components = new();

        /// <summary>
        /// Adds a middleware to the pipeline.
        /// </summary>
        public MiddlewarePipeline Use(Func<TaskDelegate, TaskDelegate> middleware)
        {
            _components.Add(middleware);
            return this;
        }

        /// <summary>
        /// Adds a middleware instance to the pipeline.
        /// </summary>
        public MiddlewarePipeline Use(ITaskMiddleware middleware)
        {
            return Use(next =>
            {
                return context => middleware.InvokeAsync(context, next);
            });
        }

        /// <summary>
        /// Adds a middleware type to the pipeline (resolved from DI).
        /// </summary>
        public MiddlewarePipeline Use<TMiddleware>() where TMiddleware : ITaskMiddleware
        {
            return Use(next =>
            {
                return async context =>
                {
                    var middleware = context.ServiceProvider.GetRequiredService<TMiddleware>();
                    await middleware.InvokeAsync(context, next);
                };
            });
        }

        /// <summary>
        /// Adds an inline middleware using a delegate.
        /// </summary>
        public MiddlewarePipeline Use(Func<TaskContext, TaskDelegate, Task> middleware)
        {
            return Use(next =>
            {
                return context => middleware(context, next);
            });
        }

        /// <summary>
        /// Builds the pipeline and returns the final TaskDelegate.
        /// </summary>
        public TaskDelegate Build(TaskDelegate finalHandler)
        {
            TaskDelegate pipeline = finalHandler;

            // Build pipeline in reverse order so first middleware wraps everything
            for (int i = _components.Count - 1; i >= 0; i--)
            {
                pipeline = _components[i](pipeline);
            }

            return pipeline;
        }

        /// <summary>
        /// Executes the pipeline with the given context.
        /// </summary>
        public async Task ExecuteAsync(TaskContext context, TaskDelegate finalHandler)
        {
            var pipeline = Build(finalHandler);
            await pipeline(context);
        }
    }

    /// <summary>
    /// Builder for creating middleware pipelines with fluent API.
    /// </summary>
    public class MiddlewarePipelineBuilder
    {
        private readonly MiddlewarePipeline _pipeline = new();
        private readonly IServiceProvider _serviceProvider;

        public MiddlewarePipelineBuilder(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Adds a middleware type to the pipeline.
        /// </summary>
        public MiddlewarePipelineBuilder UseMiddleware<TMiddleware>() where TMiddleware : ITaskMiddleware
        {
            _pipeline.Use<TMiddleware>();
            return this;
        }

        /// <summary>
        /// Adds a middleware instance to the pipeline.
        /// </summary>
        public MiddlewarePipelineBuilder UseMiddleware(ITaskMiddleware middleware)
        {
            _pipeline.Use(middleware);
            return this;
        }

        /// <summary>
        /// Adds an inline middleware.
        /// </summary>
        public MiddlewarePipelineBuilder Use(Func<TaskContext, TaskDelegate, Task> middleware)
        {
            _pipeline.Use(middleware);
            return this;
        }

        /// <summary>
        /// Builds the final pipeline.
        /// </summary>
        public MiddlewarePipeline Build()
        {
            return _pipeline;
        }
    }
}
