using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace Interview1.RateLimiting
{
    /// <summary>
    /// Controller demonstrating different rate limiting strategies
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class RateLimitingController : ControllerBase
    {
        /// <summary>
        /// No rate limiting - inherits global rate limiter
        /// (30 req/min for anonymous, 100 req/min for authenticated)
        /// </summary>
        [HttpGet("global")]
        public IActionResult GlobalRateLimit()
        {
            return Ok(new
            {
                message = "This endpoint uses the global rate limiter",
                limit = "30 requests/minute for anonymous users, 100 for authenticated",
                timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Fixed Window Rate Limiting - 10 requests per minute
        /// Simple and most commonly used strategy
        /// </summary>
        [HttpGet("fixed")]
        [EnableRateLimiting("fixed")]
        public IActionResult FixedWindowRateLimit()
        {
            return Ok(new
            {
                message = "Fixed window rate limiting applied",
                limit = "10 requests per minute (with 2 queued requests)",
                strategy = "Fixed Window",
                description = "Resets at fixed intervals. Can have burst at window boundaries.",
                timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Sliding Window Rate Limiting - 20 requests per minute
        /// More accurate than fixed window, prevents burst at boundaries
        /// </summary>
        [HttpGet("sliding")]
        [EnableRateLimiting("sliding")]
        public IActionResult SlidingWindowRateLimit()
        {
            return Ok(new
            {
                message = "Sliding window rate limiting applied",
                limit = "20 requests per minute (4 segments of 15 seconds each)",
                strategy = "Sliding Window",
                description = "Divides time window into segments for smoother rate limiting.",
                timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Token Bucket Rate Limiting - 100 tokens, replenishes 20 every 10 seconds
        /// Good for allowing controlled bursts
        /// </summary>
        [HttpGet("token")]
        [EnableRateLimiting("token")]
        public IActionResult TokenBucketRateLimit()
        {
            return Ok(new
            {
                message = "Token bucket rate limiting applied",
                limit = "100 token capacity, +20 tokens every 10 seconds",
                strategy = "Token Bucket",
                description = "Allows burst traffic while maintaining average rate. Best for APIs with variable load.",
                timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Concurrency Limiter - Maximum 5 concurrent requests
        /// Good for protecting resources from overload
        /// </summary>
        [HttpGet("concurrency")]
        [EnableRateLimiting("concurrency")]
        public async Task<IActionResult> ConcurrencyRateLimit()
        {
            // Simulate some work
            await Task.Delay(2000);

            return Ok(new
            {
                message = "Concurrency rate limiting applied",
                limit = "Maximum 5 concurrent requests (10 queued)",
                strategy = "Concurrency Limiter",
                description = "Limits simultaneous requests. Useful for DB connections, external API calls.",
                timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Strict Rate Limiting - 3 requests per 30 seconds, no queueing
        /// Immediately rejects when limit is reached
        /// </summary>
        [HttpGet("strict")]
        [EnableRateLimiting("strict")]
        public IActionResult StrictRateLimit()
        {
            return Ok(new
            {
                message = "Strict rate limiting applied",
                limit = "3 requests per 30 seconds (no queueing)",
                strategy = "Fixed Window (Strict)",
                description = "Immediately returns 429 when limit exceeded. No request queueing.",
                timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Disabled Rate Limiting - Bypasses all rate limiters
        /// Use for health checks, webhooks, etc.
        /// </summary>
        [HttpGet("disabled")]
        [DisableRateLimiting]
        public IActionResult DisabledRateLimit()
        {
            return Ok(new
            {
                message = "Rate limiting is disabled for this endpoint",
                description = "Use [DisableRateLimiting] for health checks, metrics, or trusted endpoints.",
                timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Per-User Rate Limiting - 5 requests per minute per user
        /// Demonstrates partitioned rate limiting based on user ID
        /// </summary>
        [HttpGet("per-user")]
        public IActionResult PerUserRateLimit()
        {
            // This demonstrates per-user rate limiting using custom partition
            var userId = HttpContext.Request.Headers["X-User-Id"].FirstOrDefault() ?? "anonymous";

            var rateLimitPartition = RateLimitPartition.GetFixedWindowLimiter(
                userId,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });

            return Ok(new
            {
                message = "Per-user rate limiting applied",
                userId = userId,
                limit = "5 requests per minute per user",
                strategy = "Partitioned Fixed Window",
                description = "Different rate limit per user. Send X-User-Id header to test.",
                timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Get rate limit status - shows remaining requests
        /// </summary>
        [HttpGet("status")]
        public IActionResult GetRateLimitStatus()
        {
            // Note: In real implementation, you'd need to access the rate limiter state
            // This is a simplified example
            return Ok(new
            {
                message = "Rate limit status",
                headers = new
                {
                    description = "Check response headers for rate limit info:",
                    headers = new[]
                    {
                        "X-RateLimit-Limit - Total requests allowed",
                        "X-RateLimit-Remaining - Requests remaining",
                        "X-RateLimit-Reset - Time when limit resets",
                        "Retry-After - Seconds to wait (when 429 response)"
                    }
                },
                timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Combined endpoint - demonstrates using custom policy on entire controller
        /// </summary>
        [HttpPost("action")]
        [EnableRateLimiting("sliding")]
        public IActionResult PerformAction([FromBody] ActionRequest request)
        {
            return Ok(new
            {
                message = "Action performed successfully",
                actionType = request.ActionType,
                rateLimit = "Sliding window: 20 requests per minute",
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Sample request model
    /// </summary>
    public class ActionRequest
    {
        public string ActionType { get; set; } = string.Empty;
        public Dictionary<string, string>? Parameters { get; set; }
    }
}
