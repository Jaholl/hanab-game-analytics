using MyWebApi.Models;

namespace MyWebApi.Services.Analysis;

/// <summary>
/// Shared state container passed to all checkers and trackers during analysis.
/// Provides access to game data, current action context, and accumulated state.
/// </summary>
public class AnalysisContext
{
    // Game-level data (immutable during analysis)
    public GameExport Game { get; init; } = new();
    public List<GameState> States { get; init; } = new();
    public AnalyzerOptions Options { get; init; } = new();

    // Per-action context (updated each iteration)
    public int ActionIndex { get; set; }
    public GameAction Action { get; set; } = new();
    public GameState StateBefore { get; set; } = new();
    public GameState StateAfter { get; set; } = new();
    public int CurrentPlayerIndex { get; set; }
    public string CurrentPlayer { get; set; } = string.Empty;
    public int Turn { get; set; }

    // Accumulated state from trackers
    public List<ClueHistoryEntry> ClueHistory { get; } = new();
    public List<PendingFinesse> PendingFinesses { get; } = new();
    public bool IsEarlyGame { get; set; } = true;

    // Collected violations
    public List<RuleViolation> Violations { get; } = new();
}
