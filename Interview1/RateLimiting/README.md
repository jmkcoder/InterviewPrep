# Rate Limiting in ASP.NET Core

This project demonstrates comprehensive rate limiting strategies for REST APIs.

## 📚 What is Rate Limiting?

Rate limiting controls how many requests a client can make to your API within a specific time window. It's essential for:
- **Preventing abuse** - Protect against DoS attacks
- **Fair usage** - Ensure all users get equal access
- **Cost control** - Limit expensive operations
- **Resource protection** - Prevent database/service overload

## 🎯 Rate Limiting Strategies Implemented

### 1. **Fixed Window** (`/api/ratelimiting/fixed`)
- **How it works**: Allows X requests per time window (e.g., 10 requests per minute)
- **Pros**: Simple to understand and implement
- **Cons**: Can have burst traffic at window boundaries
- **Use case**: General API rate limiting

```
Timeline: |----60s----|----60s----|
Requests: [10 requests] [10 requests]
Problem:  [9 at 59s][11 at 1s] = 20 requests in 2 seconds!
```

### 2. **Sliding Window** (`/api/ratelimiting/sliding`)
- **How it works**: Divides time window into segments for smoother limiting
- **Pros**: More accurate than fixed window, prevents boundary bursts
- **Cons**: Slightly more complex, higher memory usage
- **Use case**: Critical APIs where smoothness matters

```
Timeline: |--15s--|--15s--|--15s--|--15s--| (4 segments)
Requests:    5       5       5       5     = 20 total per minute
```

### 3. **Token Bucket** (`/api/ratelimiting/token`)
- **How it works**: Tokens replenish over time, allows controlled bursts
- **Pros**: Flexible, handles variable traffic patterns well
- **Cons**: More complex to configure
- **Use case**: APIs with bursty traffic (upload, batch operations)

```
Bucket: [100 tokens] → consume 1 per request
Refill: +20 tokens every 10 seconds
Allows burst of 100, sustained rate of 120/minute
```

### 4. **Concurrency Limiter** (`/api/ratelimiting/concurrency`)
- **How it works**: Limits simultaneous active requests
- **Pros**: Protects resources from overload
- **Cons**: Can block long-running requests
- **Use case**: Database queries, external API calls, file processing

```
Max 5 concurrent requests
Request 6 waits until one of 1-5 completes
```

### 5. **Per-User/IP Partitioning** (`/api/ratelimiting/per-user`)
- **How it works**: Different rate limits per user/IP/tenant
- **Pros**: Fairness, prevents one user from affecting others
- **Cons**: Higher memory usage
- **Use case**: Multi-tenant applications, user tiers

## 🚀 Testing the Endpoints

### Using PowerShell

```powershell
# Test fixed window rate limit (10 requests/minute)
1..15 | ForEach-Object {
    $response = Invoke-WebRequest -Uri "http://localhost:5000/api/ratelimiting/fixed" -Method Get
    Write-Host "Request $_`: $($response.StatusCode)"
}

# Test with rate limit headers
$response = Invoke-WebRequest -Uri "http://localhost:5000/api/ratelimiting/fixed" -Method Get
$response.Headers.'X-RateLimit-Limit'
$response.Headers.'X-RateLimit-Remaining'
$response.Headers.'X-RateLimit-Reset'

# Test per-user rate limiting with custom header
$headers = @{ "X-User-Id" = "user123" }
Invoke-WebRequest -Uri "http://localhost:5000/api/ratelimiting/per-user" -Headers $headers
```

### Using curl

```bash
# Test strict rate limit (3 requests/30s)
for i in {1..5}; do
  curl -i http://localhost:5000/api/ratelimiting/strict
  echo "Request $i completed"
done

# Test with headers
curl -i -H "X-User-Id: user123" http://localhost:5000/api/ratelimiting/per-user

# Test concurrency (run in parallel)
for i in {1..10}; do
  curl http://localhost:5000/api/ratelimiting/concurrency &
done
wait
```

## 📊 Response Headers

When rate limiting is active, you'll see these headers:

```
X-RateLimit-Limit: 10              # Total requests allowed
X-RateLimit-Remaining: 5           # Requests remaining in window
X-RateLimit-Reset: 2024-01-15T10:30:00Z  # When limit resets
Retry-After: 45                    # Seconds to wait (on 429 response)
```

## ❌ 429 Too Many Requests Response

```json
{
  "error": "Too many requests",
  "message": "Rate limit exceeded. Please try again later.",
  "retryAfter": 45
}
```

## 🔧 Configuration in Program.cs

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("fixed", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 2;  // Queue 2 requests when limit reached
    });
    
    // Global limiter with partitioning
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
        context => {
            var userId = context.User.Identity?.Name ?? "anonymous";
            return RateLimitPartition.GetFixedWindowLimiter(userId, _ => 
                new FixedWindowRateLimiterOptions {
                    PermitLimit = 30,
                    Window = TimeSpan.FromMinutes(1)
                });
        });
});

app.UseRateLimiter(); // MUST be before UseAuthorization()
```

## 🎓 Interview Questions & Answers

### Q1: Why use Sliding Window over Fixed Window?
**A**: Sliding Window prevents burst traffic at window boundaries. With Fixed Window, a client could make 10 requests at 11:59:59 and another 10 at 12:00:01, effectively getting 20 requests in 2 seconds.

### Q2: When would you use Token Bucket?
**A**: When you want to allow bursts while maintaining average rate. Example: File upload API where users might upload 5 files at once (burst), but average 20/hour.

### Q3: What's the difference between Rate Limiting and Throttling?
**A**: 
- **Rate Limiting**: Hard limit, returns 429 when exceeded
- **Throttling**: Slows down requests instead of blocking (queuing)

### Q4: How do you handle rate limiting in distributed systems?
**A**: Use distributed cache (Redis) to store counters:
```csharp
// Use Redis for distributed rate limiting
var redis = ConnectionMultiplexer.Connect("localhost");
var db = redis.GetDatabase();
var key = $"ratelimit:{userId}";
var count = db.StringIncrement(key);
if (count == 1) db.KeyExpire(key, TimeSpan.FromMinutes(1));
```

### Q5: Should rate limiting be at API Gateway or application level?
**A**: 
- **API Gateway**: Protects all services, simpler management
- **Application**: More granular control, custom business rules
- **Both**: Gateway for DDoS, application for business logic

### Q6: How do you test rate limiting?
**A**: 
1. Unit tests with mocked time
2. Integration tests with fast time windows
3. Load tests with tools like k6, JMeter
4. Monitor `429` response rates in production

## 🔐 Best Practices

1. **Always return proper headers** - `X-RateLimit-*` and `Retry-After`
2. **Use appropriate strategy** - Fixed for simplicity, Sliding for accuracy, Token for bursts
3. **Exempt health checks** - Use `[DisableRateLimiting]`
4. **Different limits for tiers** - Free: 100/hour, Pro: 1000/hour, Enterprise: unlimited
5. **Document limits** - In API docs, error messages, and developer portal
6. **Monitor and alert** - Track `429` rates, adjust limits based on data
7. **Graceful degradation** - Queue requests instead of immediate rejection when possible
8. **Consider distributed scenarios** - Use Redis for multi-instance applications

## 🛠️ Custom Implementation

See `CustomRateLimitAttribute.cs` for a custom attribute-based implementation using:
- `ConcurrentDictionary` for in-memory storage
- Action filters for intercepting requests
- Per-IP or per-endpoint rate limiting

```csharp
[CustomRateLimit(5, 30)]  // 5 requests per 30 seconds
public IActionResult MyEndpoint() { ... }
```

## 📈 Advanced Scenarios

### Per-Tenant Rate Limiting
```csharp
var tenantId = context.Request.Headers["X-Tenant-Id"];
return RateLimitPartition.GetFixedWindowLimiter(tenantId, ...);
```

### Rate Limiting by Endpoint
```csharp
var endpoint = context.Request.Path.ToString();
// Different limits for /expensive vs /cheap endpoints
```

### Adaptive Rate Limiting
```csharp
// Reduce limit when system is under load
var cpuUsage = GetCpuUsage();
var limit = cpuUsage > 80 ? 50 : 100;
```

## 🔗 Related Concepts

- **Circuit Breaker**: Prevents cascading failures (use Polly)
- **Bulkhead**: Isolates resources to prevent total system failure
- **Retry with Backoff**: Client-side handling of 429 responses
- **API Gateway**: Centralized rate limiting (Ocelot, YARP, Azure APIM)

## 📚 Further Reading

- [ASP.NET Core Rate Limiting Docs](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit)
- [RFC 6585 - HTTP Status Code 429](https://tools.ietf.org/html/rfc6585)
- [IETF Draft - RateLimit Headers](https://datatracker.ietf.org/doc/html/draft-ietf-httpapi-ratelimit-headers)

## 🧪 Endpoints Summary

| Endpoint | Strategy | Limit | Description |
|----------|----------|-------|-------------|
| `/api/ratelimiting/global` | Fixed Window | 30/min (anon), 100/min (auth) | Global limiter |
| `/api/ratelimiting/fixed` | Fixed Window | 10/min | Basic rate limiting |
| `/api/ratelimiting/sliding` | Sliding Window | 20/min | Smooth rate limiting |
| `/api/ratelimiting/token` | Token Bucket | 100 tokens, +20/10s | Burst-friendly |
| `/api/ratelimiting/concurrency` | Concurrency | 5 concurrent | Resource protection |
| `/api/ratelimiting/strict` | Fixed Window | 3/30s | No queueing |
| `/api/ratelimiting/disabled` | None | Unlimited | Exempt from limits |
| `/api/customratelimit/custom-limit` | Custom Filter | 5/30s | Custom attribute |
| `/api/customratelimit/ip-limit` | Custom IP-based | 3/min | Per-IP limiting |
