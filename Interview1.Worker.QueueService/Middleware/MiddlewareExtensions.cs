using Interview1.Worker.QueueService.Middleware.Examples;

namespace Interview1.Worker.QueueService.Middleware
{
    /// <summary>
    /// Extension methods for configuring middleware in the service collection.
    /// </summary>
    public static class MiddlewareExtensions
    {
        /// <summary>
        /// Adds middleware services to the service collection.
        /// </summary>
        public static IServiceCollection AddTaskMiddleware(this IServiceCollection services)
        {
            // Register common middleware as singletons (they should be stateless or thread-safe)
            //services.AddSingleton<LoggingMiddleware>();
            //services.AddSingleton<DetailedLoggingMiddleware>();
            //services.AddSingleton<ExceptionHandlingMiddleware>();
            //services.AddSingleton<RequestIdMiddleware>();
            //services.AddSingleton<TimingMiddleware>();
            //services.AddSingleton<DistributedTracingMiddleware>();
            //services.AddSingleton<ValidateSubjectMiddleware>();
            //services.AddSingleton<MessageExpirationMiddleware>();

            return services;
        }

        /// <summary>
        /// Adds a rate limiting middleware with the specified configuration.
        /// </summary>
        public static IServiceCollection AddRateLimiting(
            this IServiceCollection services,
            int maxRequestsPerMinute = 100)
        {
            services.AddSingleton<RateLimitingMiddleware>(sp =>
                new RateLimitingMiddleware(
                    sp.GetRequiredService<ILogger<RateLimitingMiddleware>>(),
                    maxRequestsPerMinute));

            return services;
        }

        /// <summary>
        /// Adds a circuit breaker middleware with the specified configuration.
        /// </summary>
        public static IServiceCollection AddCircuitBreaker(
            this IServiceCollection services,
            int failureThreshold = 5,
            TimeSpan? breakDuration = null)
        {
            services.AddSingleton<CircuitBreakerMiddleware>(sp =>
                new CircuitBreakerMiddleware(
                    sp.GetRequiredService<ILogger<CircuitBreakerMiddleware>>(),
                    failureThreshold,
                    breakDuration));

            return services;
        }

        /// <summary>
        /// Adds message size validation middleware.
        /// </summary>
        public static IServiceCollection AddMessageSizeValidation(
            this IServiceCollection services,
            long maxSizeInBytes = 1024 * 1024)
        {
            services.AddSingleton<ValidateMessageSizeMiddleware>(sp =>
                new ValidateMessageSizeMiddleware(
                    sp.GetRequiredService<ILogger<ValidateMessageSizeMiddleware>>(),
                    maxSizeInBytes));

            return services;
        }

        /// <summary>
        /// Adds authentication middleware.
        /// </summary>
        public static IServiceCollection AddAuthentication(this IServiceCollection services)
        {
            services.AddSingleton<AuthenticationMiddleware>();
            return services;
        }
    }

    /// <summary>
    /// Extension methods for the middleware pipeline builder.
    /// </summary>
    public static class MiddlewarePipelineBuilderExtensions
    {
        /// <summary>
        /// Adds exception handling middleware (should be first).
        /// </summary>
        public static MiddlewarePipelineBuilder UseExceptionHandler(this MiddlewarePipelineBuilder builder)
        {
            return builder.UseMiddleware<ExceptionHandlingMiddleware>();
        }

        /// <summary>
        /// Adds request ID middleware.
        /// </summary>
        public static MiddlewarePipelineBuilder UseRequestId(this MiddlewarePipelineBuilder builder)
        {
            return builder.UseMiddleware<RequestIdMiddleware>();
        }

        /// <summary>
        /// Adds logging middleware.
        /// </summary>
        public static MiddlewarePipelineBuilder UseLogging(this MiddlewarePipelineBuilder builder)
        {
            return builder.UseMiddleware<LoggingMiddleware>();
        }

        /// <summary>
        /// Adds detailed logging middleware.
        /// </summary>
        public static MiddlewarePipelineBuilder UseDetailedLogging(this MiddlewarePipelineBuilder builder)
        {
            return builder.UseMiddleware<DetailedLoggingMiddleware>();
        }

        /// <summary>
        /// Adds timing middleware.
        /// </summary>
        public static MiddlewarePipelineBuilder UseTiming(this MiddlewarePipelineBuilder builder)
        {
            return builder.UseMiddleware<TimingMiddleware>();
        }

        /// <summary>
        /// Adds distributed tracing middleware.
        /// </summary>
        public static MiddlewarePipelineBuilder UseDistributedTracing(this MiddlewarePipelineBuilder builder)
        {
            return builder.UseMiddleware<DistributedTracingMiddleware>();
        }

        /// <summary>
        /// Adds rate limiting middleware.
        /// </summary>
        public static MiddlewarePipelineBuilder UseRateLimiting(this MiddlewarePipelineBuilder builder)
        {
            return builder.UseMiddleware<RateLimitingMiddleware>();
        }

        /// <summary>
        /// Adds circuit breaker middleware.
        /// </summary>
        public static MiddlewarePipelineBuilder UseCircuitBreaker(this MiddlewarePipelineBuilder builder)
        {
            return builder.UseMiddleware<CircuitBreakerMiddleware>();
        }

        /// <summary>
        /// Adds authentication middleware.
        /// </summary>
        public static MiddlewarePipelineBuilder UseAuthentication(this MiddlewarePipelineBuilder builder)
        {
            return builder.UseMiddleware<AuthenticationMiddleware>();
        }

        /// <summary>
        /// Adds subject validation middleware.
        /// </summary>
        public static MiddlewarePipelineBuilder UseSubjectValidation(this MiddlewarePipelineBuilder builder)
        {
            return builder.UseMiddleware<ValidateSubjectMiddleware>();
        }

        /// <summary>
        /// Adds message size validation middleware.
        /// </summary>
        public static MiddlewarePipelineBuilder UseMessageSizeValidation(this MiddlewarePipelineBuilder builder)
        {
            return builder.UseMiddleware<ValidateMessageSizeMiddleware>();
        }

        /// <summary>
        /// Adds message expiration validation middleware.
        /// </summary>
        public static MiddlewarePipelineBuilder UseMessageExpiration(this MiddlewarePipelineBuilder builder)
        {
            return builder.UseMiddleware<MessageExpirationMiddleware>();
        }

        /// <summary>
        /// Adds inline middleware using a delegate.
        /// </summary>
        public static MiddlewarePipelineBuilder Use(
            this MiddlewarePipelineBuilder builder,
            Func<TaskContext, TaskDelegate, Task> middleware)
        {
            return builder.Use(middleware);
        }

        /// <summary>
        /// Adds middleware that runs a delegate without calling next (terminal middleware).
        /// </summary>
        public static MiddlewarePipelineBuilder Run(
            this MiddlewarePipelineBuilder builder,
            Func<TaskContext, Task> handler)
        {
            return builder.Use(async (context, next) =>
            {
                await handler(context);
                // Don't call next - this is terminal
            });
        }

        /// <summary>
        /// Conditionally execute middleware based on a predicate.
        /// </summary>
        public static MiddlewarePipelineBuilder UseWhen(
            this MiddlewarePipelineBuilder builder,
            Func<TaskContext, bool> predicate,
            Action<MiddlewarePipelineBuilder> configuration)
        {
            return builder.Use(async (context, next) =>
            {
                if (predicate(context))
                {
                    var branchBuilder = new MiddlewarePipelineBuilder(context.ServiceProvider);
                    configuration(branchBuilder);
                    var branchPipeline = branchBuilder.Build();
                    await branchPipeline.ExecuteAsync(context, next);
                }
                else
                {
                    await next(context);
                }
            });
        }

        /// <summary>
        /// Map middleware to specific message subjects.
        /// </summary>
        public static MiddlewarePipelineBuilder MapWhen(
            this MiddlewarePipelineBuilder builder,
            string subject,
            Action<MiddlewarePipelineBuilder> configuration)
        {
            return builder.UseWhen(
                ctx => ctx.Message.Subject == subject,
                configuration);
        }
    }
}
