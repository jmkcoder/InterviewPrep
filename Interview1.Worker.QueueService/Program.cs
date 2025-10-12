using Interview1.Worker.QueueService;
using Interview1.Worker.QueueService.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// Add Azure Service Bus client
builder.AddAzureServiceBusClient("messaging");

// Add task processing with middleware support
builder.Services.AddTaskProcessing();

// Optional: Configure middleware
// builder.Services.AddRateLimiting(maxRequestsPerMinute: 100);
// builder.Services.AddCircuitBreaker(failureThreshold: 5);
// builder.Services.AddMessageSizeValidation(maxSizeInBytes: 1024 * 1024);

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
