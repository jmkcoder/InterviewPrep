namespace Interview1.WeatherForecast.Usecases.V2
{
    public class GetWeatherForecastV2Usecase : IGetWeatherForecastUsecase
    {
        public IEnumerable<WeatherForecast> Execute()
        {
            // New implementation for V2
            return Enumerable.Range(1, 10).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-30, 60), // Extended temperature range
                Summary = "V2 - " + (new[] { "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching" })[Random.Shared.Next(10)]
            });
        }
    }
}
