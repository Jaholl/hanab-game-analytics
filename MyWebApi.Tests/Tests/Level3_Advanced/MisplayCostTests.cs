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
}
