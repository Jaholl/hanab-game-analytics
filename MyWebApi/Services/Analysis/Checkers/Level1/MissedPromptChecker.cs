using MyWebApi.Models;
using MyWebApi.Services.Analysis.Helpers;

namespace MyWebApi.Services.Analysis.Checkers.Level1;

/// <summary>
/// Detects when a player discards while holding a playable clued card (missed prompt).
/// </summary>
public class MissedPromptChecker : IViolationChecker
{
    public ConventionLevel Level => ConventionLevel.Level1_Beginner;

    private static readonly HashSet<ActionType> _applicableTypes = new() { ActionType.Discard };
    public IReadOnlySet<ActionType> ApplicableActionTypes => _applicableTypes;

    public void Check(AnalysisContext context)
    {
        var hand = context.StateBefore.Hands[context.CurrentPlayerIndex];

        foreach (var card in hand)
        {
            if (card.HasAnyClue && AnalysisHelpers.IsCardPlayable(card, context.StateBefore))
            {
                var suitName = AnalysisHelpers.GetSuitName(card.SuitIndex);
                context.Violations.Add(new RuleViolation
                {
                    Turn = context.Turn,
                    Player = context.CurrentPlayer,
                    Type = ViolationType.MissedPrompt,
                    Severity = Severity.Warning,
                    Description = $"Discarded but had a playable clued card ({suitName} {card.Rank})"
                });
                return; // Only report once per turn
            }
        }
    }
}
