using FluentAssertions;
using MyWebApi.Models;
using MyWebApi.Services;
using MyWebApi.Tests.Builders;
using MyWebApi.Tests.Helpers;
using Xunit;

namespace MyWebApi.Tests.Tests.Level3_Advanced;

/// <summary>
/// Tests for Misplay Cost convention.
///
/// H-Group Rule: Spending 1 clue to prevent 1 misplay is always worthwhile.
/// If a player can see that a teammate is about to misplay and has a clue
/// token available, they should spend it to prevent the misplay.
/// Not doing so is a MisplayCostViolation.
/// </summary>
public class MisplayCostTests
{
    [Fact]
    public void CouldPreventMisplay_ButDidnt_CreatesViolation()
    {
        // 3-player, hand size 5. Turn order: Alice(0), Bob(1), Charlie(2), ...
        // Alice(0-4): R2,Y1,B1,G1,P1
        // Bob(5-9): R3,Y2,B2,G2,P2   - R3 NOT playable (stacks at 0)
        // Charlie(10-14): Y3,B3,G3,P3,R4
        //
        // Turn 1 (Alice): Clue Bob Red (tells him about R3)
        // Turn 2 (Bob): Bob plays R3 -> MISPLAY
        // But wait - there's no one between Alice and Bob to prevent it.
        //
        // Better: Alice clues Bob, Charlie can prevent, Alice acts instead.
        // Turn 1 (Alice): Clue Bob Red
        // Turn 2 (Bob): Discards (doesn't misplay yet)
        // Turn 3 (Charlie): Discards instead of giving fix clue
        // Turn 4 (Alice): Something
        // Turn 5 (Bob): Plays R3 -> misplay
        //
        // Actually, simplest: Alice gives play clue to Bob, then before Bob acts,
        // the player before Bob (who can see it's wrong) fails to fix it.
        //
        // Let's use: Alice clues Bob, then it's Bob's turn to play.
        // The previous player (Alice) had a chance to prevent but clued instead.
        // Actually the MisplayCostChecker checks if the IMMEDIATELY PREVIOUS player
        // could have given a clue instead. So:
        //
        // Turn 1 (Alice): Clue Bob Red -> Bob thinks R3 is playable
        // Turn 2 (Bob): Something
        // Turn 3 (Charlie): Discards instead of fixing
        // Turn 4 (Alice): Discards
        // Turn 5 (Bob): Plays R3 -> misplay!
        // At turn 4, Alice discards - the checker sees next action is Bob misplaying.
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob", "Charlie")
            .WithDeck(
                "R2,Y1,B1,G1,P1," +   // Alice
                "R3,Y2,B2,G2,P2," +   // Bob - R3 not playable
                "Y3,B3,G3,P3,R4," +   // Charlie
                "R5,Y4,B4")
            .AtAdvancedLevel()
            .ColorClue(1, "Red")    // Alice clues Bob Red -> touches R3
            .Discard(9)             // Bob discards P2 from chop
            .Discard(14)            // Charlie discards R4 from chop
            .Discard(4)             // Alice discards P1 - could have given fix clue!
            .Play(5)                // Bob plays R3 -> MISPLAY
            .BuildAndAnalyze();

        violations.Should().ContainViolation(ViolationType.MisplayCostViolation);
        violations.Should().ContainViolationForPlayer(ViolationType.MisplayCostViolation, "Alice");
    }

    [Fact]
    public void PreventedMisplayWithClue_NoViolation()
    {
        // Alice correctly gives a clue to prevent Bob's misplay
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob", "Charlie")
            .WithDeck(
                "R2,Y1,B1,G1,P1," +
                "R3,Y2,B2,G2,P2," +
                "Y3,B3,G3,P3,R4," +
                "R5,Y4,B4")
            .AtAdvancedLevel()
            .ColorClue(1, "Red")    // Alice clues Bob Red
            .Discard(9)             // Bob discards
            .Discard(14)            // Charlie discards
            .RankClue(1, 3)         // Alice gives fix clue (tells Bob it's 3, not playable)
            .BuildAndAnalyze();

        violations.Should().NotContainViolation(ViolationType.MisplayCostViolation);
    }

    [Fact]
    public void NextPlayerDoesntMisplay_NoCostViolation()
    {
        // Alice discards but next player (Bob) doesn't misplay
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,Y1,B1,G1,P1, R2,Y2,B2,G2,P2, R3,Y3")
            .AtAdvancedLevel()
            .Discard(0)   // Alice discards
            .Discard(5)   // Bob discards (no misplay)
            .BuildAndAnalyze();

        violations.Should().NotContainViolation(ViolationType.MisplayCostViolation);
    }

    [Fact]
    public void OnlyAppliesAtLevel3()
    {
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob", "Charlie")
            .WithDeck(
                "R2,Y1,B1,G1,P1," +
                "R3,Y2,B2,G2,P2," +
                "Y3,B3,G3,P3,R4," +
                "R5,Y4,B4")
            .AtBeginnerLevel()
            .ColorClue(1, "Red")
            .Discard(9)
            .Discard(14)
            .Discard(4)
            .Play(5)
            .BuildAndAnalyze();

        violations.Should().NotContainViolation(ViolationType.MisplayCostViolation);
    }

    [Fact]
    public void ZeroClueTokens_CannotPrevent_NoViolation()
    {
        // Edge case: Player has 0 clue tokens before their action, so they
        // cannot give a clue to prevent the next player's misplay.
        // The checker correctly returns early when ClueTokens == 0.
        //
        // 2-player, hand size 5.
        // Alice(0-4): R1,Y1,B1,G1,P1
        // Bob(5-9): R3,Y2,B2,G2,P2 - R3 not playable (stacks at 0)
        // Draw: R4,Y3
        //
        // Use 8 clues first to reach 0, then test.
        // Actually, WithClueTokens(0) only modifies states[0].
        // After 8 clue actions we would be at 0 tokens. But we need specific
        // setup. Let us use a long sequence of clues to drain tokens.
        //
        // Better approach: 3-player game where many clues drain the token pool.
        // Alice(0-4): R1,Y1,B1,G1,P1
        // Bob(5-9): R3,Y2,B2,G2,P2
        // Charlie(10-14): Y3,B3,G3,P3,R4
        // Draw: R5,Y4,B4,G4,P4,R2,Y5,B5
        //
        // We need to give 8 clues total to reach 0, then have Alice discard
        // (which brings tokens to 1), then Bob misplays. But the check is on
        // StateBefore for Alice's discard, which would be 0. Perfect.
        //
        // Turns 1-8: Alternate clues between 3 players (8 clues = 0 tokens)
        // Turn 9 (action index 8): Alice discards (0 tokens before -> cannot clue)
        // Turn 10 (action index 9): Bob plays R3 -> misplay
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob", "Charlie")
            .WithDeck(
                "R1,Y1,B1,G1,P1," +
                "R3,Y2,B2,G2,P2," +
                "Y3,B3,G3,P3,R4," +
                "R5,Y4,B4,G4,P4,R2,Y5,B5")
            .AtAdvancedLevel()
            // Give clue to Bob about Red (Bob has R3)
            .ColorClue(1, "Red")       // T1 Alice: 7 tokens
            .RankClue(0, 1)              // T2 Bob: 6 tokens
            .RankClue(0, 1)              // T3 Charlie: 5 tokens
            .RankClue(1, 2)              // T4 Alice: 4 tokens
            .RankClue(2, 3)              // T5 Bob: 3 tokens
            .RankClue(0, 1)              // T6 Charlie: 2 tokens
            .RankClue(1, 2)              // T7 Alice: 1 token
            .RankClue(2, 3)              // T8 Bob: 0 tokens
            // Now 0 clue tokens. Alice cannot give a clue.
            .Discard(4)                  // T9 Alice: discards P1 (0 tokens before action)
            .Play(5)                     // T10 Bob: plays R3 -> MISPLAY
            .BuildAndAnalyze();

        // Alice had 0 clue tokens before discarding. She could not prevent misplay.
        violations.Should().NotContainViolationForPlayer(
            ViolationType.MisplayCostViolation, "Alice");
    }

    [Fact]
    public void NextPlayerMisplaysUncluedCard_NoViolation()
    {
        // Edge case: Bob misplays an UNCLUED card (blind play attempt).
        // The MisplayCost convention only applies to clued cards where
        // the player believes the card is playable. Blind plays are voluntary
        // risks, not something the previous player should prevent.
        //
        // 2-player, hand size 5.
        // Alice(0-4): R1,Y1,B1,G1,P1
        // Bob(5-9): R3,Y2,B2,G2,P2 - R3 unclued, not playable
        // Draw: R4,Y3
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,Y1,B1,G1,P1,R3,Y2,B2,G2,P2,R4,Y3")
            .AtAdvancedLevel()
            .Discard(4)           // T1 Alice: discards P1
            .Play(5)              // T2 Bob: plays R3 (unclued) -> MISPLAY
            .BuildAndAnalyze();

        // Bob's R3 is unclued. Checker should not flag Alice.
        violations.Should().NotContainViolation(ViolationType.MisplayCostViolation);
    }

    [Fact]
    public void NextPlayIsSuccessful_NoViolation()
    {
        // Edge case: The next player plays a card and it is SUCCESSFUL.
        // There is no misplay to prevent. Checker should not fire.
        //
        // 2-player, hand size 5.
        // Alice(0-4): R2,Y1,B1,G1,P1
        // Bob(5-9): R1,Y2,B2,G2,P2 - R1 IS playable
        // Draw: R3,Y3
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R2,Y1,B1,G1,P1,R1,Y2,B2,G2,P2,R3,Y3")
            .AtAdvancedLevel()
            .ColorClue(1, "Red")  // T1 Alice: clues Bob Red (touches R1)
            .Play(5)               // T2 Bob: plays R1 -> SUCCESS
            .BuildAndAnalyze();

        violations.Should().NotContainViolation(ViolationType.MisplayCostViolation);
    }

    [Fact]
    public void LastActionInGame_NoNextAction_NoViolation()
    {
        // Edge case: Current action is the last in the game. There is no
        // next action to check for misplay.
        //
        // 2-player, hand size 5.
        // Alice(0-4): R1,Y1,B1,G1,P1
        // Bob(5-9): R2,Y2,B2,G2,P2
        // Draw: R3,Y3
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,Y1,B1,G1,P1,R2,Y2,B2,G2,P2,R3,Y3")
            .AtAdvancedLevel()
            .Discard(4)           // T1 Alice: discards P1 (last action)
            .BuildAndAnalyze();

        violations.Should().NotContainViolation(ViolationType.MisplayCostViolation);
    }
}
