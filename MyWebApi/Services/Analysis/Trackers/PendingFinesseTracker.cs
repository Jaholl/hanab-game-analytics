using MyWebApi.Models;
using MyWebApi.Services.Analysis.Helpers;

namespace MyWebApi.Services.Analysis.Trackers;

/// <summary>
/// Tracks pending finesses across turns for cross-turn finesse resolution.
/// </summary>
public class PendingFinesseTracker : IStateTracker
{
    private static readonly HashSet<ActionType> _applicableTypes = new()
    {
        ActionType.Play,
        ActionType.Discard,
        ActionType.ColorClue,
        ActionType.RankClue
    };

    public IReadOnlySet<ActionType> ApplicableActionTypes => _applicableTypes;

    public void Track(AnalysisContext context)
    {
        // Resolve pending finesses when a player plays the needed card
        if (context.Action.Type == ActionType.Play)
        {
            var hand = context.StateBefore.Hands[context.CurrentPlayerIndex];
            var card = hand.FirstOrDefault(c => c.DeckIndex == context.Action.Target);
            if (card != null)
            {
                foreach (var finesse in context.PendingFinesses)
                {
                    if (!finesse.IsResolved &&
                        finesse.FinessePlayerIndex == context.CurrentPlayerIndex &&
                        finesse.NeededSuitIndex == card.SuitIndex &&
                        finesse.NeededRank == card.Rank)
                    {
                        finesse.IsResolved = true;
                    }
                }
            }
        }
    }
}
