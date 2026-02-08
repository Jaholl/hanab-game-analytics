using MyWebApi.Models;
using MyWebApi.Services.Analysis.Helpers;

namespace MyWebApi.Services.Analysis.Checkers.Level2;

/// <summary>
/// Detects when a player gives a direct clue to a card that was supposed to be
/// blind-played through a finesse, wasting a clue ("stomping on a finesse").
/// </summary>
public class StompedFinesseChecker : IViolationChecker
{
    public ConventionLevel Level => ConventionLevel.Level2_Intermediate;

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
        var targetPlayer = action.Target;
        if (targetPlayer < 0 || targetPlayer >= state.Hands.Count) return;

        var targetHand = state.Hands[targetPlayer];

        // Get cards touched by this clue
        var touchedCards = AnalysisHelpers.GetTouchedCards(targetHand, action);
        if (touchedCards.Count == 0) return;

        foreach (var finesse in context.PendingFinesses)
        {
            if (finesse.IsResolved || finesse.WasStomped) continue;

            // The stomping clue must target the finesse player
            if (targetPlayer != finesse.FinessePlayerIndex) continue;

            // Check if any touched card matches the needed blind-play card
            var stompedCard = touchedCards.FirstOrDefault(c =>
                c.SuitIndex == finesse.NeededSuitIndex && c.Rank == finesse.NeededRank);

            if (stompedCard == null) continue;

            finesse.WasStomped = true;

            var suitName = AnalysisHelpers.GetSuitName(finesse.NeededSuitIndex);
            var finessePlayer = context.Game.Players[finesse.FinessePlayerIndex];
            context.Violations.Add(new RuleViolation
            {
                Turn = context.Turn,
                Player = context.CurrentPlayer,
                Type = ViolationType.StompedFinesse,
                Severity = Severity.Warning,
                Description = $"Stomped on finesse: directly clued {suitName} {finesse.NeededRank} in {finessePlayer}'s hand that was set up for blind play on turn {finesse.SetupTurn}"
            });
        }
    }
}
