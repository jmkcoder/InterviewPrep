using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Collections.Concurrent;

namespace Interview1.RateLimiting
{
    /// <summary>
    /// Custom rate limiting attribute for fine-grained control
    /// This demonstrates how to implement rate limiting without using the built-in middleware
    /// Useful for understanding the concepts in interviews
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public class CustomRateLimitAttribute : ActionFilterAttribute
    {
        private static readonly ConcurrentDictionary<string, RateLimitEntry> _cache = new();
        
        public int Requests { get; set; }
        public int WindowSeconds { get; set; }
        public string KeyPrefix { get; set; } = "ratelimit";

        public CustomRateLimitAttribute(int requests = 10, int windowSeconds = 60)
        {
            Requests = requests;
            WindowSeconds = windowSeconds;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var key = GenerateKey(context.HttpContext);
            var now = DateTime.UtcNow;

            var entry = _cache.GetOrAdd(key, _ => new RateLimitEntry
            {
                WindowStart = now,
                RequestCount = 0
            });

            lock (entry.LockObject)
            {
                // Check if window has expired
                if ((now - entry.WindowStart).TotalSeconds >= WindowSeconds)
                {
                    // Reset window
                    entry.WindowStart = now;
                    entry.RequestCount = 0;
                }

                // Increment request count
                entry.RequestCount++;

                // Check if limit exceeded
                if (entry.RequestCount > Requests)
                {
                    var retryAfter = WindowSeconds - (int)(now - entry.WindowStart).TotalSeconds;
                    
                    context.HttpContext.Response.Headers["Retry-After"] = retryAfter.ToString();
                    context.HttpContext.Response.Headers["X-RateLimit-Limit"] = Requests.ToString();
                    context.HttpContext.Response.Headers["X-RateLimit-Remaining"] = "0";
                    context.HttpContext.Response.Headers["X-RateLimit-Reset"] = entry.WindowStart.AddSeconds(WindowSeconds).ToString("O");

                    context.Result = new ObjectResult(new
                    {
                        error = "Rate limit exceeded",
                        message = $"Maximum {Requests} requests per {WindowSeconds} seconds allowed",
                        retryAfter = retryAfter
                    })
                    {
                        StatusCode = StatusCodes.Status429TooManyRequests
                    };
                    
                    return;
                }

                // Add rate limit headers
                context.HttpContext.Response.Headers["X-RateLimit-Limit"] = Requests.ToString();
                context.HttpContext.Response.Headers["X-RateLimit-Remaining"] = (Requests - entry.RequestCount).ToString();
                context.HttpContext.Response.Headers["X-RateLimit-Reset"] = entry.WindowStart.AddSeconds(WindowSeconds).ToString("O");
            }

            base.OnActionExecuting(context);
        }

        private string GenerateKey(HttpContext context)
        {
            // Generate key based on IP address and endpoint
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var endpoint = context.Request.Path.ToString();
            
            // You can also include user ID if authenticated
            var userId = context.User.Identity?.Name;
            
            return $"{KeyPrefix}:{ip}:{endpoint}:{userId ?? "anonymous"}";
        }

        // Cleanup old entries periodically (in production, use background service)
        public static void CleanupOldEntries()
        {
            var now = DateTime.UtcNow;
            var keysToRemove = _cache
                .Where(kvp => (now - kvp.Value.WindowStart).TotalMinutes > 10)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _cache.TryRemove(key, out _);
            }
        }

        private class RateLimitEntry
        {
            public DateTime WindowStart { get; set; }
            public int RequestCount { get; set; }
            public object LockObject { get; } = new object();
        }
    }

    /// <summary>
    /// Attribute for IP-based rate limiting
    /// </summary>
    public class RateLimitByIpAttribute : ActionFilterAttribute
    {
        private static readonly ConcurrentDictionary<string, List<DateTime>> _requestLog = new();
        private readonly int _maxRequests;
        private readonly TimeSpan _timeWindow;

        public RateLimitByIpAttribute(int maxRequests = 10, int windowSeconds = 60)
        {
            _maxRequests = maxRequests;
            _timeWindow = TimeSpan.FromSeconds(windowSeconds);
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var ip = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var now = DateTime.UtcNow;

            var requests = _requestLog.GetOrAdd(ip, _ => new List<DateTime>());

            lock (requests)
            {
                // Remove old requests outside the time window
                requests.RemoveAll(r => now - r > _timeWindow);

                if (requests.Count >= _maxRequests)
                {
                    context.Result = new ObjectResult(new
                    {
                        error = "Rate limit exceeded",
                        message = $"Maximum {_maxRequests} requests per {_timeWindow.TotalSeconds} seconds from this IP"
                    })
                    {
                        StatusCode = StatusCodes.Status429TooManyRequests
                    };
                    return;
                }

                requests.Add(now);
            }

            base.OnActionExecuting(context);
        }
    }
}
