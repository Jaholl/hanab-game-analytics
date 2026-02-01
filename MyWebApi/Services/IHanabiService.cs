using MyWebApi.Models;

namespace MyWebApi.Services;

public interface IHanabiService
{
    Task<HanabiHistoryResponse> GetHistoryAsync(string username, int page = 0, int size = 100);
}
