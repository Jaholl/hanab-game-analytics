using MyWebApi.Models;

namespace MyWebApi.Services.Analysis.Trackers;

/// <summary>
/// Tracks clue history for blame attribution.
/// </summary>
public class ClueHistoryTracker : IStateTracker
{
    private static readonly HashSet<ActionType> _applicableTypes = new()
    {
        ActionType.ColorClue,
        ActionType.RankClue
    };

    public IReadOnlySet<ActionType> ApplicableActionTypes => _applicableTypes;

    public void Track(AnalysisContext context)
    {
        var action = context.Action;
        var state = context.StateBefore;

        var targetPlayer = action.Target;
        if (targetPlayer < 0 || targetPlayer >= state.Hands.Count) return;

        var targetHand = state.Hands[targetPlayer];
        var touchedIndices = new List<int>();
        int? focusIndex = null;
        int? focusHandIndex = null;

        for (int i = 0; i < targetHand.Count; i++)
        {
            var card = targetHand[i];
            bool touched = (action.Type == ActionType.ColorClue && card.SuitIndex == action.Value) ||
                           (action.Type == ActionType.RankClue && card.Rank == action.Value);

            if (touched)
            {
                touchedIndices.Add(card.DeckIndex);
                if (!card.HasAnyClue)
                {
                    if (!focusIndex.HasValue || i > focusHandIndex)
                    {
                        focusIndex = card.DeckIndex;
                        focusHandIndex = i;
                    }
                }
            }
        }

        context.ClueHistory.Add(new ClueHistoryEntry
        {
            Turn = context.Turn,
            ClueGiverIndex = context.CurrentPlayerIndex,
            TargetPlayerIndex = targetPlayer,
            ClueType = (ActionType)action.Type,
            ClueValue = action.Value,
            TouchedDeckIndices = touchedIndices,
            FocusDeckIndex = focusIndex
        });
    }
}
