namespace MyWebApi.Models;

public class GameState
{
    public int Turn { get; set; }
    public int CurrentPlayer { get; set; }
    public List<List<CardInHand>> Hands { get; set; } = new();
    public int[] PlayStacks { get; set; } = new int[5]; // Value played for each suit (0-5)
    public List<DeckCard> DiscardPile { get; set; } = new();
    public int ClueTokens { get; set; } = 8;
    public int Strikes { get; set; } = 0;
    public int DeckIndex { get; set; } = 0;
    public int Score => PlayStacks.Sum();
}

public class CardInHand
{
    public int SuitIndex { get; set; }
    public int Rank { get; set; }
    public int DeckIndex { get; set; }
    public bool[] ClueColors { get; set; } = new bool[5]; // Which colors have been clued
    public bool[] ClueRanks { get; set; } = new bool[5];  // Which ranks have been clued (1-5)

    public bool HasAnyClue => ClueColors.Any(c => c) || ClueRanks.Any(r => r);
}
