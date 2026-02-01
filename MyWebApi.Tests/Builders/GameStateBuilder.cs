using MyWebApi.Models;

namespace MyWebApi.Tests.Builders;

/// <summary>
/// Builder for creating specific game states directly (without simulation).
/// Useful for unit testing helper functions or testing specific edge cases.
/// </summary>
public class GameStateBuilder
{
    private int _turn = 1;
    private int _currentPlayer = 0;
    private readonly List<List<CardInHand>> _hands = new();
    private int[] _playStacks = new int[5];
    private readonly List<DeckCard> _discardPile = new();
    private int _clueTokens = 8;
    private int _strikes = 0;
    private int _deckIndex = 0;

    public static GameStateBuilder Create() => new();

    public GameStateBuilder WithTurn(int turn)
    {
        _turn = turn;
        return this;
    }

    public GameStateBuilder WithCurrentPlayer(int playerIndex)
    {
        _currentPlayer = playerIndex;
        return this;
    }

    /// <summary>
    /// Adds a hand for a player. Cards should be in order from oldest (index 0) to newest.
    /// </summary>
    public GameStateBuilder WithHand(params CardInHand[] cards)
    {
        _hands.Add(new List<CardInHand>(cards));
        return this;
    }

    /// <summary>
    /// Adds a hand using string notation. Format: "R1,Y2,G3,B4,P5" (all unclued)
    /// Use * suffix for clued cards: "R1*,Y2,G3" means R1 is clued
    /// </summary>
    public GameStateBuilder WithHand(string handNotation, int startDeckIndex = 0)
    {
        var cards = new List<CardInHand>();
        var cardNotations = handNotation.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        int deckIdx = startDeckIndex;
        foreach (var notation in cardNotations)
        {
            var isClued = notation.EndsWith('*');
            var cardStr = isClued ? notation.TrimEnd('*') : notation;
            var deckCard = CardBuilder.Parse(cardStr);

            var card = new CardInHand
            {
                SuitIndex = deckCard.SuitIndex,
                Rank = deckCard.Rank,
                DeckIndex = deckIdx++,
                ClueColors = new bool[5],
                ClueRanks = new bool[5]
            };

            if (isClued)
            {
                card.ClueColors[deckCard.SuitIndex] = true;
            }

            cards.Add(card);
        }

        _hands.Add(cards);
        return this;
    }

    public GameStateBuilder WithPlayStacks(params int[] stacks)
    {
        if (stacks.Length != 5)
            throw new ArgumentException("Must provide exactly 5 stack values");
        _playStacks = stacks;
        return this;
    }

    /// <summary>
    /// Sets play stacks using notation: "R2,Y1,G0,B3,P0" meaning Red at 2, Yellow at 1, etc.
    /// </summary>
    public GameStateBuilder WithPlayStacks(string stackNotation)
    {
        var parts = stackNotation.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 5)
            throw new ArgumentException("Must provide exactly 5 stack values");

        for (int i = 0; i < 5; i++)
        {
            var part = parts[i];
            var value = int.Parse(part.Substring(1));
            _playStacks[i] = value;
        }
        return this;
    }

    public GameStateBuilder WithDiscardPile(params DeckCard[] cards)
    {
        _discardPile.AddRange(cards);
        return this;
    }

    public GameStateBuilder WithClueTokens(int clues)
    {
        _clueTokens = clues;
        return this;
    }

    public GameStateBuilder WithStrikes(int strikes)
    {
        _strikes = strikes;
        return this;
    }

    public GameStateBuilder WithDeckIndex(int index)
    {
        _deckIndex = index;
        return this;
    }

    public GameState Build()
    {
        return new GameState
        {
            Turn = _turn,
            CurrentPlayer = _currentPlayer,
            Hands = new List<List<CardInHand>>(_hands),
            PlayStacks = (int[])_playStacks.Clone(),
            DiscardPile = new List<DeckCard>(_discardPile),
            ClueTokens = _clueTokens,
            Strikes = _strikes,
            DeckIndex = _deckIndex
        };
    }
}
