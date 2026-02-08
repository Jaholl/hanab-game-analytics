using MyWebApi.Models;
using MyWebApi.Services.Analysis.Helpers;

namespace MyWebApi.Services.Analysis.Checkers.Level2;

/// <summary>
/// Detects "Wrong Prompt" violations (Level 2).
///
/// A Wrong Prompt occurs when:
/// 1. A player gives a clue intending a finesse on another player
/// 2. But an intermediate player has a clued card that looks like it could be the
///    connecting card (prompts take precedence over finesses in H-Group conventions)
/// 3. That player plays their clued card (the "prompted" card) but it's the WRONG card,
///    resulting in a misplay
/// 4. The clue-giver is to blame for not accounting for the prompt
///
/// This checker triggers on Play actions that result in misplays of clued cards.
/// It searches ClueHistory for a recent clue to a different player whose focus card
/// needed a connecting card matching what the misplaying player's clued card appeared to be.
/// </summary>
public class WrongPromptChecker : IViolationChecker
{
    public ConventionLevel Level => ConventionLevel.Level2_Intermediate;

    private static readonly HashSet<ActionType> _applicableTypes = new() { ActionType.Play };
    public IReadOnlySet<ActionType> ApplicableActionTypes => _applicableTypes;

    public void Check(AnalysisContext context)
    {
        var hand = context.StateBefore.Hands[context.CurrentPlayerIndex];
        var deckIndex = context.Action.Target;
        var card = hand.FirstOrDefault(c => c.DeckIndex == deckIndex);
        if (card == null) return;

        // Step 1: Only applies to misplays
        var expectedRank = context.StateBefore.PlayStacks[card.SuitIndex] + 1;
        if (card.Rank == expectedRank) return; // Valid play, not a misplay

        // Step 2: The misplayed card must be clued (it was played because it was "prompted")
        if (!card.HasAnyClue) return;

        // Step 2b: The misplayed card must be the prompt candidate in the hand.
        // Prompt precedence says the player plays their oldest (lowest index) clued card.
        // If they misplayed a non-prompt-candidate card, it's not a wrong prompt.
        var cardIndex = hand.FindIndex(c => c.DeckIndex == deckIndex);
        var oldestCluedIndex = -1;
        for (int i = 0; i < hand.Count; i++)
        {
            if (hand[i].HasAnyClue) { oldestCluedIndex = i; break; }
        }
        if (oldestCluedIndex >= 0 && cardIndex != oldestCluedIndex) return;

        // Step 3: Find the most recent clue that could have caused this wrong prompt.
        // Search backwards through ClueHistory for a clue that:
        //   - Was given to a DIFFERENT player (not the current misplaying player)
        //   - Was given AFTER the current player's card was last clued
        //   - Has a focus card that needed a connecting card the misplayed card could match

        // First, find when the misplayed card was last clued
        var lastClueOnMisplayedCard = context.ClueHistory
            .Where(c => c.TargetPlayerIndex == context.CurrentPlayerIndex &&
                        c.TouchedDeckIndices.Contains(deckIndex))
            .OrderByDescending(c => c.Turn)
            .FirstOrDefault();

        if (lastClueOnMisplayedCard == null) return;

        // Search ALL candidate triggering clues (not just the most recent), because an
        // unrelated clue may have occurred after the actual triggering clue.
        var candidateClues = context.ClueHistory
            .Where(c => c.TargetPlayerIndex != context.CurrentPlayerIndex &&
                        c.Turn > lastClueOnMisplayedCard.Turn &&
                        c.FocusDeckIndex.HasValue)
            .OrderByDescending(c => c.Turn)
            .ToList();

        if (candidateClues.Count == 0) return;

        var numPlayers = context.Game.Players.Count;

        foreach (var triggeringClue in candidateClues)
        {
            // Step 4: Check if the triggering clue's focus card needed a connecting card
            // that matches the clue marks on the misplayed card.
            var stateAtClue = context.States[triggeringClue.Turn - 1];
            var targetHand = stateAtClue.Hands[triggeringClue.TargetPlayerIndex];
            var focusCard = targetHand.FirstOrDefault(c => c.DeckIndex == triggeringClue.FocusDeckIndex);
            if (focusCard == null) continue;

            // The focus card must not have been directly playable at the time of the clue
            if (AnalysisHelpers.IsCardPlayable(focusCard, stateAtClue)) continue;

            var neededRank = stateAtClue.PlayStacks[focusCard.SuitIndex] + 1;

            // The focus card's rank should be > neededRank (needs a connecting card first)
            if (focusCard.Rank <= neededRank) continue;

            // Check clue mark consistency: when both color and rank clues are present,
            // BOTH must be compatible. When only one type is present, that one must match.
            bool hasColorClue = card.ClueColors.Any(c => c);
            bool hasRankClue = card.ClueRanks.Any(r => r);
            bool colorMatches = card.ClueColors[focusCard.SuitIndex];
            bool rankMatches = card.ClueRanks[neededRank - 1];

            bool clueMarksMatch;
            if (hasColorClue && hasRankClue)
            {
                // Both clue types present: both must agree for the player to believe
                // this card is the connecting card
                clueMarksMatch = colorMatches && rankMatches;
            }
            else
            {
                // Only one clue type: it must match
                clueMarksMatch = colorMatches || rankMatches;
            }

            if (!clueMarksMatch) continue;

            // Step 5: Verify the misplaying player is actually the prompted seat.
            bool isPromptedSeat = false;
            for (int offset = 1; offset < numPlayers; offset++)
            {
                int checkPlayer = (triggeringClue.ClueGiverIndex + offset) % numPlayers;
                if (checkPlayer == triggeringClue.TargetPlayerIndex) break;
                if (checkPlayer == context.CurrentPlayerIndex) { isPromptedSeat = true; break; }
            }
            if (!isPromptedSeat) continue;

            // Step 6: Verify there IS a valid finesse target that the clue giver likely intended
            bool validFinesseExists = AnalysisHelpers.CheckForValidFinesse(
                triggeringClue, stateAtClue, context.Game, focusCard);

            if (!validFinesseExists) continue;

            // All checks pass: this is a Wrong Prompt
            var suitName = AnalysisHelpers.GetSuitName(card.SuitIndex);
            var focusSuitName = AnalysisHelpers.GetSuitName(focusCard.SuitIndex);
            var clueGiver = context.Game.Players[triggeringClue.ClueGiverIndex];

            context.Violations.Add(new RuleViolation
            {
                Turn = triggeringClue.Turn,
                Player = clueGiver,
                Type = ViolationType.WrongPrompt,
                Severity = Severity.Warning,
                Description = $"Wrong prompt: clue to {context.Game.Players[triggeringClue.TargetPlayerIndex]} " +
                              $"for {focusSuitName} {focusCard.Rank} caused {context.CurrentPlayer} " +
                              $"to play {suitName} {card.Rank} as a prompt, but it was the wrong card. " +
                              $"Should have accounted for the existing clued card."
            });
            return; // Only report the first matching triggering clue
        }
    }
}
