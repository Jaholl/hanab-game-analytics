using MyWebApi.Models;
using MyWebApi.Services.Analysis.Helpers;

namespace MyWebApi.Services.Analysis.Checkers.Level2;

/// <summary>
/// Detects double discard avoidance violations - discarding from chop
/// after the previous player also discarded from chop.
/// </summary>
public class DoubleDiscardAvoidanceChecker : IViolationChecker
{
    public ConventionLevel Level => ConventionLevel.Level2_Intermediate;

    private static readonly HashSet<ActionType> _applicableTypes = new() { ActionType.Discard };
    public IReadOnlySet<ActionType> ApplicableActionTypes => _applicableTypes;

    public void Check(AnalysisContext context)
    {
        if (context.ActionIndex == 0) return;

        var game = context.Game;
        var previousAction = game.Actions[context.ActionIndex - 1];
        if (previousAction.Type != ActionType.Discard) return;

        var previousState = context.States[context.ActionIndex - 1];
        var numPlayers = game.Players.Count;
        var previousPlayerIndex = (context.CurrentPlayerIndex - 1 + numPlayers) % numPlayers;

        var previousHand = previousState.Hands[previousPlayerIndex];
        var previousChopIndex = AnalysisHelpers.GetChopIndex(previousHand);
        if (!previousChopIndex.HasValue) return;

        var previousDiscardedCard = previousHand.FirstOrDefault(c => c.DeckIndex == previousAction.Target);
        if (previousDiscardedCard == null) return;

        if (AnalysisHelpers.IsCardTrash(previousDiscardedCard, previousState)) return;

        var previousDiscardIndex = previousHand.FindIndex(c => c.DeckIndex == previousAction.Target);
        if (previousDiscardIndex != previousChopIndex.Value) return;

        var currentHand = context.StateBefore.Hands[context.CurrentPlayerIndex];
        var currentChopIndex = AnalysisHelpers.GetChopIndex(currentHand);
        if (!currentChopIndex.HasValue) return;

        var currentDiscardedCard = currentHand.FirstOrDefault(c => c.DeckIndex == context.Action.Target);
        if (currentDiscardedCard == null) return;

        var currentDiscardIndex = currentHand.FindIndex(c => c.DeckIndex == context.Action.Target);
        if (currentDiscardIndex != currentChopIndex.Value) return;

        if (AnalysisHelpers.IsCardTrash(currentDiscardedCard, context.StateBefore)) return;

        // Exempt forced discards: 0 clue tokens and no playable cards means the player
        // can only discard (playing would be a guaranteed misplay).
        // Note: In practice this is rare since the previous discard adds a clue token.
        if (context.StateBefore.ClueTokens == 0 &&
            !currentHand.Any(c => AnalysisHelpers.IsCardPlayable(c, context.StateBefore)))
        {
            return;
        }

        var suitName = AnalysisHelpers.GetSuitName(currentDiscardedCard.SuitIndex);
        context.Violations.Add(new RuleViolation
        {
            Turn = context.Turn,
            Player = context.CurrentPlayer,
            Type = ViolationType.DoubleDiscardAvoidance,
            Severity = Severity.Warning,
            Description = $"Discarded {suitName} {currentDiscardedCard.Rank} from chop after {game.Players[previousPlayerIndex]} discarded from chop - should avoid double discard"
        });
    }
}
