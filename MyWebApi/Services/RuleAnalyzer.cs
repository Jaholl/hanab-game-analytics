using MyWebApi.Models;

namespace MyWebApi.Services;

// TODO: Implement cross-turn state tracking for:
//   - Pending finesses that haven't been resolved yet
//   - Delayed plays that are completed on subsequent turns
//   - Context about why a player chose one action over another (urgency evaluation)
// TODO: Add support for detecting more advanced conventions (bluffs, ejections, discharges)
public class RuleAnalyzer
{
    // Standard 5-suit card counts per rank
    // Rank 1: 3 copies, Rank 2-4: 2 copies, Rank 5: 1 copy
    private static readonly int[] CardCopiesPerRank = { 0, 3, 2, 2, 2, 1 };

    private static readonly string[] SuitNames = { "Red", "Yellow", "Green", "Blue", "Purple" };

    public List<RuleViolation> AnalyzeGame(GameExport game, List<GameState> states)
    {
        var violations = new List<RuleViolation>();

        for (int i = 0; i < game.Actions.Count; i++)
        {
            var action = game.Actions[i];
            var stateBefore = states[i]; // State before this action
            var stateAfter = states[i + 1]; // State after this action

            // Calculate current player from action index (players take turns in order)
            var currentPlayerIndex = i % game.Players.Count;
            var currentPlayer = game.Players[currentPlayerIndex];

            switch (action.Type)
            {
                case ActionType.Play:
                    CheckMisplay(violations, action, stateBefore, currentPlayerIndex, currentPlayer, i + 1);
                    CheckBrokenFinesse(violations, action, stateBefore, currentPlayerIndex, currentPlayer, i + 1);
                    break;
                case ActionType.Discard:
                    CheckBadDiscard(violations, action, stateBefore, game, currentPlayerIndex, currentPlayer, i + 1);
                    CheckMissedSave(violations, action, stateBefore, game, currentPlayerIndex, currentPlayer, i + 1);
                    CheckMissedPrompt(violations, action, stateBefore, currentPlayerIndex, currentPlayer, i + 1);
                    break;
                case ActionType.ColorClue:
                case ActionType.RankClue:
                    CheckGoodTouch(violations, action, stateBefore, game, currentPlayerIndex, currentPlayer, i + 1);
                    CheckMCVP(violations, action, stateBefore, game, currentPlayerIndex, currentPlayer, i + 1);
                    CheckFinesseSetup(violations, action, stateBefore, game, currentPlayerIndex, currentPlayer, i + 1, i, states);
                    break;
            }
        }

        return violations;
    }

    private void CheckMisplay(List<RuleViolation> violations, GameAction action, GameState state, int playerIndex, string player, int turn)
    {
        var hand = state.Hands[playerIndex];
        var deckIndex = action.Target;

        // Find the card in hand by its deck index
        var card = hand.FirstOrDefault(c => c.DeckIndex == deckIndex);
        if (card == null) return;
        var expectedRank = state.PlayStacks[card.SuitIndex] + 1;

        // Check if play is invalid
        if (card.Rank != expectedRank)
        {
            var suitName = GetSuitName(card.SuitIndex);
            var stackValue = state.PlayStacks[card.SuitIndex];

            violations.Add(new RuleViolation
            {
                Turn = turn,
                Player = player,
                Type = ViolationType.Misplay,
                Severity = Severity.Critical,
                Description = $"Played {suitName} {card.Rank} but {suitName} {stackValue} was on the stack (needed {expectedRank})",
                Card = new CardIdentifier
                {
                    DeckIndex = deckIndex,
                    SuitIndex = card.SuitIndex,
                    Rank = card.Rank
                }
            });
        }
    }

    private void CheckBadDiscard(List<RuleViolation> violations, GameAction action, GameState state, GameExport game, int playerIndex, string player, int turn)
    {
        var hand = state.Hands[playerIndex];
        var deckIndex = action.Target;

        // Find the card in hand by its deck index
        var card = hand.FirstOrDefault(c => c.DeckIndex == deckIndex);
        if (card == null) return;

        var suitName = GetSuitName(card.SuitIndex);

        // Check for discarding a 5
        if (card.Rank == 5)
        {
            // Only flag if the 5 hasn't been played yet
            if (state.PlayStacks[card.SuitIndex] < 5)
            {
                violations.Add(new RuleViolation
                {
                    Turn = turn,
                    Player = player,
                    Type = ViolationType.BadDiscard5,
                    Severity = Severity.Critical,
                    Description = $"Discarded {suitName} 5 - fives are always critical!"
                });
            }
            return; // Don't also flag as critical, 5s are already handled
        }

        // Check for discarding a critical card (last remaining copy)
        if (IsCardCritical(card, state, game))
        {
            // Only flag if the card is still needed (hasn't been played)
            if (state.PlayStacks[card.SuitIndex] < card.Rank)
            {
                violations.Add(new RuleViolation
                {
                    Turn = turn,
                    Player = player,
                    Type = ViolationType.BadDiscardCritical,
                    Severity = Severity.Critical,
                    Description = $"Discarded {suitName} {card.Rank} - it was the last copy!"
                });
            }
        }
    }

    private bool IsCardCritical(CardInHand card, GameState state, GameExport game)
    {
        // A card is critical if there's only one remaining copy
        var totalCopies = CardCopiesPerRank[card.Rank];
        var discardedCount = state.DiscardPile.Count(c => c.SuitIndex == card.SuitIndex && c.Rank == card.Rank);

        // Count how many are in hands (excluding the one being discarded)
        var inHandsCount = 0;
        foreach (var hand in state.Hands)
        {
            inHandsCount += hand.Count(c => c.SuitIndex == card.SuitIndex && c.Rank == card.Rank);
        }

        // Count in deck (cards not yet dealt)
        var inDeckCount = 0;
        for (int i = state.DeckIndex; i < game.Deck.Count; i++)
        {
            var deckCard = game.Deck[i];
            if (deckCard.SuitIndex == card.SuitIndex && deckCard.Rank == card.Rank)
            {
                inDeckCount++;
            }
        }

        // Remaining copies = total - discarded (not counting the current discard yet)
        // If in hands + in deck == 1, this is the last copy
        var remainingCopies = inHandsCount + inDeckCount;
        return remainingCopies == 1;
    }

    // Phase 2: Good Touch Principle - clues should not touch "dead" cards (already played or duplicates)
    private void CheckGoodTouch(List<RuleViolation> violations, GameAction action, GameState state, GameExport game, int playerIndex, string player, int turn)
    {
        var targetPlayer = action.Target;
        if (targetPlayer < 0 || targetPlayer >= state.Hands.Count) return;

        var targetHand = state.Hands[targetPlayer];
        var touchedCards = new List<CardInHand>();

        // Find which cards are touched by this clue
        if (action.Type == ActionType.ColorClue)
        {
            touchedCards = targetHand.Where(c => c.SuitIndex == action.Value).ToList();
        }
        else if (action.Type == ActionType.RankClue)
        {
            touchedCards = targetHand.Where(c => c.Rank == action.Value).ToList();
        }

        foreach (var card in touchedCards)
        {
            var suitName = GetSuitName(card.SuitIndex);

            // Check if card is already played (trash/dead card)
            if (state.PlayStacks[card.SuitIndex] >= card.Rank)
            {
                violations.Add(new RuleViolation
                {
                    Turn = turn,
                    Player = player,
                    Type = ViolationType.GoodTouchViolation,
                    Severity = Severity.Warning,
                    Description = $"Clue touched {suitName} {card.Rank} which is already played (trash card)"
                });
                continue;
            }

            // Check if card is a duplicate of another clued card elsewhere
            for (int p = 0; p < state.Hands.Count; p++)
            {
                if (p == targetPlayer) continue;

                foreach (var otherCard in state.Hands[p])
                {
                    if (otherCard.SuitIndex == card.SuitIndex &&
                        otherCard.Rank == card.Rank &&
                        otherCard.HasAnyClue)
                    {
                        violations.Add(new RuleViolation
                        {
                            Turn = turn,
                            Player = player,
                            Type = ViolationType.GoodTouchViolation,
                            Severity = Severity.Warning,
                            Description = $"Clue touched {suitName} {card.Rank} which duplicates a clued card in {game.Players[p]}'s hand"
                        });
                        break;
                    }
                }
            }
        }
    }

    // Phase 2: MCVP - Minimum Clue Value Principle - clue must touch at least one new card
    private void CheckMCVP(List<RuleViolation> violations, GameAction action, GameState state, GameExport game, int playerIndex, string player, int turn)
    {
        var targetPlayer = action.Target;
        if (targetPlayer < 0 || targetPlayer >= state.Hands.Count) return;

        var targetHand = state.Hands[targetPlayer];
        var newCardsTouched = 0;

        // Count how many new (previously unclued) cards are touched
        foreach (var card in targetHand)
        {
            bool isTouched = false;
            if (action.Type == ActionType.ColorClue && card.SuitIndex == action.Value)
            {
                isTouched = true;
            }
            else if (action.Type == ActionType.RankClue && card.Rank == action.Value)
            {
                isTouched = true;
            }

            if (isTouched && !card.HasAnyClue)
            {
                newCardsTouched++;
            }
        }

        if (newCardsTouched == 0)
        {
            var clueType = action.Type == ActionType.ColorClue
                ? GetSuitName(action.Value)
                : action.Value.ToString();

            violations.Add(new RuleViolation
            {
                Turn = turn,
                Player = player,
                Type = ViolationType.MCVPViolation,
                Severity = Severity.Warning,
                Description = $"Clue ({clueType}) only touched already-clued cards - no new information given"
            });
        }
    }

    // Phase 2: Missed Save - discarding when teammate has critical on chop
    private void CheckMissedSave(List<RuleViolation> violations, GameAction action, GameState state, GameExport game, int playerIndex, string player, int turn)
    {
        // Only check if we have clue tokens available
        if (state.ClueTokens == 0) return;

        var numPlayers = state.Hands.Count;

        // Check each other player's chop
        for (int p = 0; p < numPlayers; p++)
        {
            if (p == playerIndex) continue;

            var chopCard = GetChopCard(state.Hands[p]);
            if (chopCard == null) continue;

            // Check if chop card needs saving (is critical or is a 5 or is a 2 with no copies visible)
            bool needsSave = false;
            string saveReason = "";

            // 5s always need saving
            if (chopCard.Rank == 5 && state.PlayStacks[chopCard.SuitIndex] < 5)
            {
                needsSave = true;
                saveReason = "it's a 5";
            }
            // Critical cards need saving
            else if (IsCardCriticalForSave(chopCard, state, game))
            {
                needsSave = true;
                saveReason = "it's critical (last copy)";
            }
            // 2s on chop in early game often need saving
            else if (chopCard.Rank == 2 && state.PlayStacks[chopCard.SuitIndex] < 2)
            {
                // Check if this 2 is unique (no other copies visible to the team)
                var visibleCopies = CountVisibleCopies(chopCard, state, game, playerIndex);
                if (visibleCopies == 0)
                {
                    needsSave = true;
                    saveReason = "it's a 2 with no other copies visible";
                }
            }

            if (needsSave && !chopCard.HasAnyClue)
            {
                var suitName = GetSuitName(chopCard.SuitIndex);
                violations.Add(new RuleViolation
                {
                    Turn = turn,
                    Player = player,
                    Type = ViolationType.MissedSave,
                    Severity = Severity.Warning,
                    Description = $"Discarded instead of saving {game.Players[p]}'s {suitName} {chopCard.Rank} on chop ({saveReason})"
                });
            }
        }
    }

    // Get the chop card (oldest unclued card, rightmost in hand representation)
    private CardInHand? GetChopCard(List<CardInHand> hand)
    {
        // Chop is the oldest unclued card
        // In our representation, cards are added to the end, so older cards are at lower indices
        // Chop is typically the oldest (lowest index) unclued card
        for (int i = 0; i < hand.Count; i++)
        {
            if (!hand[i].HasAnyClue)
            {
                return hand[i];
            }
        }
        return null; // All cards are clued
    }

    // Check if card is critical (different from IsCardCritical - this counts the card itself)
    private bool IsCardCriticalForSave(CardInHand card, GameState state, GameExport game)
    {
        if (card.Rank == 5) return true;

        var totalCopies = CardCopiesPerRank[card.Rank];
        var discardedCount = state.DiscardPile.Count(c => c.SuitIndex == card.SuitIndex && c.Rank == card.Rank);

        return totalCopies - discardedCount == 1;
    }

    // Count copies visible to a player (in other hands, not their own)
    private int CountVisibleCopies(CardInHand card, GameState state, GameExport game, int excludePlayer)
    {
        int count = 0;
        for (int p = 0; p < state.Hands.Count; p++)
        {
            if (p == excludePlayer) continue;
            count += state.Hands[p].Count(c => c.SuitIndex == card.SuitIndex && c.Rank == card.Rank);
        }
        return count;
    }

    private string GetSuitName(int suitIndex)
    {
        if (suitIndex >= 0 && suitIndex < SuitNames.Length)
        {
            return SuitNames[suitIndex];
        }
        return $"Suit{suitIndex}";
    }

    // Phase 3: Missed Prompt - player has playable clued card but discards instead
    // TODO: Check if the discard was justified (e.g., generating a clue for an urgent save)
    // TODO: Consider if the player might not know their card is playable yet (incomplete information)
    private void CheckMissedPrompt(List<RuleViolation> violations, GameAction action, GameState state, int playerIndex, string player, int turn)
    {
        var hand = state.Hands[playerIndex];

        // Check if player has any clued cards that are currently playable
        foreach (var card in hand)
        {
            if (card.HasAnyClue && IsCardPlayable(card, state))
            {
                var suitName = GetSuitName(card.SuitIndex);
                violations.Add(new RuleViolation
                {
                    Turn = turn,
                    Player = player,
                    Type = ViolationType.MissedPrompt,
                    Severity = Severity.Warning,
                    Description = $"Discarded but had a playable clued card ({suitName} {card.Rank})"
                });
                return; // Only report once per turn
            }
        }
    }

    // Phase 3: Broken Finesse - blind play (unclued card) that fails
    // TODO: Track whether a finesse was actually set up for this player before flagging
    // TODO: Distinguish between "broken finesse" (clue-giver's fault) vs "misread finesse" (player's fault)
    private void CheckBrokenFinesse(List<RuleViolation> violations, GameAction action, GameState state, int playerIndex, string player, int turn)
    {
        var hand = state.Hands[playerIndex];
        var deckIndex = action.Target;

        // Find the card in hand by its deck index
        var cardIndex = hand.FindIndex(c => c.DeckIndex == deckIndex);
        if (cardIndex < 0) return;

        var card = hand[cardIndex];

        // Check if this was a blind play (card had no clues)
        if (!card.HasAnyClue)
        {
            // Check if it was the "finesse position" (newest card = last in hand)
            // TODO: Finesse position is actually "leftmost unclued card" - should find first unclued from end
            bool isFinessePosition = cardIndex == hand.Count - 1;

            // Check if play failed
            if (!IsCardPlayable(card, state))
            {
                var suitName = GetSuitName(card.SuitIndex);
                var expectedRank = state.PlayStacks[card.SuitIndex] + 1;

                if (isFinessePosition)
                {
                    violations.Add(new RuleViolation
                    {
                        Turn = turn,
                        Player = player,
                        Type = ViolationType.BrokenFinesse,
                        Severity = Severity.Warning,
                        Description = $"Blind-played {suitName} {card.Rank} from finesse position but needed {suitName} {expectedRank}"
                    });
                }
            }
        }
    }

    // Phase 3: Check if a clue sets up a finesse that isn't followed
    // TODO: Track "pending finesses" across multiple turns to handle delayed finesses
    // TODO: Recognize when a player legitimately delays a finesse for a more urgent action (e.g., save clue)
    // TODO: Only flag MissedFinesse when the opportunity permanently passes (card discarded, someone else plays it)
    // TODO: Support detection of layered finesses, double finesses, and hidden finesses
    private void CheckFinesseSetup(List<RuleViolation> violations, GameAction action, GameState state, GameExport game, int playerIndex, string player, int turn, int actionIndex, List<GameState> allStates)
    {
        var targetPlayer = action.Target;
        if (targetPlayer < 0 || targetPlayer >= state.Hands.Count) return;

        var targetHand = state.Hands[targetPlayer];
        var numPlayers = game.Players.Count;

        // Find the "focus" card of this clue (newest touched card, or chop if chop was touched)
        var touchedCards = new List<(CardInHand card, int index)>();
        for (int i = 0; i < targetHand.Count; i++)
        {
            var card = targetHand[i];
            bool touched = false;
            if (action.Type == ActionType.ColorClue && card.SuitIndex == action.Value)
                touched = true;
            else if (action.Type == ActionType.RankClue && card.Rank == action.Value)
                touched = true;

            if (touched && !card.HasAnyClue) // Only consider newly clued cards
            {
                touchedCards.Add((card, i));
            }
        }

        if (touchedCards.Count == 0) return;

        // Focus is typically the newest newly-touched card (highest index)
        var focusCard = touchedCards.OrderByDescending(t => t.index).First().card;

        // Check if focus card is directly playable
        if (IsCardPlayable(focusCard, state)) return; // Not a finesse, just a play clue

        // Check if focus card WOULD be playable if a specific card was played first
        var neededRank = state.PlayStacks[focusCard.SuitIndex] + 1;
        if (focusCard.Rank != neededRank + 1) return; // Not a simple one-away finesse

        // This looks like a finesse! Check if someone between clue-giver and target has the needed card
        // in their finesse position (newest card)
        var finesseCard = new { SuitIndex = focusCard.SuitIndex, Rank = neededRank };

        // Find who should have the finesse card
        int finessePlayerIndex = -1;
        for (int offset = 1; offset < numPlayers; offset++)
        {
            int checkPlayer = (playerIndex + offset) % numPlayers;
            if (checkPlayer == targetPlayer) break; // Stop before reaching target

            var checkHand = state.Hands[checkPlayer];
            if (checkHand.Count == 0) continue;

            // Finesse position is the newest card (last in hand)
            // TODO: Finesse position should be "leftmost unclued card" - if newest card is clued,
            //       finesse position shifts to the next unclued slot
            var finessePos = checkHand[checkHand.Count - 1];
            if (finessePos.SuitIndex == finesseCard.SuitIndex && finessePos.Rank == finesseCard.Rank)
            {
                finessePlayerIndex = checkPlayer;
                break;
            }
        }

        if (finessePlayerIndex == -1) return; // No finesse detected

        // Now check if the finesse was followed - look at the next action
        // TODO: Look ahead multiple turns instead of just the immediate next action
        // TODO: Track if the finesse is eventually completed on a later turn
        int nextActionIndex = actionIndex + 1;
        if (nextActionIndex >= game.Actions.Count) return;

        var nextAction = game.Actions[nextActionIndex];
        int nextPlayerIndex = nextActionIndex % numPlayers;

        // Check if the finesse player was the next player
        // TODO: Handle case where other players act between clue-giver and finesse player
        if (nextPlayerIndex != finessePlayerIndex) return; // Someone else acted first

        // Check if they blind-played from finesse position
        // TODO: Verify they played specifically from finesse position, not just any play
        // TODO: Check if their non-play action was justified (e.g., urgent save clue)
        if (nextAction.Type != ActionType.Play)
        {
            // They didn't play - missed finesse!
            var suitName = GetSuitName(focusCard.SuitIndex);
            violations.Add(new RuleViolation
            {
                Turn = turn,
                Player = game.Players[finessePlayerIndex],
                Type = ViolationType.MissedFinesse,
                Severity = Severity.Info,
                Description = $"Possible finesse for {suitName} {neededRank} was set up but not followed"
            });
        }
    }

    // Helper: Check if a card is currently playable
    private bool IsCardPlayable(CardInHand card, GameState state)
    {
        return state.PlayStacks[card.SuitIndex] == card.Rank - 1;
    }

    public AnalysisSummary CreateSummary(List<RuleViolation> violations)
    {
        return new AnalysisSummary
        {
            TotalViolations = violations.Count,
            BySeverity = violations.GroupBy(v => v.Severity)
                .ToDictionary(g => g.Key, g => g.Count()),
            ByType = violations.GroupBy(v => v.Type)
                .ToDictionary(g => g.Key, g => g.Count())
        };
    }
}
