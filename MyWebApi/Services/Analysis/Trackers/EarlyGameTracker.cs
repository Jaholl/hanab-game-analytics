using MyWebApi.Models;
using MyWebApi.Services.Analysis.Helpers;

namespace MyWebApi.Services.Analysis.Trackers;

/// <summary>
/// Tracks whether the game is still in the Early Game phase.
/// The Early Game ends when any player makes a deliberate chop discard.
/// </summary>
public class EarlyGameTracker : IStateTracker
{
    private static readonly HashSet<ActionType> _applicableTypes = new()
    {
        ActionType.Discard
    };

    public IReadOnlySet<ActionType> ApplicableActionTypes => _applicableTypes;

    public void Track(AnalysisContext context)
    {
        // Once early game has ended, it stays ended
        if (!context.IsEarlyGame) return;

        var action = context.Action;
        var state = context.StateBefore;
        var hand = state.Hands[context.CurrentPlayerIndex];

        // Find the chop position (oldest unclued card)
        var chopIndex = AnalysisHelpers.GetChopIndex(hand);
        if (!chopIndex.HasValue) return;

        // Find the position of the discarded card in the hand
        var discardedIndex = hand.FindIndex(c => c.DeckIndex == action.Target);
        if (discardedIndex < 0) return;

        // If the discarded card was the chop card, the early game is over —
        // UNLESS the discard was forced (0 clue tokens and no playable cards).
        // A forced discard is not a deliberate exit from early-game convention.
        if (discardedIndex == chopIndex.Value)
        {
            bool forcedDiscard = state.ClueTokens == 0 &&
                !hand.Any(c => AnalysisHelpers.IsCardPlayable(c, state));
            if (!forcedDiscard)
                context.IsEarlyGame = false;
        }
    }
}
