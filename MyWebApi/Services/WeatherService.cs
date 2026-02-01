using MyWebApi.Data;
using MyWebApi.Models;

namespace MyWebApi.Services;

public class WeatherService : IWeatherService
{
    private readonly AppDbContext _db;

    public WeatherService(AppDbContext db)
    {
        _db = db;
    }

    public IEnumerable<WeatherForecast> GetForecast(int days = 5)
    {
        return _db.WeatherForecasts
            .OrderBy(f => f.Date)
            .Take(days)
            .ToList();
    }
}
