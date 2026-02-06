using MyWebApi.Models;
using MyWebApi.Services.Analysis.Helpers;

namespace MyWebApi.Services.Analysis.Checkers.Level3;

/// <summary>
/// Detects missed fix clue opportunities.
/// H-Group convention: When a teammate has a clued card they believe is playable
/// but it's actually trash (already played), and a misplay is imminent,
/// you should give a "fix clue" to prevent the misplay.
/// </summary>
public class FixClueChecker : IViolationChecker
{
    public ConventionLevel Level => ConventionLevel.Level3_Advanced;

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
        // Only check when a player does something OTHER than giving a fix clue
        // and a teammate is about to misplay a clued-but-unplayable card

        if (context.StateBefore.ClueTokens == 0) return; // Can't give clues

        var game = context.Game;
        var state = context.StateBefore;
        var numPlayers = game.Players.Count;

        // Look at the next player's hand to see if they have a misplay-risk card
        int nextPlayerIndex = (context.CurrentPlayerIndex + 1) % numPlayers;
        var nextHand = state.Hands[nextPlayerIndex];

        // Find clued cards in next player's hand that are trash (already played)
        // These are cards the next player might try to play, causing a misplay
        foreach (var card in nextHand)
        {
            if (!card.HasAnyClue) continue;

            // Card is already played (trash) - player might try to play it
            if (state.PlayStacks[card.SuitIndex] >= card.Rank)
            {
                // Check if current player's action was a fix clue targeting the next player
                bool isFixClue = (context.Action.Type == ActionType.ColorClue ||
                                  context.Action.Type == ActionType.RankClue) &&
                                 context.Action.Target == nextPlayerIndex;

                if (isFixClue)
                {
                    // Check if the clue touches the problematic card
                    var touchedCards = AnalysisHelpers.GetTouchedCards(nextHand, context.Action);
                    if (touchedCards.Any(c => c.DeckIndex == card.DeckIndex))
                        return; // Fix clue was given
                }

                // Check if the next player actually misplays this card
                // (look ahead to confirm the threat was real)
                int nextActionIndex = context.ActionIndex + 1;
                if (nextActionIndex < game.Actions.Count)
                {
                    var nextAction = game.Actions[nextActionIndex];
                    int nextActingPlayer = nextActionIndex % numPlayers;

                    if (nextActingPlayer == nextPlayerIndex &&
                        nextAction.Type == ActionType.Play &&
                        nextAction.Target == card.DeckIndex)
                    {
                        // The next player did misplay this card!
                        // Current player should have given a fix clue
                        if (!isFixClue)
                        {
                            var suitName = AnalysisHelpers.GetSuitName(card.SuitIndex);
                            context.Violations.Add(new RuleViolation
                            {
                                Turn = context.Turn,
                                Player = context.CurrentPlayer,
                                Type = ViolationType.FixClue,
                                Severity = Severity.Warning,
                                Description = $"Could have given fix clue to prevent {game.Players[nextPlayerIndex]}'s misplay of {suitName} {card.Rank} (already played)"
                            });
                            return;
                        }
                    }
                }
            }
        }
    }
}
