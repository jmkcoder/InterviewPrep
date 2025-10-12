using Interview1.StreamingData.Dtos;

namespace Interview1.StreamingData.Repositories;

public interface IStreamingDataRepository
{
    IAsyncEnumerable<StreamingDataDto> GetStreamingDataAsync(int count, int delayMs, CancellationToken cancellationToken = default);
}
