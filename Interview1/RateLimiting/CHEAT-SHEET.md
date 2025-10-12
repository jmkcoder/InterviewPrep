# Rate Limiting Implementation - Interview Cheat Sheet

## Quick Reference

### 1. Four Main Strategies

| Strategy | Use Case | Pros | Cons |
|----------|----------|------|------|
| **Fixed Window** | General APIs | Simple, low memory | Burst at boundaries |
| **Sliding Window** | Critical APIs | Smooth, accurate | Higher memory |
| **Token Bucket** | File upload, batch | Allows bursts | Complex config |
| **Concurrency** | DB/External APIs | Resource protection | Blocks long requests |

### 2. Configuration Template

```csharp
builder.Services.AddRateLimiter(options =>
{
    // Fixed Window: 10 req/min
    options.AddFixedWindowLimiter("policy-name", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 2; // Queue when limit reached
    });
});

app.UseRateLimiter(); // MUST be before UseAuthorization()
```

### 3. Usage on Controllers

```csharp
[EnableRateLimiting("policy-name")]  // Enable
[DisableRateLimiting]                // Disable
```

### 4. Response Headers

```
X-RateLimit-Limit: 10           # Total allowed
X-RateLimit-Remaining: 7        # Remaining
X-RateLimit-Reset: <timestamp>  # Reset time
Retry-After: 45                 # Wait time (on 429)
```

## Interview Questions - Fast Answers

### Q: Why rate limit?
**A**: Prevent abuse, ensure fair usage, protect resources, control costs

### Q: Fixed vs Sliding Window?
**A**: Fixed is simple but allows burst at boundaries. Sliding prevents this by using segments.

### Q: When to use Token Bucket?
**A**: When you want to allow bursts while maintaining average rate (e.g., file uploads)

### Q: Rate Limiting vs Throttling?
**A**: 
- Rate Limiting = Hard reject (429)
- Throttling = Slow down (queue)

### Q: Distributed systems?
**A**: Use Redis/distributed cache for shared counters across instances

```csharp
// Redis example
var redis = ConnectionMultiplexer.Connect("localhost");
var key = $"ratelimit:{userId}";
var count = redis.GetDatabase().StringIncrement(key);
if (count == 1) redis.GetDatabase().KeyExpire(key, TimeSpan.FromMinutes(1));
if (count > 100) return 429;
```

### Q: Where to implement?
**A**:
- API Gateway: DDoS protection, simple rules
- Application: Business logic, per-feature limits
- Both: Gateway for infrastructure, app for business

### Q: Testing strategy?
**A**:
1. Unit tests with mocked time
2. Integration tests with short windows
3. Load tests (k6, JMeter)
4. Monitor 429 rates in production

### Q: Best practices?
**A**:
1. Return proper headers (X-RateLimit-*)
2. Use 429 status code
3. Document limits
4. Exempt health checks
5. Different limits per tier
6. Monitor and adjust

## Code Snippets for Interviews

### Custom Implementation (Without Middleware)
```csharp
public class RateLimitAttribute : ActionFilterAttribute
{
    private static ConcurrentDictionary<string, List<DateTime>> _log = new();
    private readonly int _limit;
    private readonly TimeSpan _window;

    public RateLimitAttribute(int limit, int windowSeconds)
    {
        _limit = limit;
        _window = TimeSpan.FromSeconds(windowSeconds);
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var key = context.HttpContext.Connection.RemoteIpAddress?.ToString();
        var now = DateTime.UtcNow;
        var requests = _log.GetOrAdd(key, _ => new List<DateTime>());

        lock (requests)
        {
            requests.RemoveAll(r => now - r > _window);
            if (requests.Count >= _limit)
            {
                context.Result = new StatusCodeResult(429);
                return;
            }
            requests.Add(now);
        }
    }
}
```

### Per-User Partitioning
```csharp
options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
    context => {
        var userId = context.User.Identity?.Name ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(userId, _ =>
            new FixedWindowRateLimiterOptions {
                PermitLimit = userId == "anonymous" ? 10 : 100,
                Window = TimeSpan.FromMinutes(1)
            });
    });
```

## Demo Commands

```powershell
# Quick test - should see 10 OK, then 429s
1..15 | % { Invoke-WebRequest http://localhost:5000/api/ratelimiting/fixed }

# Check headers
(Invoke-WebRequest http://localhost:5000/api/ratelimiting/fixed).Headers

# Per-user test
$h1 = @{"X-User-Id"="user1"}
$h2 = @{"X-User-Id"="user2"}
1..7 | % { Invoke-WebRequest http://localhost:5000/api/ratelimiting/per-user -Headers $h1 }
1..7 | % { Invoke-WebRequest http://localhost:5000/api/ratelimiting/per-user -Headers $h2 }
```

## Common Pitfalls

1. ❌ **Not ordering middleware correctly**
   ```csharp
   app.UseAuthorization();
   app.UseRateLimiter(); // WRONG! Must be BEFORE
   ```

2. ❌ **Not handling 429 on client**
   ```csharp
   // Client should retry with exponential backoff
   if (response.StatusCode == 429) {
       var retryAfter = response.Headers["Retry-After"];
       await Task.Delay(TimeSpan.FromSeconds(int.Parse(retryAfter)));
   }
   ```

3. ❌ **Using same limit for all operations**
   - Expensive operations (reports): 10/hour
   - Regular operations (GET): 100/minute
   - Cheap operations (health): unlimited

4. ❌ **Not considering distributed scenarios**
   - In-memory cache only works for single instance
   - Use Redis for multi-instance deployments

5. ❌ **Not documenting limits**
   - Add to API docs
   - Show in error messages
   - Display in developer portal

## Performance Considerations

| Strategy | Memory | CPU | Accuracy | Best For |
|----------|--------|-----|----------|----------|
| Fixed Window | Low | Low | Medium | High throughput |
| Sliding Window | Medium | Medium | High | Fairness |
| Token Bucket | Medium | Low | High | Variable load |
| Concurrency | Low | Low | High | Resource protection |

## Production Monitoring

```csharp
// Log rate limit events
options.OnRejected = async (context, cancellationToken) =>
{
    logger.LogWarning("Rate limit exceeded for {IP} on {Endpoint}", 
        context.HttpContext.Connection.RemoteIpAddress,
        context.HttpContext.Request.Path);
    
    // Metrics
    metrics.IncrementCounter("rate_limit_rejections");
    
    // Response
    context.HttpContext.Response.StatusCode = 429;
    await context.HttpContext.Response.WriteAsJsonAsync(new {
        error = "Too many requests",
        retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry) 
            ? retry.TotalSeconds : 60
    });
};
```

## Related Patterns

- **Circuit Breaker**: Stop calling failing services (Polly)
- **Retry with Backoff**: Client-side resilience
- **Bulkhead**: Isolate resources to prevent cascading failures
- **API Gateway**: Centralized rate limiting (Azure APIM, Kong, Tyk)

## Resources to Mention in Interviews

- [RFC 6585](https://tools.ietf.org/html/rfc6585) - HTTP 429 Status Code
- [IETF Draft - RateLimit Headers](https://datatracker.ietf.org/doc/html/draft-ietf-httpapi-ratelimit-headers)
- ASP.NET Core Rate Limiting Middleware (built-in since .NET 7)
- Polly for resilience patterns
- Redis for distributed rate limiting

## Project Structure
```
Interview1/
├── RateLimiting/
│   ├── RateLimitingController.cs          # Built-in middleware examples
│   ├── CustomRateLimitController.cs       # Custom attribute examples
│   ├── CustomRateLimitAttribute.cs        # Custom implementation
│   ├── README.md                          # Full documentation
│   └── TEST-SCENARIOS.md                  # Test scripts
└── Program.cs                              # Configuration
```

## Key Takeaways

1. ✅ Rate limiting is essential for production APIs
2. ✅ Choose strategy based on use case (Fixed for simplicity, Sliding for accuracy, Token for bursts)
3. ✅ Always return proper headers and status codes
4. ✅ Consider distributed scenarios (use Redis)
5. ✅ Monitor and adjust limits based on usage patterns
6. ✅ Document limits clearly for API consumers
7. ✅ Test thoroughly before production
8. ✅ Combine with other resilience patterns (circuit breaker, retry)

---
**Time to implement**: 2-4 hours
**Interview prep time**: 30 minutes
**Confidence level after demo**: 90%+
