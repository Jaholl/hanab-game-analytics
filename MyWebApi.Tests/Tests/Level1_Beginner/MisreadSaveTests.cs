using FluentAssertions;
using MyWebApi.Models;
using MyWebApi.Tests.Builders;
using MyWebApi.Tests.Helpers;
using Xunit;

namespace MyWebApi.Tests.Tests.Level1_Beginner;

/// <summary>
/// Tests for MisreadSave detection.
/// A MisreadSave occurs when a player misplays a card that was on chop when clued
/// (i.e., they interpreted a save clue as a play clue).
/// </summary>
public class MisreadSaveTests
{
    [Fact]
    public void ChopCardClued_ThenMisplayed_EmitsMisreadSave()
    {
        // Alice's hand: R1(idx 0), R2(idx 1), Y1(idx 2), B1(idx 3), G1(idx 4)
        // Bob's hand:   R3(idx 5), Y2(idx 6), B2(idx 7), G2(idx 8), P1(idx 9)
        // Turn 1 (Alice): clue Bob color Red (touches R3)
        // Turn 2 (Bob): clue Alice rank 2 (touches R2 at idx 1)
        //   — Alice's chop is idx 0 (R1, oldest unclued) … but rank 2 touches idx 1, not chop.
        //
        // Better setup: put the card we want clued at chop (idx 0).
        // Alice's hand: R3(idx 0, chop), R1(idx 1), Y1(idx 2), B1(idx 3), G1(idx 4)
        // Turn 1 (Alice): clue Bob color Red
        // Turn 2 (Bob): clue Alice rank 3, touches R3 at idx 0 (Alice's chop)
        // Turn 3 (Alice): plays R3 (idx 0) — misplay (stack at 0, needs 1)
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R3,R1,Y1,B1,G1, R2,Y2,B2,G2,P1, P2,Y3")
            .ColorClue(1, "Red")  // Turn 1: Alice clues Bob Red (touches R2 at idx 5)
            .RankClue(0, 3)       // Turn 2: Bob clues Alice rank 3 (touches R3 at idx 0 = chop)
            .Play(0)              // Turn 3: Alice plays R3 — misplay (misread save as play)
            .BuildAndAnalyze();

        violations.Should().ContainViolation(ViolationType.MisreadSave);
        violations.Should().ContainViolationForPlayer(ViolationType.MisreadSave, "Alice");
        // Should also still emit a Misplay
        violations.Should().ContainViolation(ViolationType.Misplay);
    }

    [Fact]
    public void NonChopCardClued_ThenMisplayed_NoMisreadSave()
    {
        // Alice's hand: R1(idx 0, chop), R3(idx 1), Y1(idx 2), B1(idx 3), G1(idx 4)
        // Turn 1 (Alice): clue Bob color Yellow
        // Turn 2 (Bob): clue Alice rank 3, touches R3 at idx 1 (NOT chop — chop is idx 0)
        // Turn 3 (Alice): plays R3 (idx 1) — misplay, but not a misread save
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,R3,Y1,B1,G1, R2,Y2,B2,G2,P1, P2,Y3")
            .ColorClue(1, "Yellow")  // Turn 1: Alice clues Bob Yellow
            .RankClue(0, 3)          // Turn 2: Bob clues Alice rank 3 (touches idx 1, not chop)
            .Play(1)                 // Turn 3: Alice plays R3 — misplay
            .BuildAndAnalyze();

        violations.Should().NotContainViolation(ViolationType.MisreadSave);
        violations.Should().ContainViolation(ViolationType.Misplay);
    }

    [Fact]
    public void UncluedCardMisplayed_NoMisreadSave()
    {
        // Alice plays an unclued card that's a misplay — no MisreadSave
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R3,R1,Y1,B1,G1, R2,Y2,B2,G2,P1, P2,Y3")
            .Play(0)  // Turn 1: Alice plays R3 unclued — standard misplay
            .BuildAndAnalyze();

        violations.Should().NotContainViolation(ViolationType.MisreadSave);
        violations.Should().ContainViolation(ViolationType.Misplay);
    }

    [Fact]
    public void Level0Analysis_NoMisreadSave()
    {
        // Same scenario as ChopCardClued but at Level 0 — MisreadSave filtered out
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R3,R1,Y1,B1,G1, R2,Y2,B2,G2,P1, P2,Y3")
            .AtBasicLevel()
            .ColorClue(1, "Red")  // Turn 1: Alice clues Bob Red
            .RankClue(0, 3)       // Turn 2: Bob clues Alice rank 3 (chop)
            .Play(0)              // Turn 3: Alice misplays R3
            .BuildAndAnalyze();

        violations.Should().NotContainViolation(ViolationType.MisreadSave);
        violations.Should().ContainViolation(ViolationType.Misplay);
    }
}
