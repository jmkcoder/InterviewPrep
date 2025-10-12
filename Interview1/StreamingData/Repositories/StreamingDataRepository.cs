using Interview1.StreamingData.Dtos;
using System.Runtime.CompilerServices;

namespace Interview1.StreamingData.Repositories;

public class StreamingDataRepository : IStreamingDataRepository
{
    public async IAsyncEnumerable<StreamingDataDto> GetStreamingDataAsync(
        int count, 
        int delayMs, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (int i = 1; i <= count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }

            // Simulate data generation/retrieval with delay
            await Task.Delay(delayMs, cancellationToken);

            yield return new StreamingDataDto
            {
                Id = i,
                Message = $"Streaming message #{i}",
                Timestamp = DateTime.UtcNow,
                Value = Random.Shared.NextDouble() * 100
            };
        }
    }
}
