using MyWebApi.Models;
using MyWebApi.Services.Analysis.Helpers;

namespace MyWebApi.Services.Analysis.Checkers.Level3;

/// <summary>
/// Detects misplay cost violations.
/// H-Group convention: Spending 1 clue to prevent 1 misplay is always worthwhile.
/// If a player can see a teammate is about to misplay and has clue tokens,
/// they should spend a clue to prevent it.
/// </summary>
public class MisplayCostChecker : IViolationChecker
{
    public ConventionLevel Level => ConventionLevel.Level3_Advanced;

    private static readonly HashSet<ActionType> _applicableTypes = new()
    {
        ActionType.Play,
        ActionType.Discard
    };
    public IReadOnlySet<ActionType> ApplicableActionTypes => _applicableTypes;

    public void Check(AnalysisContext context)
    {
        // Only relevant when player did NOT give a clue (they played or discarded)
        if (context.StateBefore.ClueTokens == 0) return; // Can't give clues

        var game = context.Game;
        var state = context.StateBefore;
        var numPlayers = game.Players.Count;

        // Check if the next player misplays a clued card.
        // 1-action lookahead is correct: this checker runs for every action,
        // so each player is only responsible for the misplay immediately after their turn.
        // Blame correctly cascades to the player right before the misplay.
        int nextActionIndex = context.ActionIndex + 1;
        if (nextActionIndex >= game.Actions.Count) return;

        var nextAction = game.Actions[nextActionIndex];
        int nextPlayerIndex = nextActionIndex % numPlayers;

        if (nextAction.Type != ActionType.Play) return;

        // Check if the next play was a misplay
        var nextState = context.States[nextActionIndex];
        var nextHand = nextState.Hands[nextPlayerIndex];
        var nextCard = nextHand.FirstOrDefault(c => c.DeckIndex == nextAction.Target);
        if (nextCard == null) return;

        // Was it actually a misplay?
        if (AnalysisHelpers.IsCardPlayable(nextCard, nextState)) return;

        // Was the misplayed card clued? (player thought it was playable)
        if (!nextCard.HasAnyClue) return;

        // Current player could have prevented this with a clue
        var suitName = AnalysisHelpers.GetSuitName(nextCard.SuitIndex);
        context.Violations.Add(new RuleViolation
        {
            Turn = context.Turn,
            Player = context.CurrentPlayer,
            Type = ViolationType.MisplayCostViolation,
            Severity = Severity.Warning,
            Description = $"Could have spent 1 clue to prevent {game.Players[nextPlayerIndex]}'s misplay of {suitName} {nextCard.Rank}"
        });
    }
}
