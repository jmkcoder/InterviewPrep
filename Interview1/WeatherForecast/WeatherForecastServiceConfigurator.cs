using Interview1.WeatherForecast.Usecases;
using Interview1.WeatherForecast.Usecases.V2;

namespace Interview1.WeatherForecast
{
    public static class WeatherForecastServiceConfigurator
    {
        public static void AddWeatherForecastServices(this IServiceCollection services)
        {
            services.AddScoped<GetWeatherForecastUsecase>();
            services.AddScoped<GetWeatherForecastV2Usecase>();
            services.AddScoped<WeatherForecastUsecaseProvider>();

            services.AddScoped<IGetWeatherForecastUsecase>(
                provider => provider
                    .GetRequiredService<WeatherForecastUsecaseProvider>()
                    .GetUsecase());
        }
    }
}
