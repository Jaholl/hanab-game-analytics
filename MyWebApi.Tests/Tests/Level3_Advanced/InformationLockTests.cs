using FluentAssertions;
using MyWebApi.Models;
using MyWebApi.Services;
using MyWebApi.Tests.Builders;
using MyWebApi.Tests.Helpers;
using Xunit;

namespace MyWebApi.Tests.Tests.Level3_Advanced;

/// <summary>
/// Tests for Information Lock convention.
///
/// H-Group Rule: Once a card's identity is fully determined by clues
/// (both color and rank are known), that identity "locks in."
/// The player should act on the locked identity. If they don't
/// (e.g., they discard a known-playable card), it's a violation.
/// </summary>
public class InformationLockTests
{
    [Fact]
    public void FullyKnownPlayable_Discarded_CreatesViolation()
    {
        // Bob has R1 with both color and rank clued. R1 is playable.
        // Bob discards it instead of playing - violation.
        //
        // 2-player, hand size 5
        // Alice(0-4): R2,Y2,B1,G1,P1
        // Bob(5-9): R1,Y1,B2,G2,P2
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R2,Y2,B1,G1,P1, R1,Y1,B2,G2,P2, R3,Y3")
            .AtAdvancedLevel()
            .ColorClue(1, "Red")  // Alice clues Bob Red -> marks R1 color
            .RankClue(0, 2)       // Bob clues Alice 2
            .RankClue(1, 1)       // Alice clues Bob 1 -> marks R1 rank. Now R1 fully known.
            .Discard(5)           // Bob discards R1 despite knowing it's Red 1 (playable)!
            .BuildAndAnalyze();

        violations.Should().ContainViolation(ViolationType.InformationLock);
        violations.Should().ContainViolationForPlayer(ViolationType.InformationLock, "Bob");
    }

    [Fact]
    public void FullyKnownPlayable_Played_NoViolation()
    {
        // Bob has R1 fully determined and plays it correctly
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R2,Y2,B1,G1,P1, R1,Y1,B2,G2,P2, R3,Y3")
            .AtAdvancedLevel()
            .ColorClue(1, "Red")
            .RankClue(0, 2)
            .RankClue(1, 1)
            .Play(5)              // Bob plays R1 - correct!
            .BuildAndAnalyze();

        violations.Should().NotContainViolation(ViolationType.InformationLock);
    }

    [Fact]
    public void PartiallyKnown_NoLockViolation()
    {
        // Bob has a card with only color known (not rank) - not fully determined
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R2,Y2,B1,G1,P1, R1,Y1,B2,G2,P2, R3,Y3")
            .AtAdvancedLevel()
            .ColorClue(1, "Red")  // Bob knows color but not rank
            .Discard(5)           // Bob discards R1 (only knows it's Red)
            .BuildAndAnalyze();

        violations.Should().NotContainViolation(ViolationType.InformationLock);
    }

    [Fact]
    public void FullyKnownTrash_Discarded_NoViolation()
    {
        // R1 is fully known but it's trash (already played) - discarding is fine
        //
        // 2-player, hand size 5
        // Alice(0-4): R1,Y2,B1,G1,P1
        // Bob(5-9): R1,Y1,B2,G2,P2
        // Draw pile: R3,Y3,B3,G3
        //
        // Action 0 (Alice): Play R1 (deck idx 0) -> Red stack = 1, draws R3 (idx 10)
        // Action 1 (Bob):   Clue Alice rank 2 (filler)
        // Action 2 (Alice): ColorClue Bob Red -> marks Bob's R1 (deck idx 5)
        // Action 3 (Bob):   Clue Alice rank 1 (filler)
        // Action 4 (Alice): RankClue Bob 1 -> Bob's R1 now fully known
        // Action 5 (Bob):   Discard R1 (deck idx 5) - fully known trash
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,Y2,B1,G1,P1, R1,Y1,B2,G2,P2, R3,Y3,B3,G3")
            .AtAdvancedLevel()
            .Play(0)              // Action 0: Alice plays R1 -> Red stack = 1
            .RankClue(0, 2)       // Action 1: Bob clues Alice 2 (filler)
            .ColorClue(1, "Red")  // Action 2: Alice clues Bob Red -> touches R1
            .RankClue(0, 1)       // Action 3: Bob clues Alice 1 (filler)
            .RankClue(1, 1)       // Action 4: Alice clues Bob 1 -> R1 fully known
            .Discard(5)           // Action 5: Bob discards R1 (fully known trash)
            .BuildAndAnalyze();

        // The key assertion: no InformationLock violation for discarding trash
        violations.Should().NotContainViolation(ViolationType.InformationLock);
    }

    [Fact]
    public void OnlyAppliesAtLevel3()
    {
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R2,Y2,B1,G1,P1, R1,Y1,B2,G2,P2, R3,Y3")
            .AtBeginnerLevel()
            .ColorClue(1, "Red")
            .RankClue(0, 2)
            .RankClue(1, 1)
            .Discard(5)
            .BuildAndAnalyze();

        violations.Should().NotContainViolation(ViolationType.InformationLock);
    }
}
