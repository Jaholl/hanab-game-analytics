using MyWebApi.Models;
using MyWebApi.Services.Analysis.Helpers;

namespace MyWebApi.Services.Analysis.Checkers.Level3;

/// <summary>
/// Detects missed sarcastic discard opportunities.
/// H-Group convention: When you have a clued card that duplicates a clued card
/// in another player's hand, you should discard yours to signal certainty.
/// </summary>
public class SarcasticDiscardChecker : IViolationChecker
{
    public ConventionLevel Level => ConventionLevel.Level3_Advanced;

    private static readonly HashSet<ActionType> _applicableTypes = new() { ActionType.Discard };
    public IReadOnlySet<ActionType> ApplicableActionTypes => _applicableTypes;

    public void Check(AnalysisContext context)
    {
        var hand = context.StateBefore.Hands[context.CurrentPlayerIndex];
        var deckIndex = context.Action.Target;
        var discardedCard = hand.FirstOrDefault(c => c.DeckIndex == deckIndex);
        if (discardedCard == null) return;

        // Check if the player has any clued cards that are known duplicates
        // of clued cards in other players' hands
        foreach (var myCard in hand)
        {
            if (!myCard.HasAnyClue) continue;
            if (myCard.DeckIndex == deckIndex) continue; // Already being discarded

            // Check if this clued card matches a clued card in another player's hand
            for (int p = 0; p < context.StateBefore.Hands.Count; p++)
            {
                if (p == context.CurrentPlayerIndex) continue;

                foreach (var otherCard in context.StateBefore.Hands[p])
                {
                    if (otherCard.HasAnyClue &&
                        otherCard.SuitIndex == myCard.SuitIndex &&
                        otherCard.Rank == myCard.Rank)
                    {
                        // Found a known duplicate!
                        // The player should have sarcastic-discarded myCard
                        // instead of discarding from chop

                        // Only flag if the player discarded a different (non-duplicate) card
                        if (discardedCard.SuitIndex != myCard.SuitIndex ||
                            discardedCard.Rank != myCard.Rank)
                        {
                            var suitName = AnalysisHelpers.GetSuitName(myCard.SuitIndex);
                            context.Violations.Add(new RuleViolation
                            {
                                Turn = context.Turn,
                                Player = context.CurrentPlayer,
                                Type = ViolationType.SarcasticDiscard,
                                Severity = Severity.Warning,
                                Description = $"Should have sarcastic-discarded {suitName} {myCard.Rank} (duplicate of clued card in {context.Game.Players[p]}'s hand)"
                            });
                            return; // Only report once per turn
                        }
                    }
                }
            }
        }
    }
}
