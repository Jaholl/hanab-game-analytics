using FluentAssertions;
using MyWebApi.Models;
using MyWebApi.Services;
using MyWebApi.Tests.Builders;
using MyWebApi.Tests.Helpers;
using Xunit;

namespace MyWebApi.Tests.Tests.Level3_Advanced;

/// <summary>
/// Tests for Fix Clue convention.
///
/// H-Group Rule: A fix clue is given to prevent misplay of a duplicate or
/// unplayable card that was previously clued. When a player could give a
/// fix clue to prevent a misplay but doesn't, it's a FixClue violation.
///
/// Key principle: Fix clues must be given as soon as possible once a
/// Good Touch Principle violation is discovered.
/// </summary>
public class FixClueTests
{
    [Fact]
    public void MissedFixClue_WhenTeammateAboutToMisplay_CreatesViolation()
    {
        // Setup: R1 is already played. Bob gets a "1" clue touching R1 (trash!).
        // Bob thinks R1 is playable. Alice should give fix clue but doesn't.
        //
        // 3-player: hand size 5.
        // Alice(0-4): R2,Y1,B1,G1,P1
        // Bob(5-9): R1,Y2,B2,G2,P2  - R1 is trash (already played)
        // Charlie(10-14): Y3,B3,G3,P3,R3
        //
        // First: play R1 from deck to get it on the stack, then clue Bob
        // Actually, we need a setup where R1 is already played.
        // Use initial plays to set up the stack.
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob", "Charlie")
            .WithDeck(
                "R1,Y1,B1,G1,P1," +   // Alice
                "R1,Y2,B2,G2,P2," +   // Bob - R1 at slot 0
                "Y3,B3,G3,P3,R3," +   // Charlie
                "R4,Y4,B4")
            .AtAdvancedLevel()
            // Alice plays R1 first to get it on the stack
            .Play(0)                    // Alice plays R1 -> Red stack = 1
            .RankClue(0, 1)             // Bob clues Alice's 1s (Y1,B1,G1,P1)
            .RankClue(1, 1)             // Charlie clues Bob's "1" -> touches R1 (trash!)
            // Now it's Alice's turn. Bob has clued R1 which is trash.
            // Bob will try to play it. Alice should give fix clue.
            .ColorClue(2, "Yellow")     // Alice gives unrelated clue instead of fixing Bob!
            .Play(5)                    // Bob plays R1 -> MISPLAY (R1 already played)
            .BuildAndAnalyze();

        violations.Should().ContainViolation(ViolationType.FixClue);
        violations.Should().ContainViolationForPlayer(ViolationType.FixClue, "Alice");
    }

    [Fact]
    public void FixClueGiven_NoViolation()
    {
        // Alice correctly gives a fix clue to prevent Bob's misplay
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob", "Charlie")
            .WithDeck(
                "R1,Y1,B1,G1,P1," +   // Alice
                "R1,Y2,B2,G2,P2," +   // Bob - R1 (trash after Alice plays)
                "Y3,B3,G3,P3,R3," +   // Charlie
                "R4,Y4,B4")
            .Play(0)                    // Alice plays R1
            .RankClue(0, 1)             // Bob clues Alice's 1s
            .RankClue(1, 1)             // Charlie clues Bob's "1" (R1 = trash!)
            .ColorClue(1, "Red")        // Alice gives fix clue! Tells Bob his 1 is Red = already played
            .BuildAndAnalyze();

        violations.Should().NotContainViolation(ViolationType.FixClue);
    }

    [Fact]
    public void NoImmediateThreat_NoFixClueNeeded()
    {
        // Bob has a clued 1 that's trash but doesn't try to play it
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob", "Charlie")
            .WithDeck(
                "R1,Y1,B1,G1,P1," +
                "R1,Y2,B2,G2,P2," +
                "Y3,B3,G3,P3,R3," +
                "R4,Y4,B4")
            .Play(0)                    // Alice plays R1
            .RankClue(0, 1)             // Bob clues Alice 1s
            .RankClue(1, 1)             // Charlie clues Bob 1 (R1 = trash)
            .Discard(4)                 // Alice discards (no fix clue)
            .Discard(9)                 // Bob discards instead of playing - no misplay!
            .BuildAndAnalyze();

        violations.Should().NotContainViolation(ViolationType.FixClue);
    }

    [Fact]
    public void OnlyAppliesAtLevel3()
    {
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob", "Charlie")
            .WithDeck(
                "R1,Y1,B1,G1,P1," +
                "R1,Y2,B2,G2,P2," +
                "Y3,B3,G3,P3,R3," +
                "R4,Y4,B4")
            .AtBeginnerLevel()
            .Play(0)
            .RankClue(0, 1)
            .RankClue(1, 1)
            .ColorClue(2, "Yellow")
            .Play(5)
            .BuildAndAnalyze();

        violations.Should().NotContainViolation(ViolationType.FixClue);
    }
}
