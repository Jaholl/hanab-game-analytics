namespace MyWebApi.Models;

public enum ConventionLevel
{
    Level0_Basic,
    Level1_Beginner,
    Level2_Intermediate,
    Level3_Advanced
}

public enum ActionType
{
    Play = 0,
    Discard = 1,
    ColorClue = 2,
    RankClue = 3
}

public class DeckCard
{
    public int SuitIndex { get; set; }
    public int Rank { get; set; }
}

public class GameAction
{
    public ActionType Type { get; set; }
    public int Target { get; set; }
    public int Value { get; set; }
}

public class GameOptions
{
    public string Variant { get; set; } = "No Variant";
}

public class GameExport
{
    public int Id { get; set; }
    public List<string> Players { get; set; } = new();
    public List<DeckCard> Deck { get; set; } = new();
    public List<GameAction> Actions { get; set; } = new();
    public GameOptions Options { get; set; } = new();
}
