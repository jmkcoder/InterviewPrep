# Task Filter System

A complete filter pipeline for Service Bus message processing that mirrors ASP.NET Core MVC's filter behavior.

## Overview

This filter system provides the same powerful, well-understood filter pipeline as ASP.NET Core MVC, adapted for Azure Service Bus message processing. Filters allow you to run code before and after specific stages of task execution, handle exceptions, and short-circuit the pipeline when needed.

## Filter Types

Filters run in a specific order and serve different purposes:

### 1. **Authorization Filters** (`IAuthorizationFilter`)
- **Run First** in the pipeline
- Used to determine whether the task should execute
- Can inspect message properties, headers, or claims
- **Short-circuits** by setting `context.Result`

**Use Cases:**
- Validate message claims or permissions
- Check message subject or correlation ID
- Enforce security policies

**Example:**
```csharp
[RequireClaim("role", "order-processor")]
public class ProcessOrderTask : BaseTask { }
```

### 2. **Resource Filters** (`IResourceFilter`)
- Run after authorization and **surround** the rest of the pipeline
- Have both `OnResourceExecutingAsync` and `OnResourceExecutedAsync` methods
- **Short-circuits** by setting `context.Result` in `OnResourceExecuting`

**Use Cases:**
- Performance measurement and timing
- Resource management (connections, caching)
- Rate limiting
- Distributed tracing setup

**Example:**
```csharp
[MeasureExecutionTime]
[RateLimit(maxCallsPerMinute: 100)]
public class ProcessOrderTask : BaseTask { }
```

### 3. **Action Filters** (`IActionFilter`)
- Run before and after **task execution**
- Have both `OnActionExecutingAsync` and `OnActionExecutedAsync` methods
- **Short-circuits** by setting `context.Result` in `OnActionExecuting`

**Use Cases:**
- Input validation
- Logging task execution
- Modifying or inspecting message content
- Adding telemetry

**Example:**
```csharp
[ValidateJsonBody]
[LogAction]
[AddTelemetry]
public class ProcessOrderTask : BaseTask { }
```

### 4. **Result Filters** (`IResultFilter`)
- Run before and after **result execution** (Complete/Abandon/DeadLetter/Defer)
- Have both `OnResultExecutingAsync` and `OnResultExecutedAsync` methods
- Can **modify the result** or cancel result execution

**Use Cases:**
- Convert Abandon to DeadLetter after max retries
- Add custom logging for specific result types
- Modify message properties before completion

**Example:**
```csharp
[MaxRetry(maxRetries: 3)]
[LogResult]
public class ProcessOrderTask : BaseTask { }
```

### 5. **Exception Filters** (`IExceptionFilter`)
- Run when an **unhandled exception** occurs anywhere in the pipeline
- Can handle specific exception types
- Mark exception as handled by setting `context.ExceptionHandled = true`
- Optionally provide a result with `context.Result`

**Use Cases:**
- Handle specific exception types (retry transient errors)
- Dead-letter messages on fatal errors
- Log exceptions with custom formatting
- Convert exceptions to specific results

**Example:**
```csharp
[HandleException(typeof(InvalidOperationException), deadLetter: true)]
[LogException]
[DeadLetterOnError] // Catch-all
public class ProcessOrderTask : BaseTask { }
```

## Filter Execution Order

```
1. Authorization Filters ──────────────────────────────────┐
   └─> Can short-circuit                                    │
                                                             │
2. Resource Filters (Executing) ───────────────────────────┤
   └─> Can short-circuit                                    │
                                                             │
3. Action Filters (Executing) ─────────────────────────────┤  Exception
   └─> Can short-circuit                                    │  Filters
                                                             │  (if exception)
4. ★ TASK EXECUTION ★ ────────────────────────────────────┤
                                                             │
5. Action Filters (Executed) ──────────────────────────────┤  Run in
   └─> Run in reverse order                                 │  reverse
                                                             │  order
6. Result Filters (Executing) ─────────────────────────────┤
   └─> Can modify result or cancel                          │
                                                             │
7. ★ RESULT EXECUTION ★ (Complete/Abandon/etc.) ──────────┤
                                                             │
8. Result Filters (Executed) ──────────────────────────────┤
   └─> Run in reverse order                                 │
                                                             │
9. Resource Filters (Executed) ────────────────────────────┘
   └─> Run in reverse order
```

## Filter Ordering

Control the order filters execute with the `Order` property:

```csharp
[LogAction] // Order = 0 (default)
[CustomFilter(Order = 10)] // Runs after LogAction
[AnotherFilter(Order = -10)] // Runs before LogAction
public class MyTask : BaseTask { }
```

- Lower `Order` values execute **first**
- Default order is `0`
- Negative values run early, positive values run late
- Within the same order, filters run in the order they're declared

## Result Types

Tasks must return an `ITaskResult` that determines message disposition:

| Result Type | Behavior | Use Case |
|------------|----------|----------|
| `CompleteResult` | Removes message from queue | Successful processing |
| `AbandonResult` | Returns message to queue | Transient error, retry later |
| `DeadLetterResult` | Moves to dead-letter queue | Permanent error, poison message |
| `DeferResult` | Defers message for later | Requires manual approval, rate limiting |

**Example:**
```csharp
public override async Task<ITaskResult> ExecuteAsync(ProcessMessageEventArgs eventArgs)
{
    var order = ParseOrder(eventArgs.Message.Body);
    
    if (order.Amount < 0)
        return new DeadLetterResult("InvalidAmount", "Negative amount not allowed");
    
    if (order.Amount > 10000)
        return new DeferResult(); // Needs approval
    
    await ProcessOrderAsync(order);
    return new CompleteResult();
}
```

## Short-Circuiting

Filters can stop the pipeline from continuing:

### Authorization/Action/Resource Filters
Set `context.Result` to skip remaining pipeline stages:

```csharp
public Task OnAuthorizationAsync(AuthorizationFilterContext context)
{
    if (!IsAuthorized(context.MessageEventArgs.Message))
    {
        // Short-circuit: Skip task execution entirely
        context.Result = new DeadLetterResult("Unauthorized", "Missing required claim");
    }
    return Task.CompletedTask;
}
```

### Result Filters
Set `context.Cancel = true` to prevent result execution:

```csharp
public Task OnResultExecutingAsync(ResultExecutingContext context)
{
    if (ShouldPreventCompletion(context))
    {
        context.Cancel = true; // Don't execute the result
    }
    return Task.CompletedTask;
}
```

## Context Items Dictionary

Share data between filter stages using `context.Items`:

```csharp
public class TimingFilter : FilterAttribute, IResourceFilter
{
    public Task OnResourceExecutingAsync(ResourceExecutingContext context)
    {
        context.Items["StartTime"] = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    public Task OnResourceExecutedAsync(ResourceExecutedContext context)
    {
        if (context.Items.TryGetValue("StartTime", out var start))
        {
            var duration = DateTime.UtcNow - (DateTime)start;
            // Log duration
        }
        return Task.CompletedTask;
    }
}
```

## Complete Example

```csharp
[Task("ProcessPayment")]
// 1. Authorization
[ValidateMessageSubject]
[RequireClaim("role", "payment-processor")]

// 2. Resource (surrounds everything)
[MeasureExecutionTime]
[RateLimit(maxCallsPerMinute: 50)]

// 3. Action (surrounds task)
[ValidateJsonBody]
[LogAction]
[AddTelemetry]

// 4. Result
[MaxRetry(maxRetries: 3)]
[LogResult]

// 5. Exception handling
[LogException]
[HandleException(typeof(HttpRequestException), deadLetter: false)] // Retry on network errors
[HandleException(typeof(InvalidOperationException), deadLetter: true)] // Dead-letter on logic errors
[DeadLetterOnError] // Catch-all
public class ProcessPaymentTask : BaseTask
{
    public override async Task<ITaskResult> ExecuteAsync(ProcessMessageEventArgs eventArgs)
    {
        var payment = JsonSerializer.Deserialize<Payment>(eventArgs.Message.Body);
        
        await ProcessPaymentAsync(payment);
        
        return new CompleteResult();
    }
}
```

## Creating Custom Filters

### Authorization Filter
```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class CustomAuthAttribute : FilterAttribute, IAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // Check authorization
        if (!IsAuthorized(context.MessageEventArgs.Message))
        {
            context.Result = new DeadLetterResult("Unauthorized", "...");
        }
        return Task.CompletedTask;
    }
}
```

### Resource Filter (Surrounds Execution)
```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class CustomResourceAttribute : FilterAttribute, IResourceFilter
{
    public Task OnResourceExecutingAsync(ResourceExecutingContext context)
    {
        // Before everything
        context.Items["Key"] = "Value";
        return Task.CompletedTask;
    }

    public Task OnResourceExecutedAsync(ResourceExecutedContext context)
    {
        // After everything
        return Task.CompletedTask;
    }
}
```

### Action Filter
```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class CustomActionAttribute : FilterAttribute, IActionFilter
{
    public Task OnActionExecutingAsync(ActionExecutingContext context)
    {
        // Before task execution
        return Task.CompletedTask;
    }

    public Task OnActionExecutedAsync(ActionExecutedContext context)
    {
        // After task execution
        // Can inspect context.Result and context.Exception
        return Task.CompletedTask;
    }
}
```

### Result Filter
```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class CustomResultAttribute : FilterAttribute, IResultFilter
{
    public Task OnResultExecutingAsync(ResultExecutingContext context)
    {
        // Before result execution
        // Can modify context.Result or set context.Cancel = true
        return Task.CompletedTask;
    }

    public Task OnResultExecutedAsync(ResultExecutedContext context)
    {
        // After result execution
        return Task.CompletedTask;
    }
}
```

### Exception Filter
```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class CustomExceptionAttribute : FilterAttribute, IExceptionFilter
{
    public Task OnExceptionAsync(ExceptionContext context)
    {
        // Handle exception
        if (context.Exception is MyException)
        {
            context.ExceptionHandled = true;
            context.Result = new AbandonResult();
        }
        return Task.CompletedTask;
    }
}
```

## Dependency Injection

All filters have access to `context.ServiceProvider` for dependency injection:

```csharp
public Task OnActionExecutingAsync(ActionExecutingContext context)
{
    var logger = context.ServiceProvider.GetRequiredService<ILogger<MyFilter>>();
    var myService = context.ServiceProvider.GetRequiredService<IMyService>();
    
    // Use services
    logger.LogInformation("Filter executing");
    myService.DoSomething();
    
    return Task.CompletedTask;
}
```

## Best Practices

1. **Keep filters focused**: Each filter should do one thing well
2. **Use appropriate filter type**: Choose the filter that matches your use case
3. **Order matters**: Use `Order` property when filter sequence is important
4. **Share state via Items**: Use `context.Items` to pass data between filter stages
5. **Handle exceptions gracefully**: Use exception filters to provide meaningful error handling
6. **Log appropriately**: Add logging in filters for observability
7. **Test filters independently**: Filters should be unit-testable
8. **Document short-circuit behavior**: Make it clear when filters will short-circuit

## Testing

Test filters independently by creating contexts manually:

```csharp
[Fact]
public async Task RequireClaimAttribute_MissingClaim_ShortCircuits()
{
    // Arrange
    var filter = new RequireClaimAttribute("role", "admin");
    var message = CreateTestMessage(); // Without claim
    var context = new AuthorizationFilterContext(message, serviceProvider, task);
    
    // Act
    await filter.OnAuthorizationAsync(context);
    
    // Assert
    Assert.NotNull(context.Result);
    Assert.IsType<DeadLetterResult>(context.Result);
}
```

## Comparison to ASP.NET Core MVC

| MVC Filter | Task Filter | Same Behavior? |
|------------|-------------|----------------|
| `IAuthorizationFilter` | `IAuthorizationFilter` | ✅ Yes |
| `IResourceFilter` | `IResourceFilter` | ✅ Yes |
| `IActionFilter` | `IActionFilter` | ✅ Yes |
| `IResultFilter` | `IResultFilter` | ✅ Yes |
| `IExceptionFilter` | `IExceptionFilter` | ✅ Yes |
| `Order` property | `Order` property | ✅ Yes |
| Short-circuiting | Short-circuiting | ✅ Yes |
| `HttpContext.Items` | `FilterContext.Items` | ✅ Yes |

The filter pipeline has been implemented to match ASP.NET Core MVC behavior as closely as possible, making it familiar to .NET developers.
