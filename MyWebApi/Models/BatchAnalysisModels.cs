namespace MyWebApi.Models;

public class GameCriticalSummary
{
    public int GameId { get; set; }
    public string DateTime { get; set; } = string.Empty;
    public int CriticalCount { get; set; }
    public int Score { get; set; }
    public List<string> Players { get; set; } = new();
}

public class BatchCriticalMistakesResponse
{
    public string Player { get; set; } = string.Empty;
    public List<GameCriticalSummary> Games { get; set; } = new();
}
