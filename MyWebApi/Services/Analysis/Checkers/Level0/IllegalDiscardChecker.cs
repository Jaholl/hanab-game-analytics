using MyWebApi.Models;

namespace MyWebApi.Services.Analysis.Checkers.Level0;

/// <summary>
/// Detects illegal discards (discarding at 8 clue tokens).
/// </summary>
public class IllegalDiscardChecker : IViolationChecker
{
    public ConventionLevel Level => ConventionLevel.Level0_Basic;

    private static readonly HashSet<ActionType> _applicableTypes = new() { ActionType.Discard };
    public IReadOnlySet<ActionType> ApplicableActionTypes => _applicableTypes;

    public void Check(AnalysisContext context)
    {
        if (context.StateBefore.ClueTokens >= 8)
        {
            context.Violations.Add(new RuleViolation
            {
                Turn = context.Turn,
                Player = context.CurrentPlayer,
                Type = ViolationType.IllegalDiscard,
                Severity = Severity.Critical,
                Description = "Discarded at 8 clue tokens - must clue or play instead"
            });
        }
    }
}
