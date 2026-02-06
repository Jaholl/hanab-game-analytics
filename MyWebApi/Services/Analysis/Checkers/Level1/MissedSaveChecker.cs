using MyWebApi.Models;
using MyWebApi.Services.Analysis.Helpers;

namespace MyWebApi.Services.Analysis.Checkers.Level1;

/// <summary>
/// Detects missed save opportunities - taking an action when a teammate has
/// a critical card on chop that needs saving.
/// </summary>
public class MissedSaveChecker : IViolationChecker
{
    public ConventionLevel Level => ConventionLevel.Level1_Beginner;

    private static readonly HashSet<ActionType> _applicableTypes = new()
    {
        ActionType.Play,
        ActionType.Discard,
        ActionType.ColorClue,
        ActionType.RankClue
    };
    public IReadOnlySet<ActionType> ApplicableActionTypes => _applicableTypes;

    public void Check(AnalysisContext context)
    {
        var action = context.Action;
        var state = context.StateBefore;
        var game = context.Game;

        if (state.ClueTokens == 0) return;

        if (action.Type == ActionType.Play)
        {
            var hand = state.Hands[context.CurrentPlayerIndex];
            var playedCard = hand.FirstOrDefault(c => c.DeckIndex == action.Target);
            if (playedCard != null && playedCard.HasAnyClue) return;
        }

        var numPlayers = state.Hands.Count;
        for (int p = 0; p < numPlayers; p++)
        {
            if (p == context.CurrentPlayerIndex) continue;

            var chopCard = AnalysisHelpers.GetChopCard(state.Hands[p]);
            if (chopCard == null) continue;

            if ((action.Type == ActionType.ColorClue || action.Type == ActionType.RankClue) && action.Target == p)
            {
                var touchedCards = AnalysisHelpers.GetTouchedCards(state.Hands[p], action);
                if (touchedCards.Any(c => c.DeckIndex == chopCard.DeckIndex))
                    continue;
            }

            bool needsSave = false;
            string saveReason = "";

            if (chopCard.Rank == 5 && state.PlayStacks[chopCard.SuitIndex] < 5)
            {
                needsSave = true;
                saveReason = "it's a 5";
            }
            else if (AnalysisHelpers.IsCardCriticalForSave(chopCard, state, game))
            {
                needsSave = true;
                saveReason = "it's critical (last copy)";
            }
            else if (chopCard.Rank == 2 && state.PlayStacks[chopCard.SuitIndex] < 2)
            {
                var visibleCopies = AnalysisHelpers.CountVisibleCopies(chopCard, state, game, context.CurrentPlayerIndex);
                if (visibleCopies == 0)
                {
                    needsSave = true;
                    saveReason = "it's a 2 with no other copies visible";
                }
            }

            if (needsSave && !chopCard.HasAnyClue)
            {
                var suitName = AnalysisHelpers.GetSuitName(chopCard.SuitIndex);
                string actionDescription = action.Type switch
                {
                    ActionType.Play => "Played instead of saving",
                    ActionType.ColorClue or ActionType.RankClue => "Gave a clue instead of saving",
                    _ => "Discarded instead of saving"
                };

                context.Violations.Add(new RuleViolation
                {
                    Turn = context.Turn,
                    Player = context.CurrentPlayer,
                    Type = ViolationType.MissedSave,
                    Severity = Severity.Warning,
                    Description = $"{actionDescription} {game.Players[p]}'s {suitName} {chopCard.Rank} on chop ({saveReason})"
                });
            }
        }
    }
}
