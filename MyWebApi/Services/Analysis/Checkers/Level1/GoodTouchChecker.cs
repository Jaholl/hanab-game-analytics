using MyWebApi.Models;
using MyWebApi.Services.Analysis.Helpers;

namespace MyWebApi.Services.Analysis.Checkers.Level1;

/// <summary>
/// Good Touch Principle - clues should not touch dead cards (already played or duplicates).
/// </summary>
public class GoodTouchChecker : IViolationChecker
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
        var touchedCards = AnalysisHelpers.GetTouchedCards(targetHand, action);

        foreach (var card in touchedCards)
        {
            var suitName = AnalysisHelpers.GetSuitName(card.SuitIndex);

            if (state.PlayStacks[card.SuitIndex] >= card.Rank)
            {
                context.Violations.Add(new RuleViolation
                {
                    Turn = context.Turn,
                    Player = context.CurrentPlayer,
                    Type = ViolationType.GoodTouchViolation,
                    Severity = Severity.Warning,
                    Description = $"Clue touched {suitName} {card.Rank} which is already played (trash card)"
                });
                continue;
            }

            if (AnalysisHelpers.IsSuitDead(card.SuitIndex, card.Rank, state))
            {
                context.Violations.Add(new RuleViolation
                {
                    Turn = context.Turn,
                    Player = context.CurrentPlayer,
                    Type = ViolationType.GoodTouchViolation,
                    Severity = Severity.Warning,
                    Description = $"Clue touched {suitName} {card.Rank} which can never be played (suit is dead)"
                });
                continue;
            }

            for (int p = 0; p < state.Hands.Count; p++)
            {
                if (p == targetPlayer) continue;
                if (p == context.CurrentPlayerIndex) continue; // Clue-giver can't see own hand
                foreach (var otherCard in state.Hands[p])
                {
                    if (otherCard.SuitIndex == card.SuitIndex &&
                        otherCard.Rank == card.Rank &&
                        otherCard.HasAnyClue)
                    {
                        context.Violations.Add(new RuleViolation
                        {
                            Turn = context.Turn,
                            Player = context.CurrentPlayer,
                            Type = ViolationType.GoodTouchViolation,
                            Severity = Severity.Warning,
                            Description = $"Clue touched {suitName} {card.Rank} which duplicates a clued card in {game.Players[p]}'s hand"
                        });
                        break;
                    }
                }
            }
        }

        var sameHandDupes = touchedCards
            .GroupBy(c => (c.SuitIndex, c.Rank))
            .Where(g => g.Count() > 1);

        foreach (var dupeGroup in sameHandDupes)
        {
            var suitName = AnalysisHelpers.GetSuitName(dupeGroup.Key.SuitIndex);
            context.Violations.Add(new RuleViolation
            {
                Turn = context.Turn,
                Player = context.CurrentPlayer,
                Type = ViolationType.GoodTouchViolation,
                Severity = Severity.Warning,
                Description = $"Clue touched duplicate {suitName} {dupeGroup.Key.Rank} in same hand"
            });
        }
    }
}
