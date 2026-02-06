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

        var pendingViolations = new List<RuleViolation>();

        foreach (var card in touchedCards)
        {
            var suitName = AnalysisHelpers.GetSuitName(card.SuitIndex);

            if (state.PlayStacks[card.SuitIndex] >= card.Rank)
            {
                // Suppress if recipient can trivially deduce this is trash.
                // Color clue + suit complete (stack=5) → player knows ALL cards of that color are trash.
                // (With stack < 5, player can't distinguish trash from useful cards of the same color.)
                if (action.Type == ActionType.ColorClue &&
                    state.PlayStacks[card.SuitIndex] >= 5)
                    continue;

                pendingViolations.Add(new RuleViolation
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
                pendingViolations.Add(new RuleViolation
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
                        // Suppress if the duplicate is harmlessly resolved (discarded or played successfully)
                        if (IsDuplicateHarmlesslyResolved(context, card))
                            break;

                        pendingViolations.Add(new RuleViolation
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
            // Suppress if all copies are harmlessly resolved (natural play order solves it)
            bool allResolved = dupeGroup.All(c => IsDuplicateHarmlesslyResolved(context, c));
            if (allResolved)
                continue;

            var suitName = AnalysisHelpers.GetSuitName(dupeGroup.Key.SuitIndex);
            pendingViolations.Add(new RuleViolation
            {
                Turn = context.Turn,
                Player = context.CurrentPlayer,
                Type = ViolationType.GoodTouchViolation,
                Severity = Severity.Warning,
                Description = $"Clue touched duplicate {suitName} {dupeGroup.Key.Rank} in same hand"
            });
        }

        // Suppress violations if no clean clue exists (burn/stall clue)
        if (pendingViolations.Count > 0 && !HasAnyCleanClue(state, context.CurrentPlayerIndex))
            return;

        context.Violations.AddRange(pendingViolations);
    }

    /// <summary>
    /// Checks if a duplicated card is harmlessly resolved — either safely discarded
    /// or played successfully. If the card stays in hand when the game ends,
    /// it's considered unresolved (the player may still be confused by the duplicate).
    /// </summary>
    private static bool IsDuplicateHarmlesslyResolved(AnalysisContext context, CardInHand card)
    {
        var game = context.Game;
        for (int i = context.ActionIndex + 1; i < game.Actions.Count; i++)
        {
            var futureAction = game.Actions[i];
            if (futureAction.Target != card.DeckIndex) continue;

            if (futureAction.Type == ActionType.Discard)
                return true; // Safely discarded

            if (futureAction.Type == ActionType.Play)
            {
                bool playable = context.States[i].PlayStacks[card.SuitIndex] == card.Rank - 1;
                return playable; // Resolved if played successfully, not if misplayed
            }
        }

        // Card stayed in hand until game end — unresolved
        return false;
    }

    /// <summary>
    /// Checks if any legal clue exists that doesn't touch trash cards and provides
    /// new information. If no such clue exists, the player is forced to give a
    /// "burn" clue and GoodTouch violations should be suppressed.
    /// </summary>
    private static bool HasAnyCleanClue(GameState state, int currentPlayerIndex)
    {
        int numPlayers = state.Hands.Count;

        for (int target = 0; target < numPlayers; target++)
        {
            if (target == currentPlayerIndex) continue;
            var hand = state.Hands[target];
            if (hand.Count == 0) continue;

            // Try each color clue
            for (int color = 0; color < state.PlayStacks.Length; color++)
            {
                var touched = hand.Where(c => c.SuitIndex == color).ToList();
                if (touched.Count == 0) continue;
                bool allClean = touched.All(c => !AnalysisHelpers.IsCardTrash(c, state));
                bool givesNewInfo = touched.Any(c =>
                    !AnalysisHelpers.IsCardTrash(c, state) && !c.ClueColors[color]);
                if (allClean && givesNewInfo)
                    return true;
            }

            // Try each rank clue (1-5)
            for (int rank = 1; rank <= 5; rank++)
            {
                var touched = hand.Where(c => c.Rank == rank).ToList();
                if (touched.Count == 0) continue;
                bool allClean = touched.All(c => !AnalysisHelpers.IsCardTrash(c, state));
                bool givesNewInfo = touched.Any(c =>
                    !AnalysisHelpers.IsCardTrash(c, state) && !c.ClueRanks[rank - 1]);
                if (allClean && givesNewInfo)
                    return true;
            }
        }

        return false;
    }
}
