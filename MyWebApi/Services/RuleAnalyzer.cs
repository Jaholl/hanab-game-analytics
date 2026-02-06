using MyWebApi.Models;
using MyWebApi.Services.Analysis;

namespace MyWebApi.Services;

/// <summary>
/// Thin wrapper that delegates to GameAnalysisOrchestrator.
/// Maintains backward compatibility with existing callers.
/// </summary>
public class RuleAnalyzer
{
    private readonly AnalyzerOptions _options;
    private readonly GameAnalysisOrchestrator _orchestrator;

    public RuleAnalyzer() : this(null) { }

    public RuleAnalyzer(AnalyzerOptions? options)
    {
        _options = options ?? new AnalyzerOptions();
        _orchestrator = new GameAnalysisOrchestrator();
    }

    public List<RuleViolation> AnalyzeGame(GameExport game, List<GameState> states)
    {
        return _orchestrator.AnalyzeGame(game, states, _options);
    }

    public AnalysisSummary CreateSummary(List<RuleViolation> violations)
    {
        return new AnalysisSummary
        {
            TotalViolations = violations.Count,
            BySeverity = violations.GroupBy(v => v.Severity)
                .ToDictionary(g => g.Key, g => g.Count()),
            ByType = violations.GroupBy(v => v.Type)
                .ToDictionary(g => g.Key, g => g.Count())
        };
    }
}
