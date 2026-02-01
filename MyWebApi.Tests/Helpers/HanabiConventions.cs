using MyWebApi.Models;

namespace MyWebApi.Tests.Helpers;

/// <summary>
/// Reference implementation of H-Group Hanabi conventions.
/// These methods define CORRECT behavior per https://hanabi.github.io/
/// Tests use these to verify RuleAnalyzer correctness.
/// </summary>
public static class HanabiConventions
{
    // Standard 5-suit card counts per rank
    private static readonly int[] CardCopiesPerRank = { 0, 3, 2, 2, 2, 1 };

    #region Position Helpers

    /// <summary>
    /// Gets the chop index - the oldest unclued card position.
    /// In our representation, older cards are at lower indices.
    /// </summary>
    public static int? GetChopIndex(List<CardInHand> hand)
    {
        for (int i = 0; i < hand.Count; i++)
        {
            if (!hand[i].HasAnyClue)
            {
                return i;
            }
        }
        return null; // All cards are clued (no chop)
    }

    /// <summary>
    /// Gets the finesse position index - the leftmost (newest) unclued card.
    /// In our representation, newer cards are at higher indices.
    /// Finesse position is the highest index among unclued cards.
    /// </summary>
    public static int? GetFinessePositionIndex(List<CardInHand> hand)
    {
        for (int i = hand.Count - 1; i >= 0; i--)
        {
            if (!hand[i].HasAnyClue)
            {
                return i;
            }
        }
        return null; // All cards are clued (no finesse position)
    }

    /// <summary>
    /// Gets the chop card (oldest unclued).
    /// </summary>
    public static CardInHand? GetChopCard(List<CardInHand> hand)
    {
        var index = GetChopIndex(hand);
        return index.HasValue ? hand[index.Value] : null;
    }

    /// <summary>
    /// Gets the finesse position card (newest unclued).
    /// </summary>
    public static CardInHand? GetFinessePositionCard(List<CardInHand> hand)
    {
        var index = GetFinessePositionIndex(hand);
        return index.HasValue ? hand[index.Value] : null;
    }

    #endregion

    #region Card Analysis Helpers

    /// <summary>
    /// Gets all clued cards in prompt order (oldest first).
    /// Prompts work from oldest clued card to newest.
    /// </summary>
    public static List<CardInHand> GetPromptableCards(List<CardInHand> hand)
    {
        return hand.Where(c => c.HasAnyClue).ToList();
    }

    /// <summary>
    /// Gets all currently playable cards in a hand.
    /// </summary>
    public static List<CardInHand> GetPlayableCards(List<CardInHand> hand, int[] playStacks)
    {
        return hand.Where(c => IsPlayable(c, playStacks)).ToList();
    }

    /// <summary>
    /// Checks if a card is currently playable.
    /// </summary>
    public static bool IsPlayable(CardInHand card, int[] playStacks)
    {
        return playStacks[card.SuitIndex] == card.Rank - 1;
    }

    /// <summary>
    /// Checks if a card is currently playable.
    /// </summary>
    public static bool IsPlayable(DeckCard card, int[] playStacks)
    {
        return playStacks[card.SuitIndex] == card.Rank - 1;
    }

    /// <summary>
    /// Gets all critical cards across all hands.
    /// A card is critical if it's the last remaining copy.
    /// </summary>
    public static List<(int playerIndex, CardInHand card)> GetCriticalCards(GameState state, GameExport? game = null)
    {
        var criticals = new List<(int, CardInHand)>();

        for (int p = 0; p < state.Hands.Count; p++)
        {
            foreach (var card in state.Hands[p])
            {
                if (IsCritical(card, state, game))
                {
                    criticals.Add((p, card));
                }
            }
        }

        return criticals;
    }

    /// <summary>
    /// Checks if a card is critical (last remaining copy that's still needed).
    /// </summary>
    public static bool IsCritical(CardInHand card, GameState state, GameExport? game = null)
    {
        // If already played, not critical
        if (state.PlayStacks[card.SuitIndex] >= card.Rank)
            return false;

        // 5s are always critical (single copy)
        if (card.Rank == 5)
            return true;

        // Count discarded copies
        var discardedCount = state.DiscardPile.Count(c => c.SuitIndex == card.SuitIndex && c.Rank == card.Rank);

        // Check if this is the last copy
        var totalCopies = CardCopiesPerRank[card.Rank];
        return totalCopies - discardedCount == 1;
    }

    /// <summary>
    /// Gets all trash cards in a hand (already played or suit is dead).
    /// </summary>
    public static List<CardInHand> GetTrashCards(List<CardInHand> hand, GameState state)
    {
        return hand.Where(c => IsTrash(c, state)).ToList();
    }

    /// <summary>
    /// Checks if a card is trash (already played or suit is dead).
    /// </summary>
    public static bool IsTrash(CardInHand card, GameState state)
    {
        // Already played
        if (state.PlayStacks[card.SuitIndex] >= card.Rank)
            return true;

        // Suit is dead (can't reach this card's rank)
        if (IsSuitDead(card.SuitIndex, card.Rank, state))
            return true;

        return false;
    }

    /// <summary>
    /// Checks if a suit is dead at a specific rank (all copies of a needed lower rank are discarded).
    /// </summary>
    public static bool IsSuitDead(int suitIndex, int targetRank, GameState state)
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

    /// <summary>
    /// Checks if a suit is completely dead (can never score any more points).
    /// </summary>
    public static bool IsSuitCompletelyDead(int suitIndex, GameState state)
    {
        var currentStack = state.PlayStacks[suitIndex];
        if (currentStack >= 5) return false; // Already complete

        // Check the next needed card
        var nextRank = currentStack + 1;
        var totalCopies = CardCopiesPerRank[nextRank];
        var discardedCount = state.DiscardPile.Count(c => c.SuitIndex == suitIndex && c.Rank == nextRank);

        return discardedCount >= totalCopies;
    }

    #endregion

    #region Focus Calculation

    /// <summary>
    /// Calculates the focus card of a clue per H-Group conventions.
    /// Focus rules:
    /// 1. If chop is touched, chop is focus (save clue interpretation)
    /// 2. Otherwise, focus is the leftmost newly-touched card (newest among newly touched)
    /// </summary>
    public static CardInHand? GetFocusCard(List<CardInHand> hand, List<int> touchedIndices, int? chopIndex)
    {
        if (touchedIndices.Count == 0)
            return null;

        // Get only newly touched cards (previously unclued)
        var newlyTouched = touchedIndices.Where(i => !hand[i].HasAnyClue).ToList();

        if (newlyTouched.Count == 0)
        {
            // All touched cards were already clued - this violates MCVP
            // Focus would be... none? Or the newest touched?
            // Per conventions, this clue shouldn't exist, but if it does,
            // focus is typically the newest touched card
            return hand[touchedIndices.Max()];
        }

        // If chop was touched and chop is among newly touched, it's focus
        if (chopIndex.HasValue && newlyTouched.Contains(chopIndex.Value))
        {
            return hand[chopIndex.Value];
        }

        // Otherwise, focus is the newest (highest index) newly touched card
        var focusIndex = newlyTouched.Max();
        return hand[focusIndex];
    }

    /// <summary>
    /// Gets the indices of cards that would be touched by a color clue.
    /// </summary>
    public static List<int> GetColorTouchedIndices(List<CardInHand> hand, int colorIndex)
    {
        return hand.Select((c, i) => (card: c, index: i))
                   .Where(x => x.card.SuitIndex == colorIndex)
                   .Select(x => x.index)
                   .ToList();
    }

    /// <summary>
    /// Gets the indices of cards that would be touched by a rank clue.
    /// </summary>
    public static List<int> GetRankTouchedIndices(List<CardInHand> hand, int rank)
    {
        return hand.Select((c, i) => (card: c, index: i))
                   .Where(x => x.card.Rank == rank)
                   .Select(x => x.index)
                   .ToList();
    }

    #endregion

    #region Finesse Validation

    /// <summary>
    /// Validates if a finesse setup is legitimate.
    /// A finesse is valid when:
    /// 1. The focus card is exactly one-away from playable
    /// 2. A player between clue-giver and target has the connecting card in finesse position
    /// </summary>
    public static bool IsValidFinesseSetup(
        GameState state,
        int clueGiverIndex,
        int targetPlayerIndex,
        CardInHand focusCard,
        int numPlayers)
    {
        // Focus must be one-away from playable
        var neededRank = state.PlayStacks[focusCard.SuitIndex] + 1;
        if (focusCard.Rank != neededRank + 1)
            return false;

        // Find the connecting card in a player's finesse position
        var connectingCard = new { SuitIndex = focusCard.SuitIndex, Rank = neededRank };

        for (int offset = 1; offset < numPlayers; offset++)
        {
            int checkPlayer = (clueGiverIndex + offset) % numPlayers;
            if (checkPlayer == targetPlayerIndex)
                break; // Reached target before finding finesse

            var finessePos = GetFinessePositionCard(state.Hands[checkPlayer]);
            if (finessePos != null &&
                finessePos.SuitIndex == connectingCard.SuitIndex &&
                finessePos.Rank == connectingCard.Rank)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the player who should respond to a finesse (has connecting card in finesse position).
    /// Returns -1 if no valid finesse target found.
    /// </summary>
    public static int GetFinesseRespondingPlayer(
        GameState state,
        int clueGiverIndex,
        int targetPlayerIndex,
        int neededSuit,
        int neededRank,
        int numPlayers)
    {
        for (int offset = 1; offset < numPlayers; offset++)
        {
            int checkPlayer = (clueGiverIndex + offset) % numPlayers;
            if (checkPlayer == targetPlayerIndex)
                break;

            var finessePos = GetFinessePositionCard(state.Hands[checkPlayer]);
            if (finessePos != null &&
                finessePos.SuitIndex == neededSuit &&
                finessePos.Rank == neededRank)
            {
                return checkPlayer;
            }
        }

        return -1;
    }

    /// <summary>
    /// Checks if a play could be interpreted as a valid bluff.
    /// A bluff is when you clue a card that's one-away, and someone blind-plays
    /// a card that's NOT the connecting card, but IS playable.
    /// Bluffs only work from the player immediately after the clue-giver.
    /// </summary>
    public static bool IsValidBluff(
        GameState state,
        int clueGiverIndex,
        int targetPlayerIndex,
        CardInHand focusCard,
        CardInHand playedCard,
        int numPlayers)
    {
        // Focus must be one-away
        var neededRank = state.PlayStacks[focusCard.SuitIndex] + 1;
        if (focusCard.Rank != neededRank + 1)
            return false;

        // Bluffs only work on the player immediately after clue-giver
        int bluffTarget = (clueGiverIndex + 1) % numPlayers;
        if (bluffTarget == targetPlayerIndex)
            return false; // Can't bluff the clue target

        // The played card must NOT be the connecting card
        if (playedCard.SuitIndex == focusCard.SuitIndex && playedCard.Rank == neededRank)
            return false; // This was a real finesse, not a bluff

        // The played card must be playable (for the bluff to "work")
        return IsPlayable(playedCard, state.PlayStacks);
    }

    #endregion

    #region Game Rules

    /// <summary>
    /// Maximum clue tokens in a standard game.
    /// </summary>
    public const int MaxClueTokens = 8;

    /// <summary>
    /// Checks if discarding is legal (not at max clues).
    /// Discarding at 8 clue tokens is an illegal action.
    /// </summary>
    public static bool IsDiscardLegal(int currentClueTokens)
    {
        return currentClueTokens < MaxClueTokens;
    }

    /// <summary>
    /// Checks if the player has any legal discard (unclued cards to discard).
    /// Even at less than 8 clues, a player with all clued cards cannot discard
    /// (they must play or clue, but if also at 0 clues, must play something).
    /// </summary>
    public static bool HasLegalDiscard(List<CardInHand> hand)
    {
        return hand.Any(c => !c.HasAnyClue);
    }

    /// <summary>
    /// Checks if a player's hand is "locked" (all cards clued).
    /// </summary>
    public static bool IsHandLocked(List<CardInHand> hand)
    {
        return hand.All(c => c.HasAnyClue);
    }

    #endregion

    #region Double Discard Avoidance

    /// <summary>
    /// Checks if Double Discard Avoidance applies.
    /// DDA: If the previous player discarded from their chop, you should not discard from yours
    /// unless you have something safe to discard (trash or duplicate).
    /// </summary>
    public static bool IsDDAViolation(
        GameState currentState,
        GameAction currentAction,
        GameAction? previousAction,
        GameState? previousState,
        int currentPlayerIndex,
        int previousPlayerIndex)
    {
        // DDA only applies if current action is a discard
        if (currentAction.Type != ActionType.Discard)
            return false;

        // DDA only applies if previous action was a discard
        if (previousAction == null || previousAction.Type != ActionType.Discard)
            return false;

        // Check if previous player discarded from chop
        if (previousState == null)
            return false;

        var previousHand = previousState.Hands[previousPlayerIndex];
        var previousChopIndex = GetChopIndex(previousHand);
        if (!previousChopIndex.HasValue)
            return false;

        var previousDiscardedCard = previousHand.FirstOrDefault(c => c.DeckIndex == previousAction.Target);
        if (previousDiscardedCard == null)
            return false;

        // Was the previous discard from chop?
        var previousDiscardIndex = previousHand.FindIndex(c => c.DeckIndex == previousAction.Target);
        if (previousDiscardIndex != previousChopIndex.Value)
            return false;

        // Current player also discarded - is it a DDA violation?
        var currentHand = currentState.Hands[currentPlayerIndex];
        var currentChopIndex = GetChopIndex(currentHand);
        if (!currentChopIndex.HasValue)
            return false;

        var currentDiscardedCard = currentHand.FirstOrDefault(c => c.DeckIndex == currentAction.Target);
        if (currentDiscardedCard == null)
            return false;

        // Did current player discard from chop?
        var currentDiscardIndex = currentHand.FindIndex(c => c.DeckIndex == currentAction.Target);
        if (currentDiscardIndex != currentChopIndex.Value)
            return false;

        // Check if current discard was safe (trash or known duplicate)
        if (IsTrash(currentDiscardedCard, currentState))
            return false;

        // This is a DDA violation - discarded from chop when previous player also discarded from chop
        return true;
    }

    #endregion
}
