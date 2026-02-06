using MyWebApi.Models;

namespace MyWebApi.Services.Analysis.Helpers;

/// <summary>
/// Shared utility methods used by multiple checkers and trackers.
/// </summary>
public static class AnalysisHelpers
{
    public static readonly int[] CardCopiesPerRank = { 0, 3, 2, 2, 2, 1 };
    public static readonly string[] SuitNames = { "Red", "Yellow", "Green", "Blue", "Purple" };

    public static string GetSuitName(int suitIndex)
    {
        if (suitIndex >= 0 && suitIndex < SuitNames.Length)
            return SuitNames[suitIndex];
        return $"Suit{suitIndex}";
    }

    public static bool IsCardPlayable(CardInHand card, GameState state)
    {
        return state.PlayStacks[card.SuitIndex] == card.Rank - 1;
    }

    public static bool IsCardTrash(CardInHand card, GameState state)
    {
        if (state.PlayStacks[card.SuitIndex] >= card.Rank)
            return true;
        if (IsSuitDead(card.SuitIndex, card.Rank, state))
            return true;
        return false;
    }

    public static bool IsSuitDead(int suitIndex, int targetRank, GameState state)
    {
        var currentStack = state.PlayStacks[suitIndex];
        for (int rank = currentStack + 1; rank < targetRank; rank++)
        {
            var totalCopies = CardCopiesPerRank[rank];
            var discardedCount = state.DiscardPile.Count(c => c.SuitIndex == suitIndex && c.Rank == rank);
            if (discardedCount >= totalCopies)
                return true;
        }
        return false;
    }

    public static bool IsCardCritical(CardInHand card, GameState state, GameExport game)
    {
        var totalCopies = CardCopiesPerRank[card.Rank];
        var discardedCount = state.DiscardPile.Count(c => c.SuitIndex == card.SuitIndex && c.Rank == card.Rank);

        var inHandsCount = 0;
        foreach (var hand in state.Hands)
            inHandsCount += hand.Count(c => c.SuitIndex == card.SuitIndex && c.Rank == card.Rank);

        var inDeckCount = 0;
        for (int i = state.DeckIndex; i < game.Deck.Count; i++)
        {
            var deckCard = game.Deck[i];
            if (deckCard.SuitIndex == card.SuitIndex && deckCard.Rank == card.Rank)
                inDeckCount++;
        }

        var remainingCopies = inHandsCount + inDeckCount;
        return remainingCopies == 1;
    }

    public static bool IsCardCriticalForSave(CardInHand card, GameState state, GameExport game)
    {
        if (card.Rank == 5) return true;
        var totalCopies = CardCopiesPerRank[card.Rank];
        var discardedCount = state.DiscardPile.Count(c => c.SuitIndex == card.SuitIndex && c.Rank == card.Rank);
        return totalCopies - discardedCount == 1;
    }

    public static CardInHand? GetChopCard(List<CardInHand> hand)
    {
        for (int i = 0; i < hand.Count; i++)
        {
            if (!hand[i].HasAnyClue)
                return hand[i];
        }
        return null;
    }

    public static int? GetChopIndex(List<CardInHand> hand)
    {
        for (int i = 0; i < hand.Count; i++)
        {
            if (!hand[i].HasAnyClue)
                return i;
        }
        return null;
    }

    public static int? GetFinessePositionIndex(List<CardInHand> hand)
    {
        for (int i = hand.Count - 1; i >= 0; i--)
        {
            if (!hand[i].HasAnyClue)
                return i;
        }
        return null;
    }

    public static int CountVisibleCopies(CardInHand card, GameState state, GameExport game, int excludePlayer, int excludeDeckIndex = -1)
    {
        int count = 0;
        for (int p = 0; p < state.Hands.Count; p++)
        {
            if (p == excludePlayer) continue;
            count += state.Hands[p].Count(c =>
                c.SuitIndex == card.SuitIndex && c.Rank == card.Rank && c.DeckIndex != excludeDeckIndex);
        }
        return count;
    }

    public static List<CardInHand> GetTouchedCards(List<CardInHand> hand, GameAction action)
    {
        if (action.Type == ActionType.ColorClue)
            return hand.Where(c => c.SuitIndex == action.Value).ToList();
        if (action.Type == ActionType.RankClue)
            return hand.Where(c => c.Rank == action.Value).ToList();
        return new List<CardInHand>();
    }

    public static bool CheckForValidFinesse(ClueHistoryEntry clue, GameState stateAtClue, GameExport game, CardInHand targetCard)
    {
        var neededRank = stateAtClue.PlayStacks[targetCard.SuitIndex] + 1;
        if (targetCard.Rank <= neededRank) return false;

        var numPlayers = game.Players.Count;
        for (int offset = 1; offset < numPlayers; offset++)
        {
            int checkPlayer = (clue.ClueGiverIndex + offset) % numPlayers;
            if (checkPlayer == clue.TargetPlayerIndex) break;

            var checkHand = stateAtClue.Hands[checkPlayer];
            var finessePosIndex = GetFinessePositionIndex(checkHand);
            if (!finessePosIndex.HasValue) continue;

            var finesseCard = checkHand[finessePosIndex.Value];
            if (finesseCard.SuitIndex == targetCard.SuitIndex && finesseCard.Rank == neededRank)
                return true;
        }
        return false;
    }
}
