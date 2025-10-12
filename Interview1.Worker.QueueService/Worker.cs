using Azure.Messaging.ServiceBus;
using Interview1.Worker.QueueService.Filters;
using Interview1.Worker.QueueService.Middleware;
using Interview1.Worker.QueueService.Utilities;

namespace Interview1.Worker.QueueService;

public class Worker : ServiceBusWorkerBase
{
    private readonly ILogger<Worker> _logger;
    private readonly MiddlewarePipeline _middlewarePipeline;

    public Worker(
        ILogger<Worker> logger,
        ServiceBusClient serviceBusClient,
        IServiceProvider serviceProvider)
        : base(logger, serviceBusClient, serviceProvider, "orders")
    {
        _logger = logger;
        
        // Build the middleware pipeline
        _middlewarePipeline = ConfigureMiddleware(new MiddlewarePipelineBuilder(serviceProvider)).Build();
    }

    /// <summary>
    /// Configure the middleware pipeline.
    /// Override this method in derived classes to customize middleware.
    /// Middleware execute in the order they are added.
    /// </summary>
    protected virtual MiddlewarePipelineBuilder ConfigureMiddleware(MiddlewarePipelineBuilder builder)
    {
        // Default middleware configuration
        // Order matters! Middleware execute in the order added.
        return builder;
            //.UseExceptionHandler()        // 1. Catch all exceptions (outermost)
            //.UseRequestId()                // 2. Assign request ID
            //.UseLogging()                  // 3. Log request/response
            //.UseTiming()                   // 4. Measure execution time
            //.UseDistributedTracing()       // 5. Add distributed tracing
            //.UseSubjectValidation()        // 6. Validate message subject
            //.UseMessageExpiration();       // 7. Check message expiration
            
        // Optional middleware (uncomment to enable):
        // .UseAuthentication()         // Require authentication
        // .UseRateLimiting()           // Rate limit by subject
        // .UseCircuitBreaker()         // Circuit breaker pattern
        // .UseMessageSizeValidation()  // Validate message size
        // .UseDetailedLogging()        // Detailed request logging
        
        // Custom inline middleware example:
        // .Use(async (context, next) =>
        // {
        //     // Before
        //     await next(context);
        //     // After
        // });
    }

    protected override async Task ProcessMessageAsync(ProcessMessageEventArgs args, IServiceProvider serviceProvider)
    {
        _logger.LogInformation("Processing message...");

        // Create the task context
        var context = new TaskContext(args, serviceProvider);

        // Execute middleware pipeline
        await _middlewarePipeline.ExecuteAsync(context, async (ctx) =>
        {
            // This is the core handler that runs after all middleware
            var taskFactory = serviceProvider.GetRequiredService<ITaskFactory>();
            ctx.Task = taskFactory.GetTaskByTaskName(ctx.Message.Subject);

            // Get filters from the task
            var filters = ctx.Task.GetFilters();

            // Create and execute the filter pipeline
            var pipeline = new FilterPipeline(ctx.MessageEventArgs, serviceProvider, ctx.Task, filters);
            ctx.Result = await pipeline.ExecuteAsync();
        });

        // Execute the result (Complete, Abandon, DeadLetter, or Defer the message)
        // Middleware may have already set the result
        if (context.Result != null)
        {
            await context.Result.ExecuteResultAsync(args);
        }

        _logger.LogInformation("Message processing completed");
    }
}
