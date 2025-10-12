using Azure.Messaging.ServiceBus;

namespace Interview1.Worker.QueueService;

public abstract class ServiceBusWorkerBase : BackgroundService
{
    private readonly ILogger _logger;
    private readonly ServiceBusClient _serviceBusClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly string _queueName;
    private readonly ServiceBusProcessorOptions _processorOptions;
    private ServiceBusProcessor? _processor;

    protected ServiceBusWorkerBase(
        ILogger logger,
        ServiceBusClient serviceBusClient,
        IServiceProvider serviceProvider,
        string queueName,
        ServiceBusProcessorOptions? processorOptions = null)
    {
        _logger = logger;
        _serviceBusClient = serviceBusClient;
        _serviceProvider = serviceProvider;
        _queueName = queueName;
        _processorOptions = processorOptions ?? new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = 1,
            AutoCompleteMessages = false
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor = _serviceBusClient.CreateProcessor(_queueName, _processorOptions);

        _processor.ProcessMessageAsync += MessageHandlerWrapper;
        _processor.ProcessErrorAsync += ErrorHandler;

        _logger.LogInformation("Starting Service Bus processor for '{QueueName}' queue...", _queueName);

        await _processor.StartProcessingAsync(stoppingToken);

        _logger.LogInformation("Service Bus processor started successfully");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    private async Task MessageHandlerWrapper(ProcessMessageEventArgs args)
    {
        string body = args.Message.Body.ToString();
        _logger.LogInformation("Received message: {MessageBody}", body);
        _logger.LogInformation("Message ID: {MessageId}", args.Message.MessageId);
        _logger.LogInformation("Sequence Number: {SequenceNumber}", args.Message.SequenceNumber);

        // Create a new scope for this message
        using var scope = _serviceProvider.CreateScope();

        try
        {
            // Call the derived class's message handler with the scoped service provider
            // The derived class is responsible for executing the result
            await ProcessMessageAsync(args, scope.ServiceProvider);
            
            _logger.LogInformation("Message processing completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message: {MessageId}", args.Message.MessageId);

            // If processing fails, abandon the message so it can be retried
            await args.AbandonMessageAsync(args.Message);
        }
    }

    /// <summary>
    /// Override this method to process messages. A new scope is created for each message.
    /// Use the scopedServiceProvider to resolve scoped services.
    /// </summary>
    protected abstract Task ProcessMessageAsync(ProcessMessageEventArgs args, IServiceProvider serviceProvider);

    private Task ErrorHandler(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception, "Error in Service Bus processor: {ErrorSource}", args.ErrorSource);
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Stopping Service Bus processor...");

        if (_processor != null)
        {
            await _processor.StopProcessingAsync(stoppingToken);
            await _processor.DisposeAsync();
        }

        await base.StopAsync(stoppingToken);
    }
}