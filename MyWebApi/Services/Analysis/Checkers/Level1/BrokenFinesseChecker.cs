using MyWebApi.Models;
using MyWebApi.Services.Analysis.Helpers;

namespace MyWebApi.Services.Analysis.Checkers.Level1;

/// <summary>
/// Detects broken finesses - blind plays from finesse position that fail.
/// </summary>
public class BrokenFinesseChecker : IViolationChecker
{
    public ConventionLevel Level => ConventionLevel.Level1_Beginner;

    private static readonly HashSet<ActionType> _applicableTypes = new() { ActionType.Play };
    public IReadOnlySet<ActionType> ApplicableActionTypes => _applicableTypes;

    public void Check(AnalysisContext context)
    {
        var hand = context.StateBefore.Hands[context.CurrentPlayerIndex];
        var deckIndex = context.Action.Target;

        var cardIndex = hand.FindIndex(c => c.DeckIndex == deckIndex);
        if (cardIndex < 0) return;

        var card = hand[cardIndex];

        if (!card.HasAnyClue)
        {
            var finessePositionIndex = AnalysisHelpers.GetFinessePositionIndex(hand);
            bool isFinessePosition = finessePositionIndex.HasValue && cardIndex == finessePositionIndex.Value;

            if (!AnalysisHelpers.IsCardPlayable(card, context.StateBefore))
            {
                var suitName = AnalysisHelpers.GetSuitName(card.SuitIndex);
                var expectedRank = context.StateBefore.PlayStacks[card.SuitIndex] + 1;

                if (isFinessePosition)
                {
                    context.Violations.Add(new RuleViolation
                    {
                        Turn = context.Turn,
                        Player = context.CurrentPlayer,
                        Type = ViolationType.BrokenFinesse,
                        Severity = Severity.Warning,
                        Description = $"Blind-played {suitName} {card.Rank} from finesse position but needed {suitName} {expectedRank}"
                    });
                }
            }
        }
    }
}
