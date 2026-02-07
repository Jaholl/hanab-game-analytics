namespace MyWebApi.Models;

public class PlaystyleResponse
{
    public string Player { get; set; } = string.Empty;
    public int GamesAnalyzed { get; set; }
    public int TotalActions { get; set; }
    public int Plays { get; set; }
    public int Discards { get; set; }
    public int ColorClues { get; set; }
    public int RankClues { get; set; }
    public int Misplays { get; set; }
    public int BadDiscards { get; set; }
    public int GoodTouchViolations { get; set; }
    public int MCVPViolations { get; set; }
    public int MissedSaves { get; set; }
    public int MissedPrompts { get; set; }
    public int MissedFinesses { get; set; }
    public PlaystyleRates Rates { get; set; } = new();
    public PlaystyleDimensions Dimensions { get; set; } = new();
}

public class PlaystyleRates
{
    public double PlayRate { get; set; }
    public double DiscardRate { get; set; }
    public double ClueRate { get; set; }
    public double ErrorRate { get; set; }
    public double BadClueRate { get; set; }
    public double MissedSavesPerGame { get; set; }
    public double MissedTechPerGame { get; set; }
    public double MisplayRate { get; set; }
}

public class PlaystyleDimensions
{
    public double Accuracy { get; set; }
    public double ClueQuality { get; set; }
    public double Teamwork { get; set; }
    public double Technique { get; set; }
    public double Boldness { get; set; }
    public double Efficiency { get; set; }
    public double DiscardFrequency { get; set; }
    public double MisreadSaves { get; set; }
}
