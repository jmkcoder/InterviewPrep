using Interview1.WeatherForecast.Usecases.V2;

namespace Interview1.WeatherForecast.Usecases
{
    public class WeatherForecastUsecaseProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IServiceProvider _serviceProvider;

        public WeatherForecastUsecaseProvider(
            IHttpContextAccessor httpContextAccessor, 
            IServiceProvider serviceProvider)
        {
            _httpContextAccessor = httpContextAccessor;
            _serviceProvider = serviceProvider;
        }

        public IGetWeatherForecastUsecase GetUsecase()
        {
            var version = _httpContextAccessor.HttpContext?.GetRequestedApiVersion()?.MajorVersion ?? 1;

            return version switch
            {
                1 => _serviceProvider.GetRequiredService<GetWeatherForecastUsecase>(),
                2 => _serviceProvider.GetRequiredService<GetWeatherForecastV2Usecase>(),
                _ => throw new NotSupportedException($"API version {version} is not supported")
            };
        }
    }
}
