using Interview1.StreamingData.Dtos;
using Interview1.StreamingData.Repositories;
using System.Runtime.CompilerServices;

namespace Interview1.StreamingData.Usecases;

public class GetStreamingDataUsecase : IGetStreamingDataUsecase
{
    private readonly IStreamingDataRepository _repository;

    public GetStreamingDataUsecase(IStreamingDataRepository repository)
    {
        _repository = repository;
    }

    public async IAsyncEnumerable<StreamingDataDto> ExecuteAsync(
        int count, 
        int delayMs, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var data in _repository.GetStreamingDataAsync(count, delayMs, cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }

            yield return data;
        }
    }
}
