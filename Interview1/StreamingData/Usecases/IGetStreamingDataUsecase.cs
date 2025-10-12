using Interview1.StreamingData.Dtos;

namespace Interview1.StreamingData.Usecases;

public interface IGetStreamingDataUsecase
{
    IAsyncEnumerable<StreamingDataDto> ExecuteAsync(int count, int delayMs, CancellationToken cancellationToken = default);
}
