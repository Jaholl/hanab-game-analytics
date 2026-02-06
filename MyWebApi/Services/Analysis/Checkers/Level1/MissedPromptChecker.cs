using MyWebApi.Models;
using MyWebApi.Services.Analysis.Helpers;

namespace MyWebApi.Services.Analysis.Checkers.Level1;

/// <summary>
/// Detects when a player discards while holding a playable clued card (missed prompt).
/// </summary>
public class MissedPromptChecker : IViolationChecker
{
    public ConventionLevel Level => ConventionLevel.Level1_Beginner;

    private static readonly HashSet<ActionType> _applicableTypes = new() { ActionType.Discard };
    public IReadOnlySet<ActionType> ApplicableActionTypes => _applicableTypes;

    public void Check(AnalysisContext context)
    {
        var hand = context.StateBefore.Hands[context.CurrentPlayerIndex];
        var state = context.StateBefore;

        foreach (var card in hand)
        {
            if (!card.HasAnyClue) continue;
            if (!AnalysisHelpers.IsCardPlayable(card, state)) continue;
            if (!IsKnownPlayableFromClues(card, state)) continue;

            var suitName = AnalysisHelpers.GetSuitName(card.SuitIndex);
            context.Violations.Add(new RuleViolation
            {
                Turn = context.Turn,
                Player = context.CurrentPlayer,
                Type = ViolationType.MissedPrompt,
                Severity = Severity.Warning,
                Description = $"Discarded but had a playable clued card ({suitName} {card.Rank})"
            });
            return; // Only report once per turn
        }
    }

    /// <summary>
    /// Checks if the player can deduce from their clue marks that this card is playable.
    /// Avoids false positives from omniscient knowledge the player doesn't have.
    /// </summary>
    private static bool IsKnownPlayableFromClues(CardInHand card, GameState state)
    {
        bool hasColorClue = card.ClueColors.Any(c => c);
        bool hasRankClue = card.ClueRanks.Any(r => r);

        if (hasColorClue && hasRankClue)
        {
            // Fully determined — player knows exact card identity
            return true;
        }

        if (hasRankClue)
        {
            // Player knows rank but not color.
            // Only flag if EVERY suit needs this rank (card is playable regardless of suit).
            int knownRank = -1;
            for (int i = 0; i < card.ClueRanks.Length; i++)
            {
                if (card.ClueRanks[i]) { knownRank = i + 1; break; }
            }
            if (knownRank <= 0) return false;

            for (int suit = 0; suit < state.PlayStacks.Length; suit++)
            {
                if (state.PlayStacks[suit] != knownRank - 1)
                    return false;
            }
            return true;
        }

        if (hasColorClue)
        {
            // Player knows color but not rank. In H-Group, a color clue implies
            // the card is the next needed card for that suit. If the card actually IS
            // the next needed rank, the player's inference is correct and they should play.
            int knownColor = Array.IndexOf(card.ClueColors, true);
            if (knownColor >= 0 && state.PlayStacks[knownColor] == card.Rank - 1)
                return true;
        }

        return false;
    }
}
