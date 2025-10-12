using Projects;

var builder = DistributedApplication.CreateBuilder(args);

// Add SQL Server container for Service Bus emulator
var sql = builder.AddSqlServer("sql")
                 .WithLifetime(ContainerLifetime.Persistent);

// Add Azure Service Bus emulator
// The emulator will automatically use the SQL Server instance
var serviceBus = builder.AddAzureServiceBus("messaging")
                        .RunAsEmulator();

// Add a queue to the Service Bus
var queue = serviceBus.AddServiceBusQueue("orders");

// Add API service with Service Bus reference
var apiService = builder.AddProject<Interview1>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithReference(serviceBus);

// Add Worker service with Service Bus reference
builder.AddProject<Interview1_Worker_QueueService>("interview1-worker-queueservice")
    .WithReference(serviceBus);

builder.Build().Run();
