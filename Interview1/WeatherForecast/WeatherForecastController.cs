using Interview1.WeatherForecast.Usecases;
using Microsoft.AspNetCore.Mvc;

namespace Interview1.WeatherForecast
{
    [ApiController]
    [Route("api/[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        [HttpGet]
        public IEnumerable<WeatherForecast> Get(IGetWeatherForecastUsecase getWeatherForecastUsecase)
        {
            return getWeatherForecastUsecase.Execute();
        }
    }
}
