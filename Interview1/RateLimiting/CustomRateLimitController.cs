using Microsoft.AspNetCore.Mvc;

namespace Interview1.RateLimiting
{
    /// <summary>
    /// Controller demonstrating custom rate limit attributes
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class CustomRateLimitController : ControllerBase
    {
        /// <summary>
        /// Endpoint with custom rate limiting - 5 requests per 30 seconds
        /// </summary>
        [HttpGet("custom-limit")]
        [CustomRateLimit(5, 30)]
        public IActionResult CustomLimitEndpoint()
        {
            return Ok(new
            {
                message = "Custom rate limit applied via attribute",
                limit = "5 requests per 30 seconds",
                description = "Uses custom ActionFilter for rate limiting",
                timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// IP-based rate limiting - 3 requests per minute per IP
        /// </summary>
        [HttpGet("ip-limit")]
        [RateLimitByIp(3, 60)]
        public IActionResult IpLimitEndpoint()
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            return Ok(new
            {
                message = "IP-based rate limiting applied",
                yourIp = ip,
                limit = "3 requests per minute per IP address",
                description = "Different IPs have separate rate limits",
                timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Combined with built-in rate limiter
        /// This endpoint has both custom attribute AND built-in rate limiter
        /// </summary>
        [HttpGet("combined")]
        [CustomRateLimit(10, 60)]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("fixed")]
        public IActionResult CombinedLimiting()
        {
            return Ok(new
            {
                message = "Combined rate limiting",
                limits = new[]
                {
                    "Custom attribute: 10 requests per minute",
                    "Built-in middleware: 10 requests per minute"
                },
                description = "Both rate limiters must be satisfied",
                timestamp = DateTime.UtcNow
            });
        }
    }
}
