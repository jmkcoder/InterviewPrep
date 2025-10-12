using Interview1.StreamingData.Usecases;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Interview1.StreamingData;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public class StreamingDataController : ControllerBase
{
    private readonly IGetStreamingDataUsecase _getStreamingDataUsecase;

    public StreamingDataController(IGetStreamingDataUsecase getStreamingDataUsecase)
    {
        _getStreamingDataUsecase = getStreamingDataUsecase;
    }

    /// <summary>
    /// Get streaming data (NDJSON format - Newline Delimited JSON)
    /// </summary>
    /// <param name="count">Number of items to stream (default: 10)</param>
    /// <param name="delayMs">Delay in milliseconds between items (default: 1000ms)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Streaming NDJSON response</returns>
    [HttpGet("stream")]
    public async Task StreamDataAsync(
        [FromQuery] int count = 10,
        [FromQuery] int delayMs = 1000,
        CancellationToken cancellationToken = default)
    {
        Response.ContentType = "application/x-ndjson"; // or "text/event-stream" for SSE
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        await foreach (var data in _getStreamingDataUsecase.ExecuteAsync(count, delayMs, cancellationToken))
        {
            var json = JsonSerializer.Serialize(data);
            await Response.WriteAsync(json + "\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Get streaming data as Server-Sent Events (SSE)
    /// </summary>
    /// <param name="count">Number of items to stream (default: 10)</param>
    /// <param name="delayMs">Delay in milliseconds between items (default: 1000ms)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Streaming SSE response</returns>
    [HttpGet("stream-sse")]
    public async Task StreamDataSseAsync(
        [FromQuery] int count = 10,
        [FromQuery] int delayMs = 1000,
        CancellationToken cancellationToken = default)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        await foreach (var data in _getStreamingDataUsecase.ExecuteAsync(count, delayMs, cancellationToken))
        {
            var json = JsonSerializer.Serialize(data);
            await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Get all data as a standard JSON array (non-streaming for comparison)
    /// </summary>
    /// <param name="count">Number of items to return (default: 10)</param>
    /// <param name="delayMs">Delay in milliseconds between items (default: 1000ms)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>JSON array of all data</returns>
    [HttpGet("all")]
    public async Task<IActionResult> GetAllDataAsync(
        [FromQuery] int count = 10,
        [FromQuery] int delayMs = 1000,
        CancellationToken cancellationToken = default)
    {
        var results = new List<Dtos.StreamingDataDto>();
        
        await foreach (var data in _getStreamingDataUsecase.ExecuteAsync(count, delayMs, cancellationToken))
        {
            results.Add(data);
        }

        return Ok(results);
    }
}
