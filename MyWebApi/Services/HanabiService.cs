using System.Text.Json;
using MyWebApi.Models;

namespace MyWebApi.Services;

public class HanabiService : IHanabiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HanabiService> _logger;

    public HanabiService(HttpClient httpClient, ILogger<HanabiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<HanabiHistoryResponse> GetHistoryAsync(string username, int page = 0, int size = 100)
    {
        var url = $"https://hanab.live/api/v1/history/{username}?page={page}&size={size}";
        _logger.LogInformation("Fetching Hanabi history from {Url}", url);

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<HanabiHistoryResponse>(json);

        return result ?? new HanabiHistoryResponse();
    }
}
