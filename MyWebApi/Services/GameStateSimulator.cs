using MyWebApi.Models;

namespace MyWebApi.Services;

public class GameStateSimulator
{
    public List<GameState> SimulateGame(GameExport game)
    {
        var states = new List<GameState>();
        var numPlayers = game.Players.Count;
        var handSize = numPlayers <= 3 ? 5 : 4;

        // Initialize game state
        var state = new GameState
        {
            Turn = 0,
            CurrentPlayer = 0,
            Hands = new List<List<CardInHand>>(),
            PlayStacks = new int[5],
            DiscardPile = new List<DeckCard>(),
            ClueTokens = 8,
            Strikes = 0,
            DeckIndex = 0
        };

        // Initialize empty hands
        for (int i = 0; i < numPlayers; i++)
        {
            state.Hands.Add(new List<CardInHand>());
        }

        // Deal initial hands (all cards to player 0, then player 1, etc.)
        for (int player = 0; player < numPlayers; player++)
        {
            for (int card = 0; card < handSize; card++)
            {
                if (state.DeckIndex < game.Deck.Count)
                {
                    var deckCard = game.Deck[state.DeckIndex];
                    state.Hands[player].Add(new CardInHand
                    {
                        SuitIndex = deckCard.SuitIndex,
                        Rank = deckCard.Rank,
                        DeckIndex = state.DeckIndex
                    });
                    state.DeckIndex++;
                }
            }
        }

        // Save initial state
        states.Add(CloneState(state));

        // Process each action
        foreach (var action in game.Actions)
        {
            state.Turn++;
            ProcessAction(state, action, game);
            states.Add(CloneState(state));
            state.CurrentPlayer = (state.CurrentPlayer + 1) % numPlayers;
        }

        return states;
    }

    private void ProcessAction(GameState state, GameAction action, GameExport game)
    {
        switch (action.Type)
        {
            case ActionType.Play:
                ProcessPlay(state, action, game);
                break;
            case ActionType.Discard:
                ProcessDiscard(state, action, game);
                break;
            case ActionType.ColorClue:
                ProcessColorClue(state, action);
                break;
            case ActionType.RankClue:
                ProcessRankClue(state, action);
                break;
        }
    }

    private void ProcessPlay(GameState state, GameAction action, GameExport game)
    {
        var hand = state.Hands[state.CurrentPlayer];
        var deckIndex = action.Target;

        // Find the card in hand by its deck index
        var cardIndex = hand.FindIndex(c => c.DeckIndex == deckIndex);
        if (cardIndex < 0)
        {
            // Card not found - this shouldn't happen in valid game data
            return;
        }

        var card = hand[cardIndex];

        // Check if play is valid
        if (state.PlayStacks[card.SuitIndex] == card.Rank - 1)
        {
            // Successful play
            state.PlayStacks[card.SuitIndex] = card.Rank;

            // Playing a 5 gives back a clue token
            if (card.Rank == 5 && state.ClueTokens < 8)
            {
                state.ClueTokens++;
            }
        }
        else
        {
            // Misplay - goes to discard pile and adds a strike
            state.DiscardPile.Add(new DeckCard { SuitIndex = card.SuitIndex, Rank = card.Rank });
            state.Strikes++;
        }

        // Remove card from hand and draw new card
        hand.RemoveAt(cardIndex);
        DrawCard(state, game);
    }

    private void ProcessDiscard(GameState state, GameAction action, GameExport game)
    {
        var hand = state.Hands[state.CurrentPlayer];
        var deckIndex = action.Target;

        // Find the card in hand by its deck index
        var cardIndex = hand.FindIndex(c => c.DeckIndex == deckIndex);
        if (cardIndex < 0) return;

        var card = hand[cardIndex];

        // Add to discard pile
        state.DiscardPile.Add(new DeckCard { SuitIndex = card.SuitIndex, Rank = card.Rank });

        // Gain a clue token (max 8)
        if (state.ClueTokens < 8)
        {
            state.ClueTokens++;
        }

        // Remove card from hand and draw new card
        hand.RemoveAt(cardIndex);
        DrawCard(state, game);
    }

    private void ProcessColorClue(GameState state, GameAction action)
    {
        var targetPlayer = action.Target;
        var color = action.Value;

        if (targetPlayer < 0 || targetPlayer >= state.Hands.Count) return;

        // Mark all cards of that color as clued
        foreach (var card in state.Hands[targetPlayer])
        {
            if (card.SuitIndex == color)
            {
                card.ClueColors[color] = true;
            }
        }

        state.ClueTokens--;
    }

    private void ProcessRankClue(GameState state, GameAction action)
    {
        var targetPlayer = action.Target;
        var rank = action.Value; // 1-5

        if (targetPlayer < 0 || targetPlayer >= state.Hands.Count) return;

        // Mark all cards of that rank as clued
        foreach (var card in state.Hands[targetPlayer])
        {
            if (card.Rank == rank)
            {
                card.ClueRanks[rank - 1] = true;
            }
        }

        state.ClueTokens--;
    }

    private void DrawCard(GameState state, GameExport game)
    {
        if (state.DeckIndex >= game.Deck.Count) return;

        var deckCard = game.Deck[state.DeckIndex];
        state.Hands[state.CurrentPlayer].Add(new CardInHand
        {
            SuitIndex = deckCard.SuitIndex,
            Rank = deckCard.Rank,
            DeckIndex = state.DeckIndex
        });
        state.DeckIndex++;
    }

    private GameState CloneState(GameState state)
    {
        return new GameState
        {
            Turn = state.Turn,
            CurrentPlayer = state.CurrentPlayer,
            Hands = state.Hands.Select(hand =>
                hand.Select(card => new CardInHand
                {
                    SuitIndex = card.SuitIndex,
                    Rank = card.Rank,
                    DeckIndex = card.DeckIndex,
                    ClueColors = (bool[])card.ClueColors.Clone(),
                    ClueRanks = (bool[])card.ClueRanks.Clone()
                }).ToList()
            ).ToList(),
            PlayStacks = (int[])state.PlayStacks.Clone(),
            DiscardPile = state.DiscardPile.Select(c => new DeckCard { SuitIndex = c.SuitIndex, Rank = c.Rank }).ToList(),
            ClueTokens = state.ClueTokens,
            Strikes = state.Strikes,
            DeckIndex = state.DeckIndex
        };
    }
}
