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
        // Check when a player does something OTHER than giving a fix clue
        // and a teammate is about to misplay a clued-but-unplayable card

        if (context.StateBefore.ClueTokens == 0) return; // Can't give clues

        var game = context.Game;
        var state = context.StateBefore;
        var numPlayers = game.Players.Count;

        // Check all other players who act before the current player's next turn
        for (int offset = 1; offset < numPlayers; offset++)
        {
            int checkPlayerIndex = (context.CurrentPlayerIndex + offset) % numPlayers;
            var checkHand = state.Hands[checkPlayerIndex];

            // Find clued cards that are trash (already played or dead suit)
            foreach (var card in checkHand)
            {
                if (!card.HasAnyClue) continue;

                bool isTrash = state.PlayStacks[card.SuitIndex] >= card.Rank ||
                               AnalysisHelpers.IsSuitDead(card.SuitIndex, card.Rank, state);

                if (!isTrash) continue;

                // Check if current player's action was a fix clue targeting this player
                bool isFixClue = (context.Action.Type == ActionType.ColorClue ||
                                  context.Action.Type == ActionType.RankClue) &&
                                 context.Action.Target == checkPlayerIndex;

                if (isFixClue)
                {
                    var touchedCards = AnalysisHelpers.GetTouchedCards(checkHand, context.Action);
                    if (touchedCards.Any(c => c.DeckIndex == card.DeckIndex))
                        return; // Fix clue was given
                }

                // Find this player's next action to confirm the misplay threat
                int checkActionIndex = -1;
                for (int i = context.ActionIndex + 1; i < game.Actions.Count; i++)
                {
                    if (i % numPlayers == checkPlayerIndex)
                    {
                        checkActionIndex = i;
                        break;
                    }
                }
                if (checkActionIndex == -1) continue;

                var checkAction = game.Actions[checkActionIndex];
                if (checkAction.Type == ActionType.Play &&
                    checkAction.Target == card.DeckIndex)
                {
                    // This player did misplay the trash card!
                    if (!isFixClue)
                    {
                        var suitName = AnalysisHelpers.GetSuitName(card.SuitIndex);
                        var reason = state.PlayStacks[card.SuitIndex] >= card.Rank
                            ? "already played"
                            : "suit is dead";
                        context.Violations.Add(new RuleViolation
                        {
                            Turn = context.Turn,
                            Player = context.CurrentPlayer,
                            Type = ViolationType.FixClue,
                            Severity = Severity.Warning,
                            Description = $"Could have given fix clue to prevent {game.Players[checkPlayerIndex]}'s misplay of {suitName} {card.Rank} ({reason})"
                        });
                        return;
                    }
                }
            }
        }
    }
}
