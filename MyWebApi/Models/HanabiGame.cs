using System.Text.Json.Serialization;

namespace MyWebApi.Models;

public class HanabiHistoryResponse
{
    [JsonPropertyName("total_rows")]
    public int TotalRows { get; set; }

    [JsonPropertyName("rows")]
    public List<HanabiGame> Rows { get; set; } = new();
}

public class HanabiGame
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("num_players")]
    public int NumPlayers { get; set; }

    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("variant")]
    public int Variant { get; set; }

    [JsonPropertyName("users")]
    public string Users { get; set; } = string.Empty;

    [JsonPropertyName("datetime")]
    public string DateTime { get; set; } = string.Empty;

    [JsonPropertyName("seed")]
    public string Seed { get; set; } = string.Empty;

    [JsonPropertyName("other_scores")]
    public int OtherScores { get; set; }
}
