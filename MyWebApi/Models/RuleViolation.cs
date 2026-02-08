namespace MyWebApi.Models;

public class RuleViolation
{
    public int Turn { get; set; }
    public string Player { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = "warning"; // critical, warning, info
    public string Description { get; set; } = string.Empty;
    public CardIdentifier? Card { get; set; }  // Only populated for misplays
}

public class CardIdentifier
{
    public int DeckIndex { get; set; }  // Unique card identifier
    public int SuitIndex { get; set; }  // 0-4 for R/Y/G/B/P
    public int Rank { get; set; }       // 1-5
}

public class GameAnalysisResponse
{
    public GameExport Game { get; set; } = new();
    public List<RuleViolation> Violations { get; set; } = new();
    public AnalysisSummary Summary { get; set; } = new();
    public bool VariantSupported { get; set; } = true;
    public string? VariantName { get; set; }
    public List<GameState> States { get; set; } = new();
}

public class AnalysisSummary
{
    public int TotalViolations { get; set; }
    public Dictionary<string, int> BySeverity { get; set; } = new();
    public Dictionary<string, int> ByType { get; set; } = new();
}

public static class ViolationType
{
    // Phase 1
    public const string Misplay = "Misplay";
    public const string BadDiscard5 = "BadDiscard5";
    public const string BadDiscardCritical = "BadDiscardCritical";
    public const string IllegalDiscard = "IllegalDiscard";

    // Phase 2
    public const string GoodTouchViolation = "GoodTouchViolation";
    public const string MCVPViolation = "MCVPViolation";
    public const string MissedSave = "MissedSave";
    public const string MisreadSave = "MisreadSave";

    // Phase 3 - Prompt/Finesse
    public const string MissedPrompt = "MissedPrompt";
    public const string MissedFinesse = "MissedFinesse";
    public const string BrokenFinesse = "BrokenFinesse";

    // Level 2 - Intermediate Conventions
    public const string FiveStall = "FiveStall";
    public const string StompedFinesse = "StompedFinesse";
    public const string WrongPrompt = "WrongPrompt";
    public const string DoubleDiscardAvoidance = "DoubleDiscardAvoidance";

    // Blame Attribution
    public const string BadPlayClue = "BadPlayClue";  // Clue caused a misplay (blame clue-giver)

    // Level 3 - Advanced Conventions
    public const string FixClue = "FixClue";
    public const string SarcasticDiscard = "SarcasticDiscard";
    public const string WrongOnesOrder = "WrongOnesOrder";
    public const string MisplayCostViolation = "MisplayCostViolation";
    public const string InformationLock = "InformationLock";
}

public static class Severity
{
    public const string Critical = "critical";
    public const string Warning = "warning";
    public const string Info = "info";
}

public record PendingFinesse
{
    public int SetupTurn { get; init; }
    public int ClueGiverIndex { get; init; }
    public int TargetPlayerIndex { get; init; }
    public int FinessePlayerIndex { get; init; }
    public int NeededSuitIndex { get; init; }
    public int NeededRank { get; init; }
    public bool IsResolved { get; set; }
    public bool WasStomped { get; set; }
    public int ResponseDeadlineActionIndex { get; init; } = -1;
}

public record ClueHistoryEntry
{
    public int Turn { get; init; }
    public int ClueGiverIndex { get; init; }
    public int TargetPlayerIndex { get; init; }
    public ActionType ClueType { get; init; }
    public int ClueValue { get; init; }
    public List<int> TouchedDeckIndices { get; init; } = new();
    public int? FocusDeckIndex { get; init; }
}
