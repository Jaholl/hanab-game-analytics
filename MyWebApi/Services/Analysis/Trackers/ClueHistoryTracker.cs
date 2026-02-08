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
        var newlyTouched = new List<(int deckIndex, int handIndex)>();

        for (int i = 0; i < targetHand.Count; i++)
        {
            var card = targetHand[i];
            bool touched = (action.Type == ActionType.ColorClue && card.SuitIndex == action.Value) ||
                           (action.Type == ActionType.RankClue && card.Rank == action.Value);

            if (touched)
            {
                touchedIndices.Add(card.DeckIndex);
                if (!card.HasAnyClue)
                    newlyTouched.Add((card.DeckIndex, i));
            }
        }

        // H-Group 4-step focus: if chop is among newly touched, focus = chop
        int? focusIndex = null;
        if (newlyTouched.Count > 0)
        {
            // Determine chop index (first unclued card in hand)
            int? chopIndex = null;
            for (int i = 0; i < targetHand.Count; i++)
            {
                if (!targetHand[i].HasAnyClue) { chopIndex = i; break; }
            }

            if (chopIndex.HasValue && newlyTouched.Any(t => t.handIndex == chopIndex.Value))
            {
                // Case 3: chop is among newly touched → focus on chop
                focusIndex = newlyTouched.First(t => t.handIndex == chopIndex.Value).deckIndex;
            }
            else
            {
                // Case 2 (one new) or Case 4 (multiple new, no chop) → leftmost = highest hand index
                focusIndex = newlyTouched.OrderByDescending(t => t.handIndex).First().deckIndex;
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
