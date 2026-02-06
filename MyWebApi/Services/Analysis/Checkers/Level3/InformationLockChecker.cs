using MyWebApi.Models;
using MyWebApi.Services.Analysis.Helpers;

namespace MyWebApi.Services.Analysis.Checkers.Level3;

/// <summary>
/// Detects information lock violations.
/// H-Group convention: Once a card's identity is fully determined by clues
/// (both color and rank are known), the player should act on that knowledge.
/// Discarding a known-playable card is a violation.
/// </summary>
public class InformationLockChecker : IViolationChecker
{
    public ConventionLevel Level => ConventionLevel.Level3_Advanced;

    private static readonly HashSet<ActionType> _applicableTypes = new() { ActionType.Discard };
    public IReadOnlySet<ActionType> ApplicableActionTypes => _applicableTypes;

    public void Check(AnalysisContext context)
    {
        var hand = context.StateBefore.Hands[context.CurrentPlayerIndex];
        var deckIndex = context.Action.Target;

        var card = hand.FirstOrDefault(c => c.DeckIndex == deckIndex);
        if (card == null) return;

        // Check if the card identity is fully determined (both color and rank known)
        if (!IsFullyDetermined(card)) return;

        // If the card is playable, discarding it is a violation
        if (AnalysisHelpers.IsCardPlayable(card, context.StateBefore))
        {
            var suitName = AnalysisHelpers.GetSuitName(card.SuitIndex);
            context.Violations.Add(new RuleViolation
            {
                Turn = context.Turn,
                Player = context.CurrentPlayer,
                Type = ViolationType.InformationLock,
                Severity = Severity.Warning,
                Description = $"Discarded fully known {suitName} {card.Rank} which was playable - locked information should be acted on"
            });
        }
    }

    private static bool IsFullyDetermined(CardInHand card)
    {
        // Card is fully determined if exactly one color and one rank are known
        bool hasColorClue = card.ClueColors.Any(c => c);
        bool hasRankClue = card.ClueRanks.Any(r => r);

        return hasColorClue && hasRankClue;
    }
}
