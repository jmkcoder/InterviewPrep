# Task Middleware System

A complete middleware pipeline for Service Bus message processing that mirrors ASP.NET Core middleware behavior.

## Overview

Middleware provides a mechanism for inspecting, modifying, or short-circuiting message processing before it reaches your tasks and filters. Think of middleware as the outermost layer that wraps your entire message processing pipeline.

This middleware system is **inspired by ASP.NET Core middleware** but uses **custom interfaces** designed specifically for Service Bus message processing. The patterns and concepts are the same, but adapted for message handling instead of HTTP requests.

> **Note:** These are **custom interfaces** (`ITaskMiddleware`, `TaskContext`, `TaskDelegate`), not the actual ASP.NET Core middleware interfaces. However, they follow the same design patterns for familiarity.

```
Message → Middleware Pipeline → Filter Pipeline → Task Execution → Result
```

## Middleware vs Filters

### When to Use Middleware
- **Cross-cutting concerns** that apply to ALL messages regardless of task
- **Early validation** (message structure, size, expiration)
- **Request/response logging** and timing
- **Global exception handling**
- **Rate limiting and circuit breakers**
- **Authentication/authorization** at the message level
- **Distributed tracing** setup

### When to Use Filters
- **Task-specific** logic that varies by task type
- **Authorization** based on task requirements
- **Validation** of business rules
- **Result transformation** specific to task outcomes
- **Exception handling** for specific task errors

### Key Differences

| Aspect | Middleware | Filters |
|--------|-----------|---------|
| **Scope** | Global, applies to all messages | Task-specific, applied via attributes |
| **Registration** | In Worker's `ConfigureMiddleware` | As attributes on task classes |
| **Execution** | Always runs (unless short-circuited) | Only runs for specific tasks |
| **Order** | Sequential (registered order) | Ordered by filter type and `Order` property |
| **Context** | `TaskContext` (no task info initially) | Specific filter contexts with task info |
| **Use Case** | Infrastructure concerns | Business logic concerns |

## Creating Middleware

### Simple Middleware

```csharp
public class SimpleMiddleware : ITaskMiddleware
{
    private readonly ILogger<SimpleMiddleware> _logger;

    public SimpleMiddleware(ILogger<SimpleMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(TaskContext context, TaskDelegate next)
    {
        // Before: runs before the rest of the pipeline
        _logger.LogInformation("Before processing");

        // Call next middleware/handler
        await next(context);

        // After: runs after the pipeline completes
        _logger.LogInformation("After processing");
    }
}
```

### Short-Circuiting Middleware

```csharp
public class AuthenticationMiddleware : ITaskMiddleware
{
    public async Task InvokeAsync(TaskContext context, TaskDelegate next)
    {
        if (!IsAuthenticated(context.Message))
        {
            // Short-circuit: don't call next
            context.Result = new DeadLetterResult("Unauthenticated", "...");
            return;
        }

        await next(context); // Continue if authenticated
    }
}
```

### Conditional Middleware

```csharp
public class ConditionalMiddleware : ITaskMiddleware
{
    public async Task InvokeAsync(TaskContext context, TaskDelegate next)
    {
        if (context.Message.Subject == "SpecialTask")
        {
            // Special handling for specific subjects
            await DoSpecialWork(context);
        }

        await next(context);
    }
}
```

## Configuring Middleware

### In Worker Class

```csharp
public class Worker : ServiceBusWorkerBase
{
    protected override MiddlewarePipelineBuilder ConfigureMiddleware(
        MiddlewarePipelineBuilder builder)
    {
        return builder
            .UseExceptionHandler()       // First: catch all exceptions
            .UseRequestId()               // Add request ID
            .UseLogging()                 // Log requests
            .UseTiming()                  // Measure timing
            .UseAuthentication()          // Check auth
            .UseSubjectValidation()       // Validate subject
            .UseRateLimiting();           // Rate limit
    }
}
```

### Inline Middleware

```csharp
protected override MiddlewarePipelineBuilder ConfigureMiddleware(
    MiddlewarePipelineBuilder builder)
{
    return builder
        .UseExceptionHandler()
        .Use(async (context, next) =>
        {
            // Inline middleware
            context.Items["CustomData"] = "value";
            await next(context);
        })
        .UseLogging();
}
```

### Conditional Branching

```csharp
protected override MiddlewarePipelineBuilder ConfigureMiddleware(
    MiddlewarePipelineBuilder builder)
{
    return builder
        .UseWhen(
            ctx => ctx.Message.Subject == "PriorityTask",
            branch => branch
                .UseLogging()
                .UseTiming())
        .UseAuthentication(); // Always runs
}
```

### Subject-Specific Middleware

```csharp
protected override MiddlewarePipelineBuilder ConfigureMiddleware(
    MiddlewarePipelineBuilder builder)
{
    return builder
        .MapWhen("OrderTask", orderBranch =>
        {
            orderBranch.UseLogging().UseTiming();
        })
        .MapWhen("PaymentTask", paymentBranch =>
        {
            paymentBranch.UseAuthentication().UseRateLimiting();
        });
}
```

## Built-in Middleware

### 1. ExceptionHandlingMiddleware
Catches all exceptions and provides graceful error handling.

```csharp
builder.UseExceptionHandler();
```

**Features:**
- Catches all unhandled exceptions
- Dead-letters poison messages (InvalidOperationException, ArgumentException)
- Abandons transient errors (TimeoutException, HttpRequestException)
- Implements max retry logic

**Position:** First (outermost)

### 2. RequestIdMiddleware
Assigns a unique ID to each message processing operation.

```csharp
builder.UseRequestId();
```

**Features:**
- Generates unique request ID
- Uses CorrelationId from message if available
- Stores in `context.Items["RequestId"]`

**Position:** Early in pipeline

### 3. LoggingMiddleware
Logs the start and completion of message processing.

```csharp
builder.UseLogging();
```

**Logs:**
- Message ID, Subject, Delivery Count
- Result type
- Exceptions

**Position:** After RequestId, before business logic

### 4. DetailedLoggingMiddleware
Logs detailed message information (body, properties, etc.).

```csharp
builder.UseDetailedLogging();
```

**Position:** After RequestId

### 5. TimingMiddleware
Measures and logs execution time.

```csharp
builder.UseTiming();
```

**Features:**
- Measures total execution time
- Logs with appropriate level based on duration
- Stores elapsed time in `context.Items["ElapsedMilliseconds"]`

**Position:** Early in pipeline to capture total time

### 6. DistributedTracingMiddleware
Adds distributed tracing with Activity/OpenTelemetry.

```csharp
builder.UseDistributedTracing();
```

**Features:**
- Creates Activity for each message
- Adds tags for message properties
- Sets status based on success/failure

**Position:** Early in pipeline

### 7. AuthenticationMiddleware
Validates message has required authentication information.

```csharp
builder.UseAuthentication();
```

**Features:**
- Checks for Authorization property
- Dead-letters unauthenticated messages
- Stores auth info in context

**Position:** Before business logic

### 8. ValidateSubjectMiddleware
Ensures message has a valid subject.

```csharp
builder.UseSubjectValidation();
```

**Position:** Early in pipeline

### 9. ValidateMessageSizeMiddleware
Validates message body size.

```csharp
services.AddMessageSizeValidation(maxSizeInBytes: 1024 * 1024); // 1MB
builder.UseMessageSizeValidation();
```

**Position:** Early in pipeline

### 10. MessageExpirationMiddleware
Checks if message has expired.

```csharp
builder.UseMessageExpiration();
```

**Position:** Early in pipeline

### 11. RateLimitingMiddleware
Implements rate limiting per subject.

```csharp
services.AddRateLimiting(maxRequestsPerMinute: 100);
builder.UseRateLimiting();
```

**Features:**
- Rate limits by message subject
- Defers messages when limit exceeded
- Sliding window algorithm

**Position:** Before expensive operations

### 12. CircuitBreakerMiddleware
Implements circuit breaker pattern.

```csharp
services.AddCircuitBreaker(failureThreshold: 5, breakDuration: TimeSpan.FromMinutes(1));
builder.UseCircuitBreaker();
```

**Features:**
- Opens circuit after consecutive failures
- Defers messages while circuit is open
- Automatically attempts to close after duration

**Position:** Before business logic

## Recommended Middleware Order

```csharp
protected override MiddlewarePipelineBuilder ConfigureMiddleware(
    MiddlewarePipelineBuilder builder)
{
    return builder
        // 1. Exception handling (outermost)
        .UseExceptionHandler()
        
        // 2. Request tracking
        .UseRequestId()
        .UseDistributedTracing()
        
        // 3. Logging and monitoring
        .UseLogging()
        .UseTiming()
        
        // 4. Early validation (fail fast)
        .UseSubjectValidation()
        .UseMessageExpiration()
        .UseMessageSizeValidation()
        
        // 5. Authentication and authorization
        .UseAuthentication()
        
        // 6. Resilience patterns
        .UseRateLimiting()
        .UseCircuitBreaker()
        
        // 7. Detailed logging (optional, for debugging)
        // .UseDetailedLogging()
        
        // After all middleware: Filter pipeline → Task execution
        ;
}
```

## TaskContext

The context object that flows through middleware:

```csharp
public class TaskContext
{
    // Message information
    public ProcessMessageEventArgs MessageEventArgs { get; }
    public ServiceBusReceivedMessage Message { get; }
    
    // Services
    public IServiceProvider ServiceProvider { get; }
    
    // Task (set during processing)
    public BaseTask? Task { get; set; }
    
    // Result (can be set by middleware to short-circuit)
    public ITaskResult? Result { get; set; }
    
    // Shared state
    public IDictionary<string, object?> Items { get; }
    
    // Status
    public bool IsCanceled { get; set; }
    public Exception? Exception { get; set; }
    public bool ExceptionHandled { get; set; }
    
    // Identifiers
    public string RequestId { get; set; }
    public CancellationToken CancellationToken { get; }
}
```

### Sharing Data

```csharp
// In middleware 1
context.Items["UserId"] = "user123";
context.Items["StartTime"] = DateTime.UtcNow;

// In middleware 2 or filters
var userId = context.Items["UserId"] as string;
var startTime = (DateTime)context.Items["StartTime"];
```

## Complete Example

### Custom Worker with Full Middleware Pipeline

```csharp
public class ProductionWorker : ServiceBusWorkerBase
{
    public ProductionWorker(
        ILogger<ProductionWorker> logger,
        ServiceBusClient serviceBusClient,
        IServiceProvider serviceProvider)
        : base(logger, serviceBusClient, serviceProvider, "orders")
    {
    }

    protected override MiddlewarePipelineBuilder ConfigureMiddleware(
        MiddlewarePipelineBuilder builder)
    {
        return builder
            // Global exception handling
            .UseExceptionHandler()
            
            // Request tracking and tracing
            .UseRequestId()
            .UseDistributedTracing()
            
            // Logging
            .UseLogging()
            
            // Performance monitoring
            .UseTiming()
            
            // Early validation
            .UseSubjectValidation()
            .UseMessageExpiration()
            
            // Conditional authentication for specific subjects
            .UseWhen(
                ctx => ctx.Message.Subject?.StartsWith("Secure") == true,
                auth => auth.UseAuthentication())
            
            // Rate limiting for high-volume tasks
            .UseRateLimiting()
            
            // Circuit breaker for resilience
            .UseCircuitBreaker()
            
            // Custom business logic middleware
            .Use(async (context, next) =>
            {
                // Custom logic before processing
                if (context.Message.DeliveryCount > 3)
                {
                    _logger.LogWarning(
                        "Message {MessageId} has high delivery count: {Count}",
                        context.Message.MessageId,
                        context.Message.DeliveryCount);
                }
                
                await next(context);
                
                // Custom logic after processing
            });
    }
}
```

## Testing Middleware

```csharp
[Fact]
public async Task LoggingMiddleware_LogsMessageProcessing()
{
    // Arrange
    var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
    var logger = loggerFactory.CreateLogger<LoggingMiddleware>();
    var middleware = new LoggingMiddleware(logger);
    
    var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
        body: new BinaryData("test"),
        messageId: "123",
        subject: "TestSubject");
    
    var args = /* create ProcessMessageEventArgs */;
    var context = new TaskContext(args, serviceProvider);
    var nextCalled = false;
    
    // Act
    await middleware.InvokeAsync(context, ctx =>
    {
        nextCalled = true;
        return Task.CompletedTask;
    });
    
    // Assert
    Assert.True(nextCalled);
}

[Fact]
public async Task AuthenticationMiddleware_MissingAuth_ShortCircuits()
{
    // Arrange
    var middleware = new AuthenticationMiddleware(logger);
    var context = new TaskContext(messageWithoutAuth, serviceProvider);
    var nextCalled = false;
    
    // Act
    await middleware.InvokeAsync(context, ctx =>
    {
        nextCalled = true;
        return Task.CompletedTask;
    });
    
    // Assert
    Assert.False(nextCalled); // Should short-circuit
    Assert.NotNull(context.Result);
    Assert.IsType<DeadLetterResult>(context.Result);
}
```

## Best Practices

1. **Order Matters**: Middleware execute in the order registered
   - Put exception handling first
   - Put logging early but after request ID
   - Put authentication before business logic
   - Put expensive operations last

2. **Short-Circuit When Appropriate**: 
   - Return early if validation fails
   - Set `context.Result` to indicate why

3. **Use Context.Items for Sharing**:
   - Share data between middleware
   - Pass information to filters/tasks

4. **Keep Middleware Stateless**:
   - Use singletons when possible
   - Store per-request state in `context.Items`

5. **Handle Exceptions Gracefully**:
   - Use ExceptionHandlingMiddleware as first middleware
   - Or use try-catch in individual middleware

6. **Log Appropriately**:
   - Use RequestId for correlation
   - Log at appropriate levels
   - Include relevant context

7. **Test Independently**:
   - Test each middleware in isolation
   - Mock next delegate
   - Verify short-circuiting behavior

## Common Patterns

### Pattern 1: Global Infrastructure
```csharp
builder
    .UseExceptionHandler()
    .UseRequestId()
    .UseLogging()
    .UseTiming();
```

### Pattern 2: Security-First
```csharp
builder
    .UseExceptionHandler()
    .UseAuthentication()
    .UseSubjectValidation()
    .UseLogging();
```

### Pattern 3: High-Performance
```csharp
builder
    .UseExceptionHandler()
    .UseRateLimiting()
    .UseCircuitBreaker()
    .UseTiming();
```

### Pattern 4: Development/Debugging
```csharp
builder
    .UseExceptionHandler()
    .UseRequestId()
    .UseDetailedLogging()  // More verbose
    .UseTiming()
    .UseDistributedTracing();
```

## Middleware + Filters

Middleware and filters work together:

```
Message
  ↓
ExceptionMiddleware (catch all)
  ↓
LoggingMiddleware (log start)
  ↓
AuthenticationMiddleware (check auth)
  ↓
[Filter Pipeline Starts]
  ↓
Authorization Filters (task-specific auth)
  ↓
Resource Filters
  ↓
Action Filters
  ↓
Task Execution
  ↓
Result Filters
  ↓
[Filter Pipeline Ends]
  ↓
LoggingMiddleware (log end)
  ↓
Result Execution (Complete/Abandon/etc.)
```

Middleware wraps the entire filter + task pipeline, while filters are task-specific.
