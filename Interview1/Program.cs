using Asp.Versioning;
using Interview1.Database;
using Interview1.Employee;
using Interview1.Employee.GraphQL;
using Interview1.StreamingData;
using Interview1.WeatherForecast;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add Azure Service Bus client
builder.AddAzureServiceBusClient("messaging");

builder.Services.AddHttpContextAccessor();

// Register all database services (context, repositories, caching)
builder.Services.AddDatabaseServices();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Add Rate Limiting with different strategies
builder.Services.AddRateLimiter(options =>
{
    // 1. Fixed Window Limiter - Most common, simple to understand
    // Allows X requests per time window
    options.AddFixedWindowLimiter("fixed", fixedOptions =>
    {
        fixedOptions.PermitLimit = 10; // 10 requests
        fixedOptions.Window = TimeSpan.FromMinutes(1); // per minute
        fixedOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        fixedOptions.QueueLimit = 2; // Queue up to 2 requests when limit is reached
    });

    // 2. Sliding Window Limiter - More accurate than fixed window
    // Prevents burst at window boundaries
    options.AddSlidingWindowLimiter("sliding", slidingOptions =>
    {
        slidingOptions.PermitLimit = 20; // 20 requests
        slidingOptions.Window = TimeSpan.FromMinutes(1); // per minute
        slidingOptions.SegmentsPerWindow = 4; // Divides window into 4 segments (15s each)
        slidingOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        slidingOptions.QueueLimit = 2;
    });

    // 3. Token Bucket Limiter - Good for burst scenarios
    // Tokens replenish over time, allows controlled bursts
    options.AddTokenBucketLimiter("token", tokenOptions =>
    {
        tokenOptions.TokenLimit = 100; // Bucket capacity
        tokenOptions.ReplenishmentPeriod = TimeSpan.FromSeconds(10); // Replenish every 10s
        tokenOptions.TokensPerPeriod = 20; // Add 20 tokens per period
        tokenOptions.AutoReplenishment = true;
        tokenOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        tokenOptions.QueueLimit = 5;
    });

    // 4. Concurrency Limiter - Limits concurrent requests
    // Good for protecting resources from overload
    options.AddConcurrencyLimiter("concurrency", concurrencyOptions =>
    {
        concurrencyOptions.PermitLimit = 5; // Max 5 concurrent requests
        concurrencyOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        concurrencyOptions.QueueLimit = 10; // Queue up to 10 requests
    });

    // 5. Per-User Rate Limiting (using partition key)
    options.AddFixedWindowLimiter("per-user", perUserOptions =>
    {
        perUserOptions.PermitLimit = 5;
        perUserOptions.Window = TimeSpan.FromMinutes(1);
        perUserOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        perUserOptions.QueueLimit = 0; // No queueing for per-user
    });

    // 6. Strict rate limiter - No queueing, immediate rejection
    options.AddFixedWindowLimiter("strict", strictOptions =>
    {
        strictOptions.PermitLimit = 3;
        strictOptions.Window = TimeSpan.FromSeconds(30);
        strictOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        strictOptions.QueueLimit = 0; // Reject immediately when limit reached
    });

    // Global fallback when rate limit is exceeded
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = retryAfter.TotalSeconds.ToString();
            
            await context.HttpContext.Response.WriteAsJsonAsync(new
            {
                error = "Too many requests",
                message = "Rate limit exceeded. Please try again later.",
                retryAfter = retryAfter.TotalSeconds
            }, cancellationToken);
        }
        else
        {
            await context.HttpContext.Response.WriteAsJsonAsync(new
            {
                error = "Too many requests",
                message = "Rate limit exceeded. Please try again later."
            }, cancellationToken);
        }
    };

    // Global rate limiter - applies to all endpoints by default
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        // You can create different partitions based on various criteria
        var userId = context.User.Identity?.Name ?? context.Request.Headers["X-User-Id"].ToString();
        
        if (string.IsNullOrEmpty(userId))
        {
            // Anonymous users - strict limiting
            return RateLimitPartition.GetFixedWindowLimiter("anonymous", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
        }

        // Authenticated users - more generous limits
        return RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 100,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 2
        });
    });
});

// Add API versioning
builder.Services.AddApiVersioning(options =>
{
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ApiVersionReader = new HeaderApiVersionReader("x-api-version");
    options.ReportApiVersions = true;
});

// Add services to the container.

// Register weather forecast related services
builder.Services.AddWeatherForecastServices();

// Register employee related services
builder.Services.AddEmployeeServices();

// Register streaming data related services
builder.Services.AddStreamingDataServices();

// Add GraphQL services
builder.Services
    .AddGraphQLServer()
    .AddQueryType<EmployeeQueries>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddHealthChecks();

var app = builder.Build();

// Initialize database infrastructure
app.UseDatabase();

// Configure the HTTP request pipeline.

app.UseExceptionHandler();

app.UseHttpsRedirection();

// Use rate limiting middleware (MUST be before UseAuthorization)
app.UseRateLimiter();

app.UseAuthorization();

app.MapGraphQL("/graphql");

app.MapControllers();

app.MapHealthChecks("/health");

app.Run();
