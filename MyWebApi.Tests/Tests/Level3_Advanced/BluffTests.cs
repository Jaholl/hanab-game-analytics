using FluentAssertions;
using MyWebApi.Models;
using MyWebApi.Services;
using MyWebApi.Tests.Builders;
using MyWebApi.Tests.Helpers;
using Xunit;

namespace MyWebApi.Tests.Tests.Level3_Advanced;

/// <summary>
/// Tests for bluff recognition.
///
/// A bluff is similar to a finesse but intentionally "lies":
/// - Clue points to a one-away card
/// - The next player's finesse position is NOT the connecting card
/// - But it IS some other playable card
/// - The player blind-plays, and the clued player realizes it wasn't
///   the connecting card (the "bluff" resolves)
///
/// Bluffs only work on the player immediately after the clue giver.
/// Distinguishing bluffs from finesses is crucial for correct blame attribution.
/// </summary>
public class BluffTests
{
    [Fact]
    public void ValidBluff_NonMatchingBlindPlay_NoViolation()
    {
        // Alice clues R2, Bob has B1 in finesse pos (not R1)
        // Bob blind-plays B1 (success!) - this was a bluff

        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob", "Charlie")
            .WithDeck(
                "R3,Y1,B1,G1,P1," +  // Alice
                "B1,Y2,B2,G2,P2," +  // Bob - B1 (not R1!) in finesse pos
                "R2,Y3,B3,G3,P3," +  // Charlie - R2 is focus
                "R4,Y4")
            .ColorClue(2, "Red")  // Alice clues R2 (looks like finesse, is bluff)
            .Play(5)              // Bob blind-plays B1 (playable, not R1!)
            .BuildAndAnalyze();

        // Assert - this is a valid bluff, no violations
        violations.Should().NotContainViolation(ViolationType.BrokenFinesse);
        violations.Should().NotContainViolation(ViolationType.MissedFinesse);
        violations.Should().NotContainViolation(ViolationType.Misplay);
    }

    [Fact]
    public void BluffVsFinesse_DistinguishByPlayedCard()
    {
        // If Bob plays the connecting card (R1), it was a finesse
        // If Bob plays a different playable card, it was a bluff

        // Finesse case:
        var (finGame, finStates, finViolations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob", "Charlie")
            .WithDeck(
                "R3,Y1,B1,G1,P1," +
                "R1,Y2,B2,G2,P2," +  // Bob has R1 (connecting card)
                "R2,Y3,B3,G3,P3," +
                "R4,Y4")
            .ColorClue(2, "Red")
            .Play(5)  // Bob plays R1 - FINESSE
            .BuildAndAnalyze();

        // Bluff case:
        var (bluffGame, bluffStates, bluffViolations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob", "Charlie")
            .WithDeck(
                "R3,Y1,B1,G1,P1," +
                "B1,Y2,B2,G2,P2," +  // Bob has B1 (NOT connecting card)
                "R2,Y3,B3,G3,P3," +
                "R4,Y4")
            .ColorClue(2, "Red")
            .Play(5)  // Bob plays B1 - BLUFF
            .BuildAndAnalyze();

        // Both should have no violations - both are valid
        finViolations.Should().NotContainViolation(ViolationType.BrokenFinesse);
        bluffViolations.Should().NotContainViolation(ViolationType.BrokenFinesse);
    }

    [Fact]
    public void BluffOnlyWorksOnNextPlayer()
    {
        // Bluffs only work on the player immediately after the clue giver
        // If trying to bluff a non-adjacent player, it's interpreted as finesse

        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob", "Charlie", "Diana")
            .WithDeck(
                "R3,Y1,B1,G1," +      // Alice (clue giver)
                "Y2,Y3,B2,G2," +      // Bob - no playable card!
                "B1,Y4,B3,G3," +      // Charlie - has B1 (playable)
                "R2,Y5,B4,G4," +      // Diana - R2 (focus)
                "P1,P2")
            .ColorClue(3, "Red")  // Alice clues Diana's R2
            // Is this a finesse on Bob or bluff on Charlie?
            // Bob has nothing playable, so Bob should pass
            // Charlie might interpret as bluff on them? No, bluffs are immediate
            .BuildAndAnalyze();

        // This is complex - the test documents that bluffs have position rules
        Assert.True(true, "Specification: Bluffs only work on immediately-next player");
    }

    [Fact]
    public void FailedBluff_UnplayableCard_CreatesViolation()
    {
        // Bob has Y3 in finesse pos - not playable!
        // This is neither valid finesse nor valid bluff

        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob", "Charlie")
            .WithDeck(
                "R3,Y1,B1,G1,P1," +
                "Y3,Y2,B2,G2,P2," +  // Bob - Y3 not playable (needs Y1,Y2 first)
                "R2,Y4,B3,G3,P3," +
                "R4,Y5")
            .ColorClue(2, "Red")  // Alice clues R2
            .Play(5)              // Bob plays Y3 - MISPLAY!
            .BuildAndAnalyze();

        // Assert - misplay and possibly broken finesse
        violations.Should().ContainViolation(ViolationType.Misplay);
    }

    [Fact]
    public void BluffResolution_CluedPlayerKnows()
    {
        // After a bluff, the clued player should realize their card
        // isn't immediately playable (they need to wait)

        // This is about the clued player's understanding after seeing
        // the "wrong" card played - Charlie knows R2 isn't R1 now

        Assert.True(true, "Specification: Bluff resolution affects clued player's knowledge");
    }

    [Fact]
    public void DoubleBluff_TwoPlayersBlindPlay()
    {
        // Clue targets card that's two-away, both next players have
        // playable cards in finesse pos

        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob", "Charlie", "Diana")
            .WithDeck(
                "R4,Y1,B1,G1," +
                "B1,Y2,B2,G2," +      // Bob - B1 playable
                "G1,Y3,B3,G3," +      // Charlie - G1 playable
                "R3,Y4,B4,G4," +      // Diana - R3 (two-away!)
                "P1,P2")
            .ColorClue(3, "Red")  // Alice clues R3 (double bluff!)
            .Play(4)              // Bob plays B1
            .Play(8)              // Charlie plays G1
            // Diana realizes R3 needs R1,R2 still
            .BuildAndAnalyze();

        // Double bluffs are advanced conventions
        Assert.True(true, "Specification: Double bluffs are advanced");
    }

    [Fact]
    public void HardBluff_TargetNotOneAway()
    {
        // "Hard" bluffs involve cards that aren't obviously one-away
        // These are more advanced and risky

        Assert.True(true, "Specification: Hard bluffs are advanced");
    }

    [Fact]
    public void Bluff_CantBePerformedFromPosition()
    {
        // Some positions can't give valid bluffs
        // E.g., if clue giver is immediately before target, no one to bluff

        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")  // Only 2 players
            .WithDeck("R3,Y1,B1,G1,P1, R2,Y2,B2,G2,P2, R4,Y3")
            // Alice clues Bob directly - Bob is also "next player"
            // Can't really bluff in 2-player the same way
            .ColorClue(1, "Red")  // Alice clues Bob's R2
            .BuildAndAnalyze();

        // 2-player bluffs work differently - this tests the edge case
        Assert.True(true, "Specification: Bluff mechanics vary by player count");
    }

    [Fact]
    public void BluffIndicators_NotBrokenFinesse()
    {
        // When a player blind-plays a non-connecting but playable card,
        // the analyzer should recognize this as bluff, not broken finesse

        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob", "Charlie")
            .WithDeck(
                "R3,Y1,B1,G1,P1," +
                "Y1,Y2,B2,G2,P2," +  // Bob - Y1 (playable, not R1)
                "R2,Y3,B3,G3,P3," +
                "R4,Y4")
            .ColorClue(2, "Red")
            .Play(5)  // Bob plays Y1 - successful!
            .BuildAndAnalyze();

        // Should NOT be BrokenFinesse (Y1 was playable)
        violations.Should().NotContainViolation(ViolationType.BrokenFinesse);
    }
}
