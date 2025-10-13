using Interview1.Worker.QueueService;
using Interview1.Worker.QueueService.Extensions;
using Interview1.Worker.QueueService.Middleware;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// Add Azure Service Bus client
builder.AddAzureServiceBusClient("messaging");

// Add task processing with middleware support
builder.Services.AddTaskProcessing();

// Register middleware services
builder.Services.AddTaskMiddleware();

// Register individual middleware for dependency injection
builder.Services.AddSingleton<Interview1.Worker.QueueService.Middleware.Examples.ExceptionHandlingMiddleware>();
builder.Services.AddSingleton<Interview1.Worker.QueueService.Middleware.Examples.RequestIdMiddleware>();
builder.Services.AddSingleton<Interview1.Worker.QueueService.Middleware.Examples.DistributedTracingMiddleware>();
builder.Services.AddSingleton<Interview1.Worker.QueueService.Middleware.Examples.LoggingMiddleware>();
builder.Services.AddSingleton<Interview1.Worker.QueueService.Middleware.Examples.TimingMiddleware>();

// Optional: Configure middleware
// builder.Services.AddRateLimiting(maxRequestsPerMinute: 100);
// builder.Services.AddCircuitBreaker(failureThreshold: 5);
// builder.Services.AddMessageSizeValidation(maxSizeInBytes: 1024 * 1024);

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
