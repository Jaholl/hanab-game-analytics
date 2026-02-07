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
                var visibleCopies = AnalysisHelpers.CountVisibleCopies(chopCard, state, game, context.CurrentPlayerIndex, chopCard.DeckIndex);
                if (visibleCopies == 0)
                {
                    needsSave = true;
                    saveReason = "it's a 2 with no other copies visible";
                }
            }

            // Check if the current discard makes this chop card the last copy
            if (!needsSave && action.Type == ActionType.Discard &&
                state.PlayStacks[chopCard.SuitIndex] < chopCard.Rank)
            {
                var discardedCard = state.Hands[context.CurrentPlayerIndex]
                    .FirstOrDefault(c => c.DeckIndex == action.Target);
                if (discardedCard != null &&
                    discardedCard.SuitIndex == chopCard.SuitIndex &&
                    discardedCard.Rank == chopCard.Rank)
                {
                    var totalCopies = AnalysisHelpers.CardCopiesPerRank[chopCard.Rank];
                    var discardedCount = state.DiscardPile
                        .Count(c => c.SuitIndex == chopCard.SuitIndex && c.Rank == chopCard.Rank);
                    // After this discard, remaining = totalCopies - discardedCount - 1
                    if (totalCopies - discardedCount - 1 == 1)
                    {
                        needsSave = true;
                        saveReason = "discarding makes it the last copy";
                    }
                }
            }

            if (needsSave && !chopCard.HasAnyClue)
            {
                // Suppress if the card is saved by another player within one round
                if (IsChopCardSavedSoon(context, chopCard, p))
                    continue;

                // Elevate to critical if the discard directly caused the card to be lost
                var severity = Severity.Warning;
                if (saveReason == "discarding makes it the last copy" &&
                    WasChopCardLost(context, chopCard))
                {
                    severity = Severity.Critical;
                }

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
                    Severity = severity,
                    Description = $"{actionDescription} {game.Players[p]}'s {suitName} {chopCard.Rank} on chop ({saveReason})"
                });
            }
        }
    }

    /// <summary>
    /// Checks if the chop card was actually discarded later in the game.
    /// </summary>
    private static bool WasChopCardLost(AnalysisContext context, CardInHand chopCard)
    {
        var game = context.Game;
        for (int i = context.ActionIndex + 1; i < game.Actions.Count; i++)
        {
            var action = game.Actions[i];
            if (action.Type == ActionType.Discard && action.Target == chopCard.DeckIndex)
                return true;
            if (action.Type == ActionType.Play && action.Target == chopCard.DeckIndex)
                return false;
        }
        return false;
    }

    /// <summary>
    /// Checks if the chop card is saved (clued or played) within one round.
    /// If another player handles the save soon after, the current player's
    /// "missed save" is not truly missed — the team coordinated.
    /// </summary>
    private static bool IsChopCardSavedSoon(AnalysisContext context, CardInHand chopCard, int chopPlayerIndex)
    {
        var game = context.Game;
        var numPlayers = game.Players.Count;
        int maxLookahead = context.ActionIndex + numPlayers;

        for (int i = context.ActionIndex + 1; i < game.Actions.Count && i <= maxLookahead; i++)
        {
            var action = game.Actions[i];

            // Card got clued directly (saved)
            if ((action.Type == ActionType.ColorClue || action.Type == ActionType.RankClue) &&
                action.Target == chopPlayerIndex)
            {
                var hand = context.States[i].Hands[chopPlayerIndex];
                var touched = AnalysisHelpers.GetTouchedCards(hand, action);
                if (touched.Any(c => c.DeckIndex == chopCard.DeckIndex))
                    return true;
            }

            // Card was played (survived)
            if (action.Type == ActionType.Play && action.Target == chopCard.DeckIndex)
                return true;

            // Card was discarded (truly lost — save was needed!)
            if (action.Type == ActionType.Discard && action.Target == chopCard.DeckIndex)
                return false;
        }

        // Card not explicitly saved within one round → don't suppress
        return false;
    }
}
