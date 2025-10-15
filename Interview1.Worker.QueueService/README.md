# Azure Service Bus Worker Service with Middleware & Filter Pipeline

A complete, production-ready Azure Service Bus worker service that provides a powerful middleware and filter pipeline for message processing. This framework mirrors ASP.NET Core's familiar patterns, making it easy for .NET developers to build robust, maintainable message processing systems.

## 🎯 What is This?

This is a **background worker service** that:
- Listens to Azure Service Bus queues for messages
- Processes messages using a flexible task system
- Provides middleware for cross-cutting concerns (logging, authentication, rate limiting, etc.)
- Includes filters for task-specific logic (authorization, validation, result handling)
- Handles errors gracefully with automatic retries and dead-lettering

Think of it as **ASP.NET Core MVC patterns adapted for message processing** - with familiar middleware and filter concepts applied to Service Bus!

> **Note:** This project uses **custom interfaces inspired by ASP.NET Core**, not the actual ASP.NET Core interfaces. The patterns and concepts are the same, but adapted for Service Bus message processing instead of HTTP requests.

## 📋 Table of Contents

- [Quick Start](#-quick-start)
- [Core Concepts](#-core-concepts)
- [Creating Your First Task](#-creating-your-first-task)
- [Understanding Middleware](#-understanding-middleware)
- [Understanding Filters](#-understanding-filters)
- [Configuration](#-configuration)
- [Complete Examples](#-complete-examples)
- [Best Practices](#-best-practices)
- [Troubleshooting](#-troubleshooting)

## 🚀 Quick Start

### Prerequisites

- .NET 10.0 SDK
- Azure Service Bus namespace and connection string
- Visual Studio 2022 or VS Code

### Step 1: Configuration

Add your Azure Service Bus connection string to `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "messaging": "Endpoint=sb://your-namespace.servicebus.windows.net/;SharedAccessKeyName=..."
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

### Step 2: Create Your First Task

Create a new file `Tasks/WelcomeTask.cs`:

```csharp
using Azure.Messaging.ServiceBus;
using Interview1.Worker.QueueService.Utilities;
using Interview1.Worker.QueueService.Filters;

namespace Interview1.Worker.QueueService.Tasks
{
    [Task("Welcome")]  // Message subject that triggers this task
    public class WelcomeTask : BaseTask
    {
        private readonly ILogger<WelcomeTask> _logger;

        public WelcomeTask(ILogger<WelcomeTask> logger)
        {
            _logger = logger;
        }

        public override async Task<ITaskResult> ExecuteAsync(ProcessMessageEventArgs eventArgs)
        {
            // Get the message body
            var messageBody = eventArgs.Message.Body.ToString();
            
            _logger.LogInformation("Processing welcome message: {Message}", messageBody);
            
            // Do your work here
            await Task.Delay(100); // Simulate work
            
            // Return success - message will be completed and removed from queue
            return new CompleteResult();
        }
    }
}
```

### Step 3: Run the Worker

```bash
dotnet run
```

### Step 4: Send a Test Message

Send a message to your Service Bus queue with `Subject = "Welcome"`. The worker will automatically route it to your `WelcomeTask`!

## 🧩 Core Concepts

### Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    Azure Service Bus Queue                    │
└────────────────────────┬────────────────────────────────────┘
                         │ Message arrives
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                     ServiceBusWorkerBase                      │
│                   (Base worker service)                       │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                    Middleware Pipeline                        │
│  ┌────────────────────────────────────────────────────┐     │
│  │ 1. Exception Handling (outermost)                  │     │
│  │ 2. Request ID                                      │     │
│  │ 3. Distributed Tracing                             │     │
│  │ 4. Logging                                         │     │
│  │ 5. Timing                                          │     │
│  │ 6. Authentication (optional)                       │     │
│  │ 7. Rate Limiting (optional)                        │     │
│  └────────────────────────────────────────────────────┘     │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                      Task Resolution                          │
│         (TaskFactory finds task by message subject)           │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                      Filter Pipeline                          │
│  ┌────────────────────────────────────────────────────┐     │
│  │ 1. Authorization Filters                           │     │
│  │ 2. Resource Filters (executing)                    │     │
│  │ 3. Action Filters (executing)                      │     │
│  │ 4. ★ TASK EXECUTION ★                             │     │
│  │ 5. Action Filters (executed)                       │     │
│  │ 6. Result Filters (executing)                      │     │
│  │ 7. Result Execution (Complete/Abandon/etc.)        │     │
│  │ 8. Result Filters (executed)                       │     │
│  │ 9. Resource Filters (executed)                     │     │
│  │                                                     │     │
│  │ Exception Filters (if exception occurs)            │     │
│  └────────────────────────────────────────────────────┘     │
└─────────────────────────────────────────────────────────────┘
```

### Key Components

| Component | Purpose | When to Use |
|-----------|---------|-------------|
| **Task** | Core business logic for processing a specific message type | Always - one per message type |
| **Middleware** | Global cross-cutting concerns (logging, auth, timing) | For logic that applies to ALL messages |
| **Filters** | Task-specific logic (validation, authorization, result handling) | For logic specific to certain tasks |
| **TaskFactory** | Routes messages to tasks based on subject | Automatic - handles routing |
| **Worker** | Receives messages from Service Bus | One per queue |

## 📝 Creating Your First Task

### Step-by-Step Guide

#### 1. Basic Task

The simplest possible task:

```csharp
using Azure.Messaging.ServiceBus;
using Interview1.Worker.QueueService.Utilities;
using Interview1.Worker.QueueService.Filters;

namespace Interview1.Worker.QueueService.Tasks
{
    [Task("HelloWorld")]  // This task handles messages with Subject = "HelloWorld"
    public class HelloWorldTask : BaseTask
    {
        private readonly ILogger<HelloWorldTask> _logger;

        // Constructor - dependency injection works here!
        public HelloWorldTask(ILogger<HelloWorldTask> logger)
        {
            _logger = logger;
        }

        // This method is called when a message arrives
        public override async Task<ITaskResult> ExecuteAsync(ProcessMessageEventArgs eventArgs)
        {
            // Get the message body
            var body = eventArgs.Message.Body.ToString();
            
            _logger.LogInformation("Received: {Body}", body);
            
            // Process the message
            await Task.Delay(10); // Your work here
            
            // Return a result
            return new CompleteResult(); // Message successfully processed
        }
    }
}
```

#### 2. Task with JSON Deserialization

```csharp
using System.Text.Json;

[Task("ProcessOrder")]
public class ProcessOrderTask : BaseTask
{
    private readonly ILogger<ProcessOrderTask> _logger;

    public ProcessOrderTask(ILogger<ProcessOrderTask> logger)
    {
        _logger = logger;
    }

    public override async Task<ITaskResult> ExecuteAsync(ProcessMessageEventArgs eventArgs)
    {
        // Deserialize JSON message body
        var body = eventArgs.Message.Body.ToString();
        var order = JsonSerializer.Deserialize<Order>(body);
        
        if (order == null)
        {
            _logger.LogError("Failed to deserialize order");
            return new DeadLetterResult("InvalidJson", "Could not parse order");
        }
        
        _logger.LogInformation(
            "Processing order {OrderId} for customer {CustomerId}",
            order.OrderId,
            order.CustomerId);
        
        // Process the order
        await ProcessOrderAsync(order);
        
        return new CompleteResult();
    }
    
    private async Task ProcessOrderAsync(Order order)
    {
        // Your business logic here
        await Task.CompletedTask;
    }
    
    private class Order
    {
        public string OrderId { get; set; } = "";
        public string CustomerId { get; set; } = "";
        public decimal Amount { get; set; }
    }
}
```

#### 3. Task with Error Handling

```csharp
[Task("ProcessPayment")]
public class ProcessPaymentTask : BaseTask
{
    private readonly ILogger<ProcessPaymentTask> _logger;

    public ProcessPaymentTask(ILogger<ProcessPaymentTask> logger)
    {
        _logger = logger;
    }

    public override async Task<ITaskResult> ExecuteAsync(ProcessMessageEventArgs eventArgs)
    {
        try
        {
            var payment = ParsePayment(eventArgs.Message.Body.ToString());
            
            // Validate
            if (payment.Amount <= 0)
            {
                return new DeadLetterResult("InvalidAmount", "Amount must be positive");
            }
            
            // Process
            await ProcessPaymentAsync(payment);
            
            return new CompleteResult();
        }
        catch (HttpRequestException ex)
        {
            // Transient error - retry later
            _logger.LogWarning(ex, "Payment gateway unavailable, will retry");
            return new AbandonResult();
        }
        catch (Exception ex)
        {
            // Fatal error - send to dead letter
            _logger.LogError(ex, "Fatal error processing payment");
            return new DeadLetterResult("ProcessingError", ex.Message);
        }
    }
}
```

### Understanding Task Results

Your task must return one of four result types:

| Result Type | What It Does | When to Use |
|------------|--------------|-------------|
| `CompleteResult()` | ✅ Removes message from queue | Success - message processed |
| `AbandonResult()` | 🔄 Returns message to queue | Transient error - retry later |
| `DeadLetterResult(reason, description)` | ☠️ Moves to dead-letter queue | Permanent error - poison message |
| `DeferResult()` | ⏸️ Defers message for later | Needs manual intervention or approval |

**Examples:**

```csharp
// Success
return new CompleteResult();

// Temporary failure (network issue, service down)
return new AbandonResult();

// Permanent failure (invalid data, business rule violation)
return new DeadLetterResult("InvalidData", "Customer ID is required");

// Needs approval or manual processing
return new DeferResult();
```

## 🔧 Understanding Middleware

**Middleware** is for **cross-cutting concerns** that apply to ALL messages, regardless of task type.

### When to Use Middleware

✅ Use middleware for:
- Logging (all messages)
- Performance timing (all messages)
- Authentication (all messages)
- Rate limiting (all messages)
- Distributed tracing (all messages)
- Request ID generation (all messages)

❌ Don't use middleware for:
- Task-specific validation (use filters)
- Business logic (use tasks)
- Authorization based on task requirements (use filters)

### Built-in Middleware

The framework includes ready-to-use middleware:

| Middleware | Purpose | Configuration |
|-----------|---------|---------------|
| `UseExceptionHandler()` | Catches all exceptions | Always first |
| `UseRequestId()` | Assigns unique ID to each message | Early in pipeline |
| `UseLogging()` | Logs message processing | After RequestId |
| `UseTiming()` | Measures execution time | Early in pipeline |
| `UseDistributedTracing()` | OpenTelemetry tracing | Early in pipeline |
| `UseAuthentication()` | Validates auth token | Before business logic |
| `UseRateLimiting()` | Limits message rate per subject | Before expensive operations |
| `UseCircuitBreaker()` | Circuit breaker pattern | Before business logic |

### Configuring Middleware

Edit `Worker.cs` to configure your middleware pipeline:

```csharp
public class Worker : ServiceBusWorkerBase
{
    protected override MiddlewarePipelineBuilder ConfigureMiddleware(
        MiddlewarePipelineBuilder builder)
    {
        return builder
            .UseExceptionHandler()    // 1. Catch exceptions (always first!)
            .UseRequestId()            // 2. Generate request ID
            .UseLogging()              // 3. Log requests
            .UseTiming()               // 4. Measure performance
            .UseDistributedTracing();  // 5. Add tracing
            
        // Optional - uncomment to enable:
        // .UseAuthentication()       // Require auth token
        // .UseRateLimiting()         // Rate limit by subject
    }
}
```

### Creating Custom Middleware

```csharp
using Interview1.Worker.QueueService.Middleware;

namespace Interview1.Worker.QueueService.Middleware.Examples
{
    public class CustomValidationMiddleware : ITaskMiddleware
    {
        private readonly ILogger<CustomValidationMiddleware> _logger;

        public CustomValidationMiddleware(ILogger<CustomValidationMiddleware> logger)
        {
            _logger = logger;
        }

        public async Task InvokeAsync(TaskContext context, TaskDelegate next)
        {
            // BEFORE: Runs before the rest of the pipeline
            _logger.LogInformation("Validating message {MessageId}", context.Message.MessageId);
            
            // Validation logic
            if (string.IsNullOrEmpty(context.Message.Subject))
            {
                // Short-circuit: Don't process this message
                context.Result = new DeadLetterResult("NoSubject", "Message must have a subject");
                return; // Don't call next()
            }
            
            // Call next middleware
            await next(context);
            
            // AFTER: Runs after the pipeline completes
            _logger.LogInformation("Validation complete for {MessageId}", context.Message.MessageId);
        }
    }
}
```

Register your custom middleware in `Program.cs`:

```csharp
builder.Services.AddSingleton<CustomValidationMiddleware>();
```

And use it in `Worker.cs`:

```csharp
protected override MiddlewarePipelineBuilder ConfigureMiddleware(
    MiddlewarePipelineBuilder builder)
{
    return builder
        .UseExceptionHandler()
        .Use<CustomValidationMiddleware>()  // Your custom middleware
        .UseLogging();
}
```

### Inline Middleware

For simple cases, use inline middleware:

```csharp
protected override MiddlewarePipelineBuilder ConfigureMiddleware(
    MiddlewarePipelineBuilder builder)
{
    return builder
        .UseExceptionHandler()
        .Use(async (context, next) =>
        {
            // Before
            context.Items["StartTime"] = DateTime.UtcNow;
            
            await next(context);
            
            // After
            var duration = DateTime.UtcNow - (DateTime)context.Items["StartTime"];
            // Log duration
        })
        .UseLogging();
}
```

## 🎯 Understanding Filters

**Filters** are for **task-specific logic** applied via attributes on your task classes.

### When to Use Filters

✅ Use filters for:
- Task-specific authorization ("this task needs role X")
- Input validation for specific tasks
- Result transformation for specific tasks
- Retry logic for specific task types
- Exception handling for specific errors

❌ Don't use filters for:
- Global concerns (use middleware)
- Core business logic (use task execution)

### Filter Types

Filters execute in a specific order:

```
1. Authorization Filters    → Run FIRST, can short-circuit
2. Resource Filters         → Surround everything
3. Action Filters           → Surround task execution
4. ★ TASK EXECUTION ★
5. Result Filters           → Surround result execution
6. Exception Filters        → Run on any exception
```

### Using Filters

Add filters as attributes on your task class:

```csharp
[Task("ProcessOrder")]
// Authorization: Runs first, checks permissions
[RequireClaim("role", "order-processor")]
// Action: Validates input before task runs
[ValidateJsonBody]
[LogAction]
// Result: Handles retry logic
[MaxRetry(maxRetries: 3)]
// Exception: Handles specific errors
[HandleException(typeof(InvalidOperationException), deadLetter: true)]
public class ProcessOrderTask : BaseTask
{
    public override async Task<ITaskResult> ExecuteAsync(ProcessMessageEventArgs eventArgs)
    {
        // Your task logic here
        return new CompleteResult();
    }
}
```

### Built-in Filters

#### Authorization Filters

```csharp
// Require a specific claim in message application properties
[RequireClaim("role", "admin")]

// Validate message has a subject
[ValidateMessageSubject]
```

#### Action Filters

```csharp
// Validate message body is valid JSON
[ValidateJsonBody]

// Log task execution
[LogAction]

// Add telemetry data
[AddTelemetry]
```

#### Result Filters

```csharp
// Convert Abandon to DeadLetter after max retries
[MaxRetry(maxRetries: 3)]

// Log the result
[LogResult]
```

#### Exception Filters

```csharp
// Handle specific exception types
[HandleException(typeof(InvalidOperationException), deadLetter: true)]
[HandleException(typeof(HttpRequestException), deadLetter: false)] // Abandon

// Log exceptions
[LogException]

// Catch-all: dead-letter on any unhandled error
[DeadLetterOnError]
```

### Creating Custom Filters

#### Custom Authorization Filter

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class RequireApiKeyAttribute : FilterAttribute, IAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var message = context.MessageEventArgs.Message;
        
        // Check for API key in application properties
        if (!message.ApplicationProperties.TryGetValue("ApiKey", out var apiKey) ||
            apiKey?.ToString() != "secret-key")
        {
            // Short-circuit: Don't run the task
            context.Result = new DeadLetterResult("Unauthorized", "Invalid or missing API key");
        }
        
        return Task.CompletedTask;
    }
}
```

#### Custom Action Filter

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class ValidateOrderAttribute : FilterAttribute, IActionFilter
{
    public Task OnActionExecutingAsync(ActionExecutingContext context)
    {
        // Before task execution
        var body = context.MessageEventArgs.Message.Body.ToString();
        
        if (string.IsNullOrEmpty(body))
        {
            context.Result = new DeadLetterResult("EmptyBody", "Message body cannot be empty");
        }
        
        return Task.CompletedTask;
    }

    public Task OnActionExecutedAsync(ActionExecutedContext context)
    {
        // After task execution
        return Task.CompletedTask;
    }
}
```

Use your custom filter:

```csharp
[Task("SecureOrder")]
[RequireApiKey]
[ValidateOrder]
public class SecureOrderTask : BaseTask
{
    // Task implementation
}
```

## ⚙️ Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "messaging": "Endpoint=sb://your-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=..."
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Interview1.Worker.QueueService": "Debug",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

### Environment-Specific Configuration

Create `appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

### User Secrets (for local development)

```bash
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:messaging" "Endpoint=sb://..."
```

### Configuring the Worker

In `Program.cs`, you can customize processor options:

```csharp
// In Program.cs, when registering the worker:
builder.Services.AddHostedService(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Worker>>();
    var serviceBusClient = sp.GetRequiredService<ServiceBusClient>();
    
    var processorOptions = new ServiceBusProcessorOptions
    {
        MaxConcurrentCalls = 5,          // Process 5 messages concurrently
        AutoCompleteMessages = false,    // Manual message completion
        MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(5)
    };
    
    return new Worker(logger, serviceBusClient, sp, processorOptions);
});
```

## 📚 Complete Examples

### Example 1: Simple Order Processing

```csharp
using Azure.Messaging.ServiceBus;
using Interview1.Worker.QueueService.Utilities;
using Interview1.Worker.QueueService.Filters;
using System.Text.Json;

namespace Interview1.Worker.QueueService.Tasks
{
    [Task("SimpleOrder")]
    [LogAction]  // Log when task executes
    public class SimpleOrderTask : BaseTask
    {
        private readonly ILogger<SimpleOrderTask> _logger;

        public SimpleOrderTask(ILogger<SimpleOrderTask> logger)
        {
            _logger = logger;
        }

        public override async Task<ITaskResult> ExecuteAsync(ProcessMessageEventArgs eventArgs)
        {
            var body = eventArgs.Message.Body.ToString();
            var order = JsonSerializer.Deserialize<SimpleOrder>(body);
            
            if (order == null)
            {
                return new DeadLetterResult("InvalidJson", "Could not parse order");
            }
            
            _logger.LogInformation("Processing order {OrderId}", order.OrderId);
            
            // Your business logic
            await Task.Delay(100);
            
            _logger.LogInformation("Order {OrderId} processed successfully", order.OrderId);
            
            return new CompleteResult();
        }
        
        private class SimpleOrder
        {
            public string OrderId { get; set; } = "";
            public decimal Amount { get; set; }
        }
    }
}
```

### Example 2: Secure Payment Processing with Retries

```csharp
using Azure.Messaging.ServiceBus;
using Interview1.Worker.QueueService.Utilities;
using Interview1.Worker.QueueService.Filters;
using Interview1.Worker.QueueService.Filters.Examples;
using System.Text.Json;

namespace Interview1.Worker.QueueService.Tasks
{
    [Task("ProcessPayment")]
    // Authorization
    [RequireClaim("role", "payment-processor")]
    [ValidateMessageSubject]
    // Validation
    [ValidateJsonBody]
    // Logging and telemetry
    [LogAction]
    [AddTelemetry]
    // Error handling
    [MaxRetry(maxRetries: 3)]
    [HandleException(typeof(HttpRequestException), deadLetter: false)] // Retry network errors
    [HandleException(typeof(InvalidOperationException), deadLetter: true)] // Dead-letter invalid operations
    [LogException]
    public class ProcessPaymentTask : BaseTask
    {
        private readonly ILogger<ProcessPaymentTask> _logger;
        private readonly IPaymentGateway _paymentGateway;

        public ProcessPaymentTask(
            ILogger<ProcessPaymentTask> logger,
            IPaymentGateway paymentGateway)
        {
            _logger = logger;
            _paymentGateway = paymentGateway;
        }

        public override async Task<ITaskResult> ExecuteAsync(ProcessMessageEventArgs eventArgs)
        {
            var body = eventArgs.Message.Body.ToString();
            var payment = JsonSerializer.Deserialize<Payment>(body);
            
            if (payment == null)
            {
                return new DeadLetterResult("InvalidJson", "Could not parse payment");
            }
            
            // Validate
            if (payment.Amount <= 0)
            {
                return new DeadLetterResult("InvalidAmount", "Payment amount must be positive");
            }
            
            // Process payment
            _logger.LogInformation(
                "Processing payment {PaymentId} of {Amount:C} for customer {CustomerId}",
                payment.PaymentId,
                payment.Amount,
                payment.CustomerId);
            
            var result = await _paymentGateway.ProcessPaymentAsync(payment);
            
            if (result.IsSuccess)
            {
                _logger.LogInformation("Payment {PaymentId} processed successfully", payment.PaymentId);
                return new CompleteResult();
            }
            else
            {
                _logger.LogWarning("Payment {PaymentId} failed: {Reason}", payment.PaymentId, result.FailureReason);
                return new AbandonResult(); // Will retry
            }
        }
        
        private class Payment
        {
            public string PaymentId { get; set; } = "";
            public string CustomerId { get; set; } = "";
            public decimal Amount { get; set; }
        }
    }
    
    // Mock payment gateway interface
    public interface IPaymentGateway
    {
        Task<PaymentResult> ProcessPaymentAsync(object payment);
    }
    
    public class PaymentResult
    {
        public bool IsSuccess { get; set; }
        public string? FailureReason { get; set; }
    }
}
```

### Example 3: High-Value Order Requiring Approval

```csharp
[Task("HighValueOrder")]
[RequireClaim("role", "order-processor")]
[ValidateJsonBody]
[LogAction]
public class HighValueOrderTask : BaseTask
{
    private readonly ILogger<HighValueOrderTask> _logger;
    private const decimal ApprovalThreshold = 10000m;

    public HighValueOrderTask(ILogger<HighValueOrderTask> logger)
    {
        _logger = logger;
    }

    public override async Task<ITaskResult> ExecuteAsync(ProcessMessageEventArgs eventArgs)
    {
        var body = eventArgs.Message.Body.ToString();
        var order = JsonSerializer.Deserialize<HighValueOrder>(body);
        
        if (order == null)
        {
            return new DeadLetterResult("InvalidJson", "Could not parse order");
        }
        
        if (order.Amount > ApprovalThreshold)
        {
            _logger.LogWarning(
                "Order {OrderId} with amount {Amount:C} requires manual approval. Deferring.",
                order.OrderId,
                order.Amount);
            
            // Defer for manual approval
            return new DeferResult();
        }
        
        // Process normally
        await ProcessOrderAsync(order);
        
        return new CompleteResult();
    }
    
    private async Task ProcessOrderAsync(HighValueOrder order)
    {
        // Your processing logic
        await Task.CompletedTask;
    }
    
    private class HighValueOrder
    {
        public string OrderId { get; set; } = "";
        public decimal Amount { get; set; }
    }
}
```

## 🎓 Best Practices

### 1. Task Organization

```
Tasks/
  ├── Orders/
  │   ├── CreateOrderTask.cs
  │   ├── UpdateOrderTask.cs
  │   └── CancelOrderTask.cs
  ├── Payments/
  │   ├── ProcessPaymentTask.cs
  │   └── RefundPaymentTask.cs
  └── Notifications/
      ├── SendEmailTask.cs
      └── SendSmsTask.cs
```

### 2. Error Handling Strategy

```csharp
try
{
    // Process message
    return new CompleteResult();
}
catch (ValidationException ex)
{
    // Permanent error - bad data
    return new DeadLetterResult("ValidationFailed", ex.Message);
}
catch (HttpRequestException ex)
{
    // Transient error - network issue
    return new AbandonResult(); // Will retry
}
catch (Exception ex)
{
    // Unknown error - log and dead-letter
    _logger.LogError(ex, "Unexpected error");
    return new DeadLetterResult("UnexpectedError", ex.Message);
}
```

### 3. Logging Best Practices

```csharp
// ✅ Good: Structured logging with placeholders
_logger.LogInformation("Processing order {OrderId} for customer {CustomerId}", 
    order.OrderId, order.CustomerId);

// ❌ Bad: String concatenation
_logger.LogInformation("Processing order " + order.OrderId);

// ✅ Good: Log levels
_logger.LogDebug("Detailed info for debugging");
_logger.LogInformation("Normal operation");
_logger.LogWarning("Something unexpected but handled");
_logger.LogError(ex, "Error occurred");
```

### 4. Dependency Injection

```csharp
// Register services in Program.cs
builder.Services.AddSingleton<IMyService, MyService>();
builder.Services.AddHttpClient<IPaymentGateway, PaymentGateway>();

// Use in task
public class MyTask : BaseTask
{
    private readonly IMyService _myService;
    private readonly IPaymentGateway _paymentGateway;
    
    public MyTask(IMyService myService, IPaymentGateway paymentGateway)
    {
        _myService = myService;
        _paymentGateway = paymentGateway;
    }
}
```

### 5. Message Design

**Good message design:**

```json
{
  "orderId": "ORD-12345",
  "customerId": "CUST-789",
  "amount": 99.99,
  "timestamp": "2025-10-15T10:30:00Z"
}
```

**Message properties:**
- Set `Subject` = Task name ("ProcessOrder")
- Set `CorrelationId` for tracking
- Set `MessageId` for idempotency
- Add claims in `ApplicationProperties` for authorization

### 6. Testing

```csharp
// Unit test example
[Fact]
public async Task ProcessOrderTask_ValidOrder_ReturnsComplete()
{
    // Arrange
    var logger = new Mock<ILogger<ProcessOrderTask>>();
    var task = new ProcessOrderTask(logger.Object);
    
    var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
        body: new BinaryData("{\"orderId\": \"123\", \"amount\": 50}"),
        subject: "ProcessOrder");
    
    var args = /* create ProcessMessageEventArgs */;
    
    // Act
    var result = await task.ExecuteAsync(args);
    
    // Assert
    Assert.IsType<CompleteResult>(result);
}
```

## 🔍 Troubleshooting

### Messages Not Being Processed

**Symptom:** Worker is running but messages stay in queue

**Solutions:**
1. Check that message `Subject` matches your `[Task("Name")]` attribute
2. Verify connection string is correct
3. Check queue name in Worker constructor
4. Look for exceptions in logs

### Messages Going to Dead Letter Queue

**Symptom:** All messages end up in dead letter queue

**Solutions:**
1. Check logs for exceptions
2. Verify JSON deserialization matches message format
3. Check filter authorization requirements
4. Review validation logic

### High CPU Usage

**Symptom:** Worker consuming too much CPU

**Solutions:**
1. Reduce `MaxConcurrentCalls` in processor options
2. Add delays in tight loops
3. Check for infinite loops in task logic
4. Profile your code

### Messages Being Reprocessed

**Symptom:** Same message processed multiple times

**Solutions:**
1. Ensure you return a result (`CompleteResult`, etc.)
2. Check that result is being executed
3. Verify no unhandled exceptions
4. Check lock duration vs processing time

### Authentication Errors

**Symptom:** "Unauthorized" errors

**Solutions:**
1. Check Azure Service Bus connection string
2. Verify Shared Access Policy has correct permissions
3. For managed identity, ensure role assignments are correct
4. Check firewall rules

## 📖 Additional Resources

### Detailed Documentation

- **[Filters Documentation](Filters/README.md)** - Complete guide to all filter types
- **[Middleware Documentation](Middleware/README.md)** - Complete guide to middleware

### Project Structure

```
Interview1.Worker.QueueService/
├── Program.cs                    # Application entry point
├── Worker.cs                     # Main worker class
├── ServiceBusWorkerBase.cs       # Base worker implementation
├── Tasks/                        # Your task implementations
│   ├── ProcessOrderTask.cs
│   └── TestTask.cs
├── Filters/                      # Filter infrastructure and examples
│   ├── FilterPipeline.cs
│   ├── ITaskFilter.cs
│   ├── README.md
│   └── Examples/
├── Middleware/                   # Middleware infrastructure and examples
│   ├── MiddlewarePipeline.cs
│   ├── ITaskMiddleware.cs
│   ├── README.md
│   └── Examples/
├── Utilities/                    # Core utilities
│   ├── BaseTask.cs
│   ├── TaskFactory.cs
│   └── TaskAttribute.cs
└── Extensions/                   # Service registration extensions
    └── ServiceCollectionExtension.cs
```

## 🚀 Next Steps

1. **Create your first task** - Start with a simple hello world task
2. **Add filters** - Apply validation and logging filters
3. **Configure middleware** - Set up your middleware pipeline
4. **Test it** - Send messages to your queue
5. **Monitor** - Watch the logs and adjust

## 💡 Tips for Learners

1. **Start Simple**: Begin with a basic task, then add filters and middleware
2. **Use Logging**: Add lots of logging to understand the flow
3. **Read the READMEs**: Check `Filters/README.md` and `Middleware/README.md`
4. **Experiment**: Try different result types and see what happens
5. **Check Examples**: Look at `ProcessOrderTask.cs` for a complete example
6. **Ask Questions**: The code is well-commented - read the comments!

## 📞 Support

For issues or questions:
1. Check the logs for error messages
2. Review the filter and middleware documentation
3. Look at example tasks in the `Tasks/` folder
4. Check Azure Service Bus metrics in Azure Portal

---

**Happy message processing! 🎉**
