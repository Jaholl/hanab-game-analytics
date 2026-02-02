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

    private readonly AnalyzerOptions _options;

    public RuleAnalyzer() : this(null) { }

    public RuleAnalyzer(AnalyzerOptions? options)
    {
        _options = options ?? new AnalyzerOptions();
    }

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
                    CheckMissedSave(violations, action, stateBefore, game, currentPlayerIndex, currentPlayer, i + 1, ActionType.Play);
                    break;
                case ActionType.Discard:
                    CheckIllegalDiscard(violations, stateBefore, currentPlayer, i + 1);
                    CheckBadDiscard(violations, action, stateBefore, game, currentPlayerIndex, currentPlayer, i + 1);
                    CheckMissedSave(violations, action, stateBefore, game, currentPlayerIndex, currentPlayer, i + 1, ActionType.Discard);
                    CheckMissedPrompt(violations, action, stateBefore, currentPlayerIndex, currentPlayer, i + 1);
                    CheckDoubleDiscardAvoidance(violations, action, stateBefore, game, currentPlayerIndex, currentPlayer, i + 1, i, states);
                    break;
                case ActionType.ColorClue:
                case ActionType.RankClue:
                    CheckGoodTouch(violations, action, stateBefore, game, currentPlayerIndex, currentPlayer, i + 1);
                    CheckMCVP(violations, action, stateBefore, game, currentPlayerIndex, currentPlayer, i + 1);
                    CheckFinesseSetup(violations, action, stateBefore, game, currentPlayerIndex, currentPlayer, i + 1, i, states);
                    CheckMissedSave(violations, action, stateBefore, game, currentPlayerIndex, currentPlayer, i + 1, action.Type);
                    break;
            }
        }

        // Filter violations by the enabled level
        return violations
            .Where(v => _options.EnabledViolations.Contains(v.Type))
            .ToList();
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
            // Only flag if the card is still needed (hasn't been played) and suit isn't dead
            if (state.PlayStacks[card.SuitIndex] < card.Rank && !IsSuitDead(card.SuitIndex, card.Rank, state))
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

    private void CheckIllegalDiscard(List<RuleViolation> violations, GameState state, string player, int turn)
    {
        if (state.ClueTokens >= 8)
        {
            violations.Add(new RuleViolation
            {
                Turn = turn,
                Player = player,
                Type = ViolationType.IllegalDiscard,
                Severity = Severity.Critical,
                Description = "Discarded at 8 clue tokens - must clue or play instead"
            });
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

            // Check if card is "future trash" (suit is dead)
            if (IsSuitDead(card.SuitIndex, card.Rank, state))
            {
                violations.Add(new RuleViolation
                {
                    Turn = turn,
                    Player = player,
                    Type = ViolationType.GoodTouchViolation,
                    Severity = Severity.Warning,
                    Description = $"Clue touched {suitName} {card.Rank} which can never be played (suit is dead)"
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

        // Check for duplicates within same hand
        var sameHandDupes = touchedCards
            .GroupBy(c => (c.SuitIndex, c.Rank))
            .Where(g => g.Count() > 1);

        foreach (var dupeGroup in sameHandDupes)
        {
            var suitName = GetSuitName(dupeGroup.Key.SuitIndex);
            violations.Add(new RuleViolation
            {
                Turn = turn,
                Player = player,
                Type = ViolationType.GoodTouchViolation,
                Severity = Severity.Warning,
                Description = $"Clue touched duplicate {suitName} {dupeGroup.Key.Rank} in same hand"
            });
        }
    }

    // Phase 2: MCVP - Minimum Clue Value Principle - clue must touch at least one new card
    // Exception (Level 2+): "Tempo clues" that re-touch playable cards are valid (they signal "play now")
    private void CheckMCVP(List<RuleViolation> violations, GameAction action, GameState state, GameExport game, int playerIndex, string player, int turn)
    {
        var targetPlayer = action.Target;
        if (targetPlayer < 0 || targetPlayer >= state.Hands.Count) return;

        var targetHand = state.Hands[targetPlayer];
        var newCardsTouched = 0;
        var touchedPlayableCards = 0;

        // Count how many new (previously unclued) cards are touched
        // Also check if any touched cards are already clued AND playable (tempo clue)
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

            if (isTouched)
            {
                if (!card.HasAnyClue)
                {
                    newCardsTouched++;
                }
                else if (IsCardPlayable(card, state))
                {
                    // Already clued card that is playable - this could be a tempo clue (Level 2+)
                    touchedPlayableCards++;
                }
            }
        }

        // Not a violation if new cards were touched (normal clue)
        if (newCardsTouched > 0) return;

        // At Level 2+, re-touching a playable card is a tempo clue, not a violation
        bool isTempoClueLevel = _options.Level >= ConventionLevel.Level2_Intermediate;
        if (isTempoClueLevel && touchedPlayableCards > 0) return;

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

    // Helper: Get cards touched by a clue
    private List<CardInHand> GetTouchedCards(List<CardInHand> hand, GameAction action)
    {
        if (action.Type == ActionType.ColorClue)
        {
            return hand.Where(c => c.SuitIndex == action.Value).ToList();
        }
        else if (action.Type == ActionType.RankClue)
        {
            return hand.Where(c => c.Rank == action.Value).ToList();
        }
        return new List<CardInHand>();
    }

    // Phase 2: Missed Save - taking an action when teammate has critical on chop
    private void CheckMissedSave(List<RuleViolation> violations, GameAction action, GameState state, GameExport game, int playerIndex, string player, int turn, ActionType actionType)
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

            // For clue actions, check if this clue is saving THIS player's chop
            if ((actionType == ActionType.ColorClue || actionType == ActionType.RankClue) && action.Target == p)
            {
                // The clue is targeting this player - check if it touches their chop card
                var touchedCards = GetTouchedCards(state.Hands[p], action);
                if (touchedCards.Any(c => c.DeckIndex == chopCard.DeckIndex))
                {
                    // This clue is saving their chop, skip MissedSave check for this player
                    continue;
                }
            }

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
                string actionDescription = actionType switch
                {
                    ActionType.Play => "Played instead of saving",
                    ActionType.ColorClue or ActionType.RankClue => "Gave a clue instead of saving",
                    _ => "Discarded instead of saving"
                };

                violations.Add(new RuleViolation
                {
                    Turn = turn,
                    Player = player,
                    Type = ViolationType.MissedSave,
                    Severity = Severity.Warning,
                    Description = $"{actionDescription} {game.Players[p]}'s {suitName} {chopCard.Rank} on chop ({saveReason})"
                });
            }
        }
    }

    // Phase 2: Double Discard Avoidance - discarding from chop after previous player discarded from chop
    private void CheckDoubleDiscardAvoidance(
        List<RuleViolation> violations,
        GameAction action,
        GameState state,
        GameExport game,
        int playerIndex,
        string player,
        int turn,
        int actionIndex,
        List<GameState> allStates)
    {
        if (actionIndex == 0) return;

        var previousAction = game.Actions[actionIndex - 1];
        if (previousAction.Type != ActionType.Discard) return;

        var previousState = allStates[actionIndex - 1];
        var numPlayers = game.Players.Count;
        var previousPlayerIndex = (playerIndex - 1 + numPlayers) % numPlayers;

        // Check if previous player discarded from chop
        var previousHand = previousState.Hands[previousPlayerIndex];
        var previousChopIndex = GetChopIndex(previousHand);
        if (!previousChopIndex.HasValue) return;

        var previousDiscardedCard = previousHand.FirstOrDefault(c => c.DeckIndex == previousAction.Target);
        if (previousDiscardedCard == null) return;

        var previousDiscardIndex = previousHand.FindIndex(c => c.DeckIndex == previousAction.Target);
        if (previousDiscardIndex != previousChopIndex.Value) return;

        // Current player also discarded - check if it's from chop
        var currentHand = state.Hands[playerIndex];
        var currentChopIndex = GetChopIndex(currentHand);
        if (!currentChopIndex.HasValue) return;

        var currentDiscardedCard = currentHand.FirstOrDefault(c => c.DeckIndex == action.Target);
        if (currentDiscardedCard == null) return;

        var currentDiscardIndex = currentHand.FindIndex(c => c.DeckIndex == action.Target);
        if (currentDiscardIndex != currentChopIndex.Value) return;

        // Check if current discard was safe (trash)
        if (IsCardTrash(currentDiscardedCard, state)) return;

        // This is a DDA violation
        var suitName = GetSuitName(currentDiscardedCard.SuitIndex);
        violations.Add(new RuleViolation
        {
            Turn = turn,
            Player = player,
            Type = ViolationType.DoubleDiscardAvoidance,
            Severity = Severity.Warning,
            Description = $"Discarded {suitName} {currentDiscardedCard.Rank} from chop after {game.Players[previousPlayerIndex]} discarded from chop - should avoid double discard"
        });
    }

    // Get the chop index (oldest unclued card position)
    private int? GetChopIndex(List<CardInHand> hand)
    {
        for (int i = 0; i < hand.Count; i++)
        {
            if (!hand[i].HasAnyClue)
            {
                return i;
            }
        }
        return null;
    }

    // Check if a card is trash (already played or suit is dead)
    private bool IsCardTrash(CardInHand card, GameState state)
    {
        // Already played
        if (state.PlayStacks[card.SuitIndex] >= card.Rank)
            return true;

        // Suit is dead
        if (IsSuitDead(card.SuitIndex, card.Rank, state))
            return true;

        return false;
    }

    // Check if a suit is dead at a specific rank
    private bool IsSuitDead(int suitIndex, int targetRank, GameState state)
    {
        var currentStack = state.PlayStacks[suitIndex];

        // Check each rank between current stack and target
        for (int rank = currentStack + 1; rank < targetRank; rank++)
        {
            var totalCopies = CardCopiesPerRank[rank];
            var discardedCount = state.DiscardPile.Count(c => c.SuitIndex == suitIndex && c.Rank == rank);

            // If all copies of this intermediate rank are discarded, suit is dead
            if (discardedCount >= totalCopies)
                return true;
        }

        return false;
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
            // Check if it was the "finesse position" (newest unclued card)
            var finessePositionIndex = GetFinessePositionIndex(hand);
            bool isFinessePosition = finessePositionIndex.HasValue && cardIndex == finessePositionIndex.Value;

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

            // Finesse position is the newest unclued card
            var finessePosIndex = GetFinessePositionIndex(checkHand);
            if (!finessePosIndex.HasValue) continue;

            var finessePos = checkHand[finessePosIndex.Value];
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

    // Helper: Get the finesse position index (newest unclued card)
    private int? GetFinessePositionIndex(List<CardInHand> hand)
    {
        // Finesse position is the "newest unclued card"
        // In this representation: cards are dealt/drawn with Add(), so newest is at highest index
        for (int i = hand.Count - 1; i >= 0; i--)
        {
            if (!hand[i].HasAnyClue)
                return i;
        }
        return null;
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
