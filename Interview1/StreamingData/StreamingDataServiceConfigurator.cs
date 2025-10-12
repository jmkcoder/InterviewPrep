using Interview1.StreamingData.Repositories;
using Interview1.StreamingData.Usecases;

namespace Interview1.StreamingData;

public static class StreamingDataServiceConfigurator
{
    public static IServiceCollection AddStreamingDataServices(this IServiceCollection services)
    {
        // Register repositories
        services.AddScoped<IStreamingDataRepository, StreamingDataRepository>();

        // Register use cases
        services.AddScoped<IGetStreamingDataUsecase, GetStreamingDataUsecase>();

        return services;
    }
}
