using MyWebApi.Models;
using MyWebApi.Services.Analysis.Helpers;

namespace MyWebApi.Services.Analysis.Checkers.Level1;

/// <summary>
/// Minimum Clue Value Principle - clue must touch at least one new card.
/// Exception (Level 2+): "Tempo clues" that re-touch playable cards are valid.
/// </summary>
public class MCVPChecker : IViolationChecker
{
    public ConventionLevel Level => ConventionLevel.Level1_Beginner;

    private static readonly HashSet<ActionType> _applicableTypes = new()
    {
        ActionType.ColorClue,
        ActionType.RankClue
    };
    public IReadOnlySet<ActionType> ApplicableActionTypes => _applicableTypes;

    public void Check(AnalysisContext context)
    {
        var action = context.Action;
        var state = context.StateBefore;
        var targetPlayer = action.Target;
        if (targetPlayer < 0 || targetPlayer >= state.Hands.Count) return;

        var targetHand = state.Hands[targetPlayer];
        var newCardsTouched = 0;
        var touchedPlayableCards = 0;

        foreach (var card in targetHand)
        {
            bool isTouched = (action.Type == ActionType.ColorClue && card.SuitIndex == action.Value) ||
                             (action.Type == ActionType.RankClue && card.Rank == action.Value);

            if (isTouched)
            {
                if (!card.HasAnyClue)
                    newCardsTouched++;
                else if (AnalysisHelpers.IsCardPlayable(card, state))
                    touchedPlayableCards++;
            }
        }

        if (newCardsTouched > 0) return;

        bool isTempoClueLevel = context.Options.Level >= ConventionLevel.Level2_Intermediate;
        if (isTempoClueLevel && touchedPlayableCards > 0) return;

        var clueType = action.Type == ActionType.ColorClue
            ? AnalysisHelpers.GetSuitName(action.Value)
            : action.Value.ToString();

        context.Violations.Add(new RuleViolation
        {
            Turn = context.Turn,
            Player = context.CurrentPlayer,
            Type = ViolationType.MCVPViolation,
            Severity = Severity.Warning,
            Description = $"Clue ({clueType}) only touched already-clued cards - no new information given"
        });
    }
}
