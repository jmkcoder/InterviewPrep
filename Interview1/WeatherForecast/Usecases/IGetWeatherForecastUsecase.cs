namespace Interview1.WeatherForecast.Usecases
{
    public interface IGetWeatherForecastUsecase
    {
        IEnumerable<WeatherForecast> Execute();
    }
}
