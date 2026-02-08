using MyWebApi.Models;
using MyWebApi.Services.Analysis.Helpers;

namespace MyWebApi.Services.Analysis.Trackers;

/// <summary>
/// Tracks pending finesses across turns for cross-turn finesse resolution.
/// </summary>
public class PendingFinesseTracker : IStateTracker
{
    private static readonly HashSet<ActionType> _applicableTypes = new()
    {
        ActionType.Play,
        ActionType.Discard,
        ActionType.ColorClue,
        ActionType.RankClue
    };

    public IReadOnlySet<ActionType> ApplicableActionTypes => _applicableTypes;

    public void Track(AnalysisContext context)
    {
        // Resolve pending finesses when a player plays the needed card
        if (context.Action.Type == ActionType.Play)
        {
            var hand = context.StateBefore.Hands[context.CurrentPlayerIndex];
            var card = hand.FirstOrDefault(c => c.DeckIndex == context.Action.Target);
            if (card != null)
            {
                foreach (var finesse in context.PendingFinesses)
                {
                    if (!finesse.IsResolved &&
                        finesse.FinessePlayerIndex == context.CurrentPlayerIndex &&
                        finesse.NeededSuitIndex == card.SuitIndex &&
                        finesse.NeededRank == card.Rank)
                    {
                        finesse.IsResolved = true;
                    }
                }
            }
        }

        // Resolve expired pending finesses whose deadline has passed without response.
        // This is deferred from FinesseSetupChecker so that StompedFinesseChecker can
        // detect stomps on intervening turns before the deadline.
        foreach (var finesse in context.PendingFinesses)
        {
            if (finesse.IsResolved || finesse.WasStomped) continue;
            if (finesse.ResponseDeadlineActionIndex < 0) continue;
            if (context.ActionIndex < finesse.ResponseDeadlineActionIndex) continue;

            finesse.IsResolved = true;

            var suitName = AnalysisHelpers.GetSuitName(finesse.NeededSuitIndex);
            context.Violations.Add(new RuleViolation
            {
                Turn = finesse.SetupTurn,
                Player = context.Game.Players[finesse.FinessePlayerIndex],
                Type = ViolationType.MissedFinesse,
                Severity = Severity.Info,
                Description = $"Possible finesse for {suitName} {finesse.NeededRank} was set up but not followed"
            });
        }
    }
}
