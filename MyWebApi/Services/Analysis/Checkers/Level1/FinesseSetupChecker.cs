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

        var focusCard = touchedCards.OrderByDescending(t => t.index).First().card;

        if (AnalysisHelpers.IsCardPlayable(focusCard, state)) return;

        var neededRank = state.PlayStacks[focusCard.SuitIndex] + 1;
        if (focusCard.Rank != neededRank + 1) return;

        int finessePlayerIndex = -1;
        for (int offset = 1; offset < numPlayers; offset++)
        {
            int checkPlayer = (context.CurrentPlayerIndex + offset) % numPlayers;
            if (checkPlayer == targetPlayer) break;

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

        var finesseAction = game.Actions[finesseActionIndex];

        if (finesseAction.Type != ActionType.Play)
        {
            var suitName = AnalysisHelpers.GetSuitName(focusCard.SuitIndex);
            context.Violations.Add(new RuleViolation
            {
                Turn = context.Turn,
                Player = game.Players[finessePlayerIndex],
                Type = ViolationType.MissedFinesse,
                Severity = Severity.Info,
                Description = $"Possible finesse for {suitName} {neededRank} was set up but not followed"
            });
        }
    }
}
