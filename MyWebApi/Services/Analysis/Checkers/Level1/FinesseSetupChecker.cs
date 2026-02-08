using MyWebApi.Models;
using MyWebApi.Services.Analysis.Helpers;

namespace MyWebApi.Services.Analysis.Checkers.Level1;

/// <summary>
/// Detects finesse setups that aren't followed by the expected blind play.
/// </summary>
public class FinesseSetupChecker : IViolationChecker
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
        var game = context.Game;
        var targetPlayer = action.Target;
        if (targetPlayer < 0 || targetPlayer >= state.Hands.Count) return;

        var targetHand = state.Hands[targetPlayer];
        var numPlayers = game.Players.Count;

        // Find newly touched cards
        var touchedCards = new List<(CardInHand card, int index)>();
        for (int i = 0; i < targetHand.Count; i++)
        {
            var card = targetHand[i];
            bool touched = (action.Type == ActionType.ColorClue && card.SuitIndex == action.Value) ||
                           (action.Type == ActionType.RankClue && card.Rank == action.Value);

            if (touched && !card.HasAnyClue)
                touchedCards.Add((card, i));
        }

        if (touchedCards.Count == 0) return;

        // H-Group 4-step focus: if chop is among newly touched, focus = chop
        int? chopIndex = null;
        for (int i = 0; i < targetHand.Count; i++)
        {
            if (!targetHand[i].HasAnyClue) { chopIndex = i; break; }
        }

        CardInHand focusCard;
        if (chopIndex.HasValue && touchedCards.Any(t => t.index == chopIndex.Value))
        {
            // Case 3: chop is among newly touched → focus on chop
            focusCard = touchedCards.First(t => t.index == chopIndex.Value).card;
        }
        else
        {
            // Case 2 (one new) or Case 4 (multiple new, no chop) → leftmost = highest hand index
            focusCard = touchedCards.OrderByDescending(t => t.index).First().card;
        }

        if (AnalysisHelpers.IsCardPlayable(focusCard, state)) return;

        var neededRank = state.PlayStacks[focusCard.SuitIndex] + 1;
        if (focusCard.Rank != neededRank + 1) return;

        // Scan ALL other players (not just before target) to detect reverse finesses
        int finessePlayerIndex = -1;
        for (int offset = 1; offset < numPlayers; offset++)
        {
            int checkPlayer = (context.CurrentPlayerIndex + offset) % numPlayers;
            if (checkPlayer == targetPlayer) continue;

            var checkHand = state.Hands[checkPlayer];
            if (checkHand.Count == 0) continue;

            var finessePosIndex = AnalysisHelpers.GetFinessePositionIndex(checkHand);
            if (!finessePosIndex.HasValue) continue;

            var finessePos = checkHand[finessePosIndex.Value];
            if (finessePos.SuitIndex == focusCard.SuitIndex && finessePos.Rank == neededRank)
            {
                finessePlayerIndex = checkPlayer;
                break;
            }
        }

        if (finessePlayerIndex == -1) return;

        // Find the finesse player's next turn (may not be the immediately next action)
        int finesseActionIndex = -1;
        for (int i = context.ActionIndex + 1; i < game.Actions.Count; i++)
        {
            if (i % numPlayers == finessePlayerIndex)
            {
                finesseActionIndex = i;
                break;
            }
        }
        if (finesseActionIndex == -1) return;

        // Register the pending finesse with a deadline. PendingFinesseTracker will
        // resolve it when the finesse player's turn is processed, emitting MissedFinesse
        // if they don't respond. This allows StompedFinesseChecker to detect stomps
        // on intervening turns before the deadline.
        context.PendingFinesses.Add(new PendingFinesse
        {
            SetupTurn = context.Turn,
            ClueGiverIndex = context.CurrentPlayerIndex,
            TargetPlayerIndex = targetPlayer,
            FinessePlayerIndex = finessePlayerIndex,
            NeededSuitIndex = focusCard.SuitIndex,
            NeededRank = neededRank,
            ResponseDeadlineActionIndex = finesseActionIndex
        });
    }
}
