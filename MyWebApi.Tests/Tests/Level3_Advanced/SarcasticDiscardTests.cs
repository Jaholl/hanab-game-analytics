using FluentAssertions;
using MyWebApi.Models;
using MyWebApi.Services;
using MyWebApi.Tests.Builders;
using MyWebApi.Tests.Helpers;
using Xunit;

namespace MyWebApi.Tests.Tests.Level3_Advanced;

/// <summary>
/// Tests for Sarcastic Discard convention.
///
/// H-Group Rule: When a player has a clued card that is a known duplicate
/// of a card clued in another player's hand, the first player to realize
/// the duplication should discard their copy. This "sarcastic discard"
/// signals to the other player that they definitely have that card.
///
/// A player who should give a sarcastic discard but doesn't is violating
/// this convention.
/// </summary>
public class SarcasticDiscardTests
{
    [Fact]
    public void MissedSarcasticDiscard_WhenHoldingKnownDuplicate_CreatesViolation()
    {
        // Setup: Both Alice and Bob have R2 clued.
        // When Alice sees Bob also has R2 clued, she should sarcastic-discard hers.
        //
        // 3-player, hand size 5. Turn order: Alice(0), Bob(1), Charlie(2), Alice(0)...
        // Alice(0-4): R2,Y1,B1,G1,P1
        // Bob(5-9): R2,Y2,B2,G2,P2
        // Charlie(10-14): Y3,B3,G3,P3,R3
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob", "Charlie")
            .WithDeck(
                "R2,Y1,B1,G1,P1," +   // Alice - R2 at slot 0
                "R2,Y2,B2,G2,P2," +   // Bob - R2 at slot 0
                "Y3,B3,G3,P3,R3," +   // Charlie
                "R4,Y4,B4")
            .AtAdvancedLevel()
            // Turn 1 (Alice): Clue Bob Red -> Bob's R2 gets clued
            .ColorClue(1, "Red")
            // Turn 2 (Bob): Clue Alice Red -> Alice's R2 gets clued
            .ColorClue(0, "Red")
            // Now both have clued R2s. Alice knows she has a duplicate.
            // Turn 3 (Charlie): does something else
            .Discard(14)
            // Turn 4 (Alice): Should sarcastic-discard R2, but discards chop instead
            .Discard(4)            // Alice discards P1 from chop instead of sarcastic-discarding R2
            .BuildAndAnalyze();

        violations.Should().ContainViolation(ViolationType.SarcasticDiscard);
    }

    [Fact]
    public void SarcasticDiscardPerformed_NoViolation()
    {
        // Alice correctly sarcastic-discards her known duplicate R2
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob", "Charlie")
            .WithDeck(
                "R2,Y1,B1,G1,P1," +
                "R2,Y2,B2,G2,P2," +
                "Y3,B3,G3,P3,R3," +
                "R4,Y4,B4")
            .AtAdvancedLevel()
            .ColorClue(1, "Red")   // Alice clues Bob Red
            .ColorClue(0, "Red")   // Bob clues Alice Red
            .Discard(14)           // Charlie discards
            .Discard(0)            // Alice sarcastic-discards her R2 - correct!
            .BuildAndAnalyze();

        violations.Should().NotContainViolation(ViolationType.SarcasticDiscard);
    }

    [Fact]
    public void NoDuplicate_NoSarcasticDiscardNeeded()
    {
        // Alice's clued card is not a duplicate of anything - no sarcastic discard needed
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R2,Y1,B1,G1,P1, R3,Y2,B2,G2,P2, R4,Y3")
            .AtAdvancedLevel()
            .ColorClue(1, "Red")  // Alice clues Bob Red (R3 - unique, not same as Alice's R2)
            .Discard(5)           // Bob discards from chop
            .BuildAndAnalyze();

        violations.Should().NotContainViolation(ViolationType.SarcasticDiscard);
    }

    [Fact]
    public void OnlyAppliesAtLevel3()
    {
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob", "Charlie")
            .WithDeck(
                "R2,Y1,B1,G1,P1," +
                "R2,Y2,B2,G2,P2," +
                "Y3,B3,G3,P3,R3," +
                "R4,Y4,B4")
            .AtIntermediateLevel()
            .ColorClue(1, "Red")
            .ColorClue(0, "Red")
            .Discard(14)
            .Discard(4)
            .BuildAndAnalyze();

        violations.Should().NotContainViolation(ViolationType.SarcasticDiscard);
    }
}
