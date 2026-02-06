using MyWebApi.Models;
using MyWebApi.Services.Analysis.Helpers;

namespace MyWebApi.Services.Analysis.Checkers.Level0;

/// <summary>
/// Detects discarding critical cards (5s and last copies).
/// </summary>
public class BadDiscardChecker : IViolationChecker
{
    public ConventionLevel Level => ConventionLevel.Level0_Basic;

    private static readonly HashSet<ActionType> _applicableTypes = new() { ActionType.Discard };
    public IReadOnlySet<ActionType> ApplicableActionTypes => _applicableTypes;

    public void Check(AnalysisContext context)
    {
        var hand = context.StateBefore.Hands[context.CurrentPlayerIndex];
        var deckIndex = context.Action.Target;
        var card = hand.FirstOrDefault(c => c.DeckIndex == deckIndex);
        if (card == null) return;

        var suitName = AnalysisHelpers.GetSuitName(card.SuitIndex);

        // Check for discarding a 5
        if (card.Rank == 5)
        {
            if (context.StateBefore.PlayStacks[card.SuitIndex] < 5)
            {
                context.Violations.Add(new RuleViolation
                {
                    Turn = context.Turn,
                    Player = context.CurrentPlayer,
                    Type = ViolationType.BadDiscard5,
                    Severity = Severity.Critical,
                    Description = $"Discarded {suitName} 5 - fives are always critical!"
                });
            }
            return;
        }

        // Check for discarding a critical card (last remaining copy)
        if (AnalysisHelpers.IsCardCritical(card, context.StateBefore, context.Game))
        {
            if (context.StateBefore.PlayStacks[card.SuitIndex] < card.Rank &&
                !AnalysisHelpers.IsSuitDead(card.SuitIndex, card.Rank, context.StateBefore))
            {
                context.Violations.Add(new RuleViolation
                {
                    Turn = context.Turn,
                    Player = context.CurrentPlayer,
                    Type = ViolationType.BadDiscardCritical,
                    Severity = Severity.Critical,
                    Description = $"Discarded {suitName} {card.Rank} - it was the last copy!"
                });
            }
        }
    }
}
