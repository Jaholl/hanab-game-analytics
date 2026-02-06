using FluentAssertions;
using MyWebApi.Models;
using MyWebApi.Services;
using MyWebApi.Tests.Builders;
using MyWebApi.Tests.Helpers;
using Xunit;

namespace MyWebApi.Tests.Tests.Level3_Advanced;

/// <summary>
/// Tests for Playing Multiple 1s convention.
///
/// H-Group Rule: When multiple 1s are clued in your hand, play them
/// from oldest to newest (lowest hand index to highest).
/// Playing in the wrong order is a WrongOnesOrder violation.
/// </summary>
public class PlayingMultipleOnesTests
{
    [Fact]
    public void PlayOldestOneFirst_NoViolation()
    {
        // Setup: Alice has R1 (slot 0) and Y1 (slot 2), both clued as 1s.
        // She plays R1 (oldest) first - correct order.
        //
        // 2-player: hand size 5. Alice deck 0-4, Bob deck 5-9.
        // Alice: R1(0), G2(1), Y1(2), B3(3), P4(4)
        // Bob: R2(5), Y2(6), B2(7), G3(8), P1(9)
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,G2,Y1,B3,P4, R2,Y2,B2,G3,P1, R3,B4")
            .AtAdvancedLevel()
            .ColorClue(1, "Red")  // Action 0 (Alice): clue Bob Red
            .RankClue(0, 1)       // Action 1 (Bob): clue Alice's 1s -> touches R1(slot 0) and Y1(slot 2)
            .Play(0)              // Action 2 (Alice): plays R1 (oldest 1, slot 0) - correct!
            .BuildAndAnalyze();

        violations.Should().NotContainViolation(ViolationType.WrongOnesOrder);
    }

    [Fact]
    public void PlayNewerOneFirst_CreatesViolation()
    {
        // Alice has R1 (slot 0) and Y1 (slot 2), both clued as 1s.
        // She plays Y1 (newer, slot 2) first - wrong order!
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,G2,Y1,B3,P4, R2,Y2,B2,G3,P1, R3,B4")
            .AtAdvancedLevel()
            .ColorClue(1, "Red")  // Action 0 (Alice): clue Bob Red
            .RankClue(0, 1)       // Action 1 (Bob): clue Alice's 1s -> touches R1(slot 0) and Y1(slot 2)
            .Play(2)              // Action 2 (Alice): plays Y1 (newer 1, slot 2) - wrong order!
            .BuildAndAnalyze();

        violations.Should().ContainViolation(ViolationType.WrongOnesOrder);
        violations.Should().ContainViolationForPlayer(ViolationType.WrongOnesOrder, "Alice");
    }

    [Fact]
    public void SingleClued1_NoOrderViolation()
    {
        // Only one 1 in hand - no ordering issue
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,R2,R3,R4,R5, Y1,Y2,Y3,Y4,Y5, G1,G2")
            .AtAdvancedLevel()
            .ColorClue(1, "Yellow")  // Action 0 (Alice): clue Bob Yellow
            .RankClue(0, 1)          // Action 1 (Bob): clue Alice's 1 (only R1)
            .Play(0)                 // Action 2 (Alice): plays the only 1
            .BuildAndAnalyze();

        violations.Should().NotContainViolation(ViolationType.WrongOnesOrder);
    }

    [Fact]
    public void ThreeClued1s_MustPlayOldestFirst()
    {
        // Alice has three 1s: R1(slot 0), Y1(slot 1), G1(slot 3)
        // Must play R1 (slot 0) first
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,Y1,R2,G1,B2, R3,Y2,B3,G2,P1, R4,Y3")
            .AtAdvancedLevel()
            .ColorClue(1, "Red")  // Action 0 (Alice): clue Bob Red
            .RankClue(0, 1)       // Action 1 (Bob): clue Alice's 1s (R1 slot 0, Y1 slot 1, G1 slot 3)
            .Play(1)              // Action 2 (Alice): plays Y1 (slot 1) instead of R1 (slot 0) - wrong!
            .BuildAndAnalyze();

        violations.Should().ContainViolation(ViolationType.WrongOnesOrder);
    }

    [Fact]
    public void NotAOne_NoOrderCheck()
    {
        // Playing a non-1 card - this checker doesn't apply
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,Y2,B1,G1,P1, R2,Y1,B2,G2,P2, R3,Y3")
            .AtAdvancedLevel()
            .Play(0)  // Alice plays R1 (a 1, but not from a multi-1 setup)
            .BuildAndAnalyze();

        violations.Should().NotContainViolation(ViolationType.WrongOnesOrder);
    }

    [Fact]
    public void OnlyAppliesAtLevel3()
    {
        // Same scenario at Level 2 should NOT produce WrongOnesOrder
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,G2,Y1,B3,P4, R2,Y2,B2,G3,P1, R3,B4")
            .AtIntermediateLevel()
            .ColorClue(1, "Red")  // Action 0 (Alice): clue Bob Red
            .RankClue(0, 1)       // Action 1 (Bob): clue Alice's 1s
            .Play(2)              // Action 2 (Alice): play newer 1 first
            .BuildAndAnalyze();

        violations.Should().NotContainViolation(ViolationType.WrongOnesOrder);
    }
}
