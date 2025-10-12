using Azure.Messaging.ServiceBus;
using Interview1.Worker.QueueService.Filters;
using Interview1.Worker.QueueService.Filters.Examples;
using Interview1.Worker.QueueService.Utilities;
using System.Text.Json;

namespace Interview1.Worker.QueueService.Tasks
{
    /// <summary>
    /// Example task demonstrating all filter types working together.
    /// 
    /// Filter Execution Order:
    /// 1. Authorization Filters (ValidateMessageSubject, RequireClaim)
    /// 2. Resource Filters - Executing (MeasureExecutionTime)
    /// 3. Action Filters - Executing (ValidateJsonBody, LogAction, AddTelemetry)
    /// 4. Task Execution (ProcessOrderAsync)
    /// 5. Action Filters - Executed (reverse order)
    /// 6. Result Filters - Executing (MaxRetry, LogResult)
    /// 7. Result Execution (Complete/Abandon/DeadLetter/Defer)
    /// 8. Result Filters - Executed (reverse order)
    /// 9. Resource Filters - Executed (MeasureExecutionTime)
    /// 
    /// Exception Filters run if any exception occurs at any stage.
    /// </summary>
    [Task("ProcessOrder")]
    // Authorization filters (run first)
    [ValidateMessageSubject]
    [RequireClaim("role", "order-processor")]
    // Resource filters (surround everything)
    [MeasureExecutionTime]
    // Action filters (surround task execution)
    [ValidateJsonBody]
    [LogAction]
    [AddTelemetry]
    // Result filters (surround result execution)
    [MaxRetry(maxRetries: 3)]
    [LogResult]
    // Exception filters (run on any exception)
    [LogException]
    [HandleException(typeof(InvalidOperationException), deadLetter: true)]
    [DeadLetterOnError] // Catch-all, runs last (Order = int.MaxValue)
    public class ProcessOrderTask : BaseTask
    {
        private readonly ILogger<ProcessOrderTask> _logger;

        public ProcessOrderTask(ILogger<ProcessOrderTask> logger)
        {
            _logger = logger;
        }

        public override async Task<ITaskResult> ExecuteAsync(ProcessMessageEventArgs eventArgs)
        {
            _logger.LogInformation("Processing order...");

            var body = eventArgs.Message.Body.ToString();
            var order = JsonSerializer.Deserialize<OrderDto>(body);

            if (order == null)
            {
                _logger.LogWarning("Failed to deserialize order");
                return new DeadLetterResult("DeserializationFailed", "Could not deserialize order");
            }

            _logger.LogInformation(
                "Processing order {OrderId} for customer {CustomerId} with amount {Amount:C}",
                order.OrderId,
                order.CustomerId,
                order.Amount);

            // Simulate processing
            await Task.Delay(100);

            // Simulate conditional logic
            if (order.Amount < 0)
            {
                _logger.LogError("Invalid order amount: {Amount}", order.Amount);
                return new DeadLetterResult("InvalidAmount", "Order amount cannot be negative");
            }

            if (order.Amount > 10000)
            {
                _logger.LogWarning("High-value order requires manual approval. Deferring message.");
                return new DeferResult();
            }

            _logger.LogInformation("Order processed successfully");
            return new CompleteResult();
        }

        private class OrderDto
        {
            public string? OrderId { get; set; }
            public string? CustomerId { get; set; }
            public decimal Amount { get; set; }
        }
    }
}
