using MyWebApi.Models;
using MyWebApi.Services.Analysis.Helpers;

namespace MyWebApi.Services.Analysis.Checkers.Level2;

/// <summary>
/// Detects improper use of the 5 Stall convention outside of the Early Game.
/// In the Early Game, cluing number 5 to an off-chop 5 is a valid "5 Stall".
/// Outside of the Early Game, an off-chop 5 clue where the 5 is not playable
/// is a misuse of the convention.
/// </summary>
public class FiveStallChecker : IViolationChecker
{
    public ConventionLevel Level => ConventionLevel.Level2_Intermediate;

    private static readonly HashSet<ActionType> _applicableTypes = new() { ActionType.RankClue };
    public IReadOnlySet<ActionType> ApplicableActionTypes => _applicableTypes;

    public void Check(AnalysisContext context)
    {
        var action = context.Action;

        // Only applies to rank 5 clues
        if (action.Value != 5) return;

        var state = context.StateBefore;
        var targetPlayer = action.Target;
        if (targetPlayer < 0 || targetPlayer >= state.Hands.Count) return;

        var targetHand = state.Hands[targetPlayer];

        // Find newly touched cards (cards that match rank 5 and had no prior clues)
        var newlyTouchedCards = new List<(CardInHand card, int index)>();
        for (int i = 0; i < targetHand.Count; i++)
        {
            var card = targetHand[i];
            if (card.Rank == 5 && !card.HasAnyClue)
                newlyTouchedCards.Add((card, i));
        }

        if (newlyTouchedCards.Count == 0) return;

        // Determine focus using chop-focus logic
        var chopIndex = AnalysisHelpers.GetChopIndex(targetHand);

        CardInHand focusCard;
        int focusHandIndex;

        if (chopIndex.HasValue && newlyTouchedCards.Any(t => t.index == chopIndex.Value))
        {
            // Chop is among newly touched -> focus on chop (this is a save clue, not a stall)
            return;
        }
        else
        {
            // Focus is the newest (highest index) among newly touched cards
            var focused = newlyTouchedCards.OrderByDescending(t => t.index).First();
            focusCard = focused.card;
            focusHandIndex = focused.index;
        }

        // Focus is a 5, off chop. Check if it's playable.
        if (AnalysisHelpers.IsCardPlayable(focusCard, state)) return;

        // In the Early Game, this is a valid 5 Stall - no violation
        if (context.IsEarlyGame) return;

        // Outside the Early Game, cluing an off-chop non-playable 5 is a misuse
        var suitName = AnalysisHelpers.GetSuitName(focusCard.SuitIndex);
        context.Violations.Add(new RuleViolation
        {
            Turn = context.Turn,
            Player = context.CurrentPlayer,
            Type = ViolationType.FiveStall,
            Severity = Severity.Warning,
            Description = $"Clued 5 to {context.Game.Players[targetPlayer]}'s {suitName} 5 (off-chop, not playable) outside of the Early Game - 5 Stall is only valid in the Early Game"
        });
    }
}
