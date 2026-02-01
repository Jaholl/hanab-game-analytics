using MyWebApi.Models;

namespace MyWebApi.Services;

public interface IWeatherService
{
    IEnumerable<WeatherForecast> GetForecast(int days = 5);
}
