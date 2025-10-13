using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Text.Json;

namespace Interview1.Messaging;

[ApiController]
[Route("api/[controller]")]
public class MessagingController : ControllerBase
{
    private readonly ServiceBusClient _serviceBusClient;
    private readonly ILogger<MessagingController> _logger;

    public MessagingController(ServiceBusClient serviceBusClient, ILogger<MessagingController> logger)
    {
        _serviceBusClient = serviceBusClient;
        _logger = logger;
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendMessage([FromBody] OrderMessage message)
    {
        try
        {
            // Create a sender for the "orders" queue
            await using var sender = _serviceBusClient.CreateSender("orders");

            // Serialize the message
            var messageBody = JsonSerializer.Serialize(message);
            var serviceBusMessage = new ServiceBusMessage(messageBody)
            {
                MessageId = Guid.NewGuid().ToString(),
                ContentType = "application/json"
            };

            // Add custom properties if needed
            serviceBusMessage.ApplicationProperties.Add("MessageType", "Order");
            serviceBusMessage.ApplicationProperties.Add("Timestamp", DateTime.UtcNow.ToString("o"));

            // Propagate distributed tracing context
            var activity = Activity.Current;
            if (activity != null)
            {
                serviceBusMessage.ApplicationProperties.Add("Diagnostic-Id", activity.Id);
                serviceBusMessage.ApplicationProperties.Add("traceparent", activity.Id);
                if (!string.IsNullOrEmpty(activity.TraceStateString))
                {
                    serviceBusMessage.ApplicationProperties.Add("tracestate", activity.TraceStateString);
                }
            }

            serviceBusMessage.Subject = message.Subject;

            // Send the message
            await sender.SendMessageAsync(serviceBusMessage);

            _logger.LogInformation("Message sent to queue: {MessageId}", serviceBusMessage.MessageId);

            return Ok(new
            {
                success = true,
                messageId = serviceBusMessage.MessageId,
                queueName = "orders"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to Service Bus");
            return StatusCode(500, new { error = "Failed to send message", details = ex.Message });
        }
    }

    [HttpPost("send-batch")]
    public async Task<IActionResult> SendBatchMessages([FromBody] List<OrderMessage> messages)
    {
        try
        {
            await using var sender = _serviceBusClient.CreateSender("orders");

            // Create a batch
            using ServiceBusMessageBatch messageBatch = await sender.CreateMessageBatchAsync();

            foreach (var message in messages)
            {
                var messageBody = JsonSerializer.Serialize(message);
                var serviceBusMessage = new ServiceBusMessage(messageBody)
                {
                    MessageId = Guid.NewGuid().ToString(),
                    ContentType = "application/json"
                };

                serviceBusMessage.Subject = message.Subject;

                // Propagate distributed tracing context
                var activity = Activity.Current;
                if (activity != null)
                {
                    serviceBusMessage.ApplicationProperties.Add("Diagnostic-Id", activity.Id);
                    serviceBusMessage.ApplicationProperties.Add("traceparent", activity.Id);
                    if (!string.IsNullOrEmpty(activity.TraceStateString))
                    {
                        serviceBusMessage.ApplicationProperties.Add("tracestate", activity.TraceStateString);
                    }
                }

                // Try to add the message to the batch
                if (!messageBatch.TryAddMessage(serviceBusMessage))
                {
                    _logger.LogWarning("Message was too large to fit in the batch");
                }
            }

            // Send the batch
            await sender.SendMessagesAsync(messageBatch);

            _logger.LogInformation("Sent batch of {Count} messages to queue", messageBatch.Count);

            return Ok(new
            {
                success = true,
                messageCount = messageBatch.Count,
                queueName = "orders"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending batch messages to Service Bus");
            return StatusCode(500, new { error = "Failed to send batch messages", details = ex.Message });
        }
    }
}

public record OrderMessage(
    string OrderId,
    string CustomerName,
    decimal Amount,
    DateTime OrderDate,
    string Status,
    string Subject
);
