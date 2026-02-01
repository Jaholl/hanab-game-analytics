using MyWebApi.Models;

namespace MyWebApi.Tests.Builders;

/// <summary>
/// Static helpers for creating cards in tests.
/// </summary>
public static class CardBuilder
{
    // Suit indices
    public const int Red = 0;
    public const int Yellow = 1;
    public const int Green = 2;
    public const int Blue = 3;
    public const int Purple = 4;

    /// <summary>
    /// Creates a DeckCard with the specified suit and rank.
    /// </summary>
    public static DeckCard Card(int suitIndex, int rank)
    {
        return new DeckCard { SuitIndex = suitIndex, Rank = rank };
    }

    /// <summary>
    /// Creates a DeckCard using short notation: "R1", "B5", etc.
    /// </summary>
    public static DeckCard Parse(string notation)
    {
        if (string.IsNullOrEmpty(notation) || notation.Length < 2)
            throw new ArgumentException($"Invalid card notation: {notation}");

        var suitChar = char.ToUpperInvariant(notation[0]);
        var suitIndex = suitChar switch
        {
            'R' => Red,
            'Y' => Yellow,
            'G' => Green,
            'B' => Blue,
            'P' => Purple,
            _ => throw new ArgumentException($"Unknown suit: {suitChar}")
        };

        if (!int.TryParse(notation.Substring(1), out var rank) || rank < 1 || rank > 5)
            throw new ArgumentException($"Invalid rank in notation: {notation}");

        return new DeckCard { SuitIndex = suitIndex, Rank = rank };
    }

    /// <summary>
    /// Creates a CardInHand with no clues.
    /// </summary>
    public static CardInHand UnCluedCard(int suitIndex, int rank, int deckIndex)
    {
        return new CardInHand
        {
            SuitIndex = suitIndex,
            Rank = rank,
            DeckIndex = deckIndex,
            ClueColors = new bool[5],
            ClueRanks = new bool[5]
        };
    }

    /// <summary>
    /// Creates a CardInHand with the specified clues.
    /// </summary>
    public static CardInHand CluedCard(int suitIndex, int rank, int deckIndex, bool colorClued = false, bool rankClued = false)
    {
        var card = new CardInHand
        {
            SuitIndex = suitIndex,
            Rank = rank,
            DeckIndex = deckIndex,
            ClueColors = new bool[5],
            ClueRanks = new bool[5]
        };

        if (colorClued)
            card.ClueColors[suitIndex] = true;

        if (rankClued)
            card.ClueRanks[rank - 1] = true;

        return card;
    }

    /// <summary>
    /// Creates a CardInHand with full information (both color and rank clued).
    /// </summary>
    public static CardInHand FullyCluedCard(int suitIndex, int rank, int deckIndex)
    {
        return CluedCard(suitIndex, rank, deckIndex, colorClued: true, rankClued: true);
    }

    // Convenience methods for each suit
    public static DeckCard R(int rank) => Card(Red, rank);
    public static DeckCard Y(int rank) => Card(Yellow, rank);
    public static DeckCard G(int rank) => Card(Green, rank);
    public static DeckCard B(int rank) => Card(Blue, rank);
    public static DeckCard P(int rank) => Card(Purple, rank);

    /// <summary>
    /// Creates a sequence of cards for a suit (useful for setting up stacks).
    /// E.g., RedSequence(1, 3) returns [R1, R2, R3]
    /// </summary>
    public static IEnumerable<DeckCard> Sequence(int suitIndex, int fromRank, int toRank)
    {
        for (int rank = fromRank; rank <= toRank; rank++)
        {
            yield return Card(suitIndex, rank);
        }
    }

    /// <summary>
    /// Creates cards for building a specific hand configuration.
    /// </summary>
    public static List<DeckCard> Hand(params string[] cardNotations)
    {
        return cardNotations.Select(Parse).ToList();
    }
}
