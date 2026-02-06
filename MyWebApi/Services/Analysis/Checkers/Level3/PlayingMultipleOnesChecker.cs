using MyWebApi.Models;
using MyWebApi.Services.Analysis.Helpers;

namespace MyWebApi.Services.Analysis.Checkers.Level3;

/// <summary>
/// Detects when a player plays 1s in the wrong order.
/// H-Group convention: Play 1s oldest-to-newest when multiple are clued.
/// </summary>
public class PlayingMultipleOnesChecker : IViolationChecker
{
    public ConventionLevel Level => ConventionLevel.Level3_Advanced;

    private static readonly HashSet<ActionType> _applicableTypes = new() { ActionType.Play };
    public IReadOnlySet<ActionType> ApplicableActionTypes => _applicableTypes;

    public void Check(AnalysisContext context)
    {
        var hand = context.StateBefore.Hands[context.CurrentPlayerIndex];
        var deckIndex = context.Action.Target;

        var card = hand.FirstOrDefault(c => c.DeckIndex == deckIndex);
        if (card == null) return;

        // Only applies when playing a 1
        if (card.Rank != 1) return;

        // Only applies if the card was clued as rank 1 (player knows it's a 1)
        if (!card.ClueRanks[0]) return;

        // Find all rank-1-clued 1s in hand
        var cluedOnes = new List<(CardInHand card, int handIndex)>();
        for (int i = 0; i < hand.Count; i++)
        {
            var c = hand[i];
            if (c.Rank == 1 && c.ClueRanks[0] && AnalysisHelpers.IsCardPlayable(c, context.StateBefore))
            {
                cluedOnes.Add((c, i));
            }
        }

        // Need at least 2 clued playable 1s for ordering to matter
        if (cluedOnes.Count < 2) return;

        // Find the played card's hand index
        var playedHandIndex = hand.FindIndex(c => c.DeckIndex == deckIndex);
        if (playedHandIndex < 0) return;

        // The oldest clued 1 should be played first (lowest hand index)
        var oldestCluedOneIndex = cluedOnes.Min(c => c.handIndex);

        if (playedHandIndex != oldestCluedOneIndex)
        {
            var suitName = AnalysisHelpers.GetSuitName(card.SuitIndex);
            context.Violations.Add(new RuleViolation
            {
                Turn = context.Turn,
                Player = context.CurrentPlayer,
                Type = ViolationType.WrongOnesOrder,
                Severity = Severity.Warning,
                Description = $"Played {suitName} 1 from slot {playedHandIndex} but should play oldest clued 1 from slot {oldestCluedOneIndex} first"
            });
        }
    }
}
