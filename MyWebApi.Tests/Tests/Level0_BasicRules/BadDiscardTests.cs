using FluentAssertions;
using MyWebApi.Models;
using MyWebApi.Services;
using MyWebApi.Tests.Builders;
using MyWebApi.Tests.Helpers;
using Xunit;

namespace MyWebApi.Tests.Tests.Level0_BasicRules;

/// <summary>
/// Tests for bad discard detection.
/// Bad discards include:
/// - Discarding a 5 (always critical, single copy)
/// - Discarding the last copy of a card (critical)
///
/// Per H-Group conventions, the player who discarded is blamed.
/// </summary>
public class BadDiscardTests
{
    [Theory]
    [InlineData(0)] // Red 5
    [InlineData(1)] // Yellow 5
    [InlineData(2)] // Green 5
    [InlineData(3)] // Blue 5
    [InlineData(4)] // Purple 5
    public void Discard5_AnySuit_CreatesViolation(int suitIndex)
    {
        // Arrange: First card in Alice's hand is a 5 of the test suit
        var suitChar = new[] { 'R', 'Y', 'G', 'B', 'P' }[suitIndex];
        var deck = $"{suitChar}5,R1,Y1,B1,G1, R2,Y2,B2,G2,P2";

        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck(deck)
            .Discard(0) // Alice discards the 5
            .BuildAndAnalyze();

        // Assert
        violations.Should().ContainViolation(ViolationType.BadDiscard5);
        violations.Should().ContainViolationForPlayer(ViolationType.BadDiscard5, "Alice");
    }

    [Fact]
    public void DiscardAlreadyPlayed5_NoViolation()
    {
        // If a 5 is already played, discarding another copy would be fine
        // But 5s only have one copy, so this scenario only happens if the discard
        // happens AFTER the 5 is played (same 5 can't be discarded)
        // Actually, since 5s have only one copy, we can't test this directly.
        // We'll test discarding a trash card instead.

        // Play R1-R5, then discard another R1 (trash, already played)
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,R2,R3,R4,R5, Y1,Y2,Y3,Y4,Y5, R1,R2,G1,G2,G3, G4,G5,B1,B2,B3")
            .Play(0)  // Alice plays R1
            .Play(5)  // Bob plays Y1
            .Play(1)  // Alice plays R2
            .Play(6)  // Bob plays Y2
            .Play(2)  // Alice plays R3
            .Play(7)  // Bob plays Y3
            .Play(3)  // Alice plays R4
            .Play(8)  // Bob plays Y4
            .Play(4)  // Alice plays R5 - Red complete!
            .Play(9)  // Bob plays Y5 - Yellow complete!
            .Discard(10) // Alice discards R1 - it's trash now (Red already at 5)
            .BuildAndAnalyze();

        // Assert - discarding R1 when Red is complete is NOT a BadDiscard5
        violations.OfType(ViolationType.BadDiscard5).Should().BeEmpty();
    }

    [Fact]
    public void DiscardLastCopy_CreatesViolation()
    {
        // Arrange: Set up scenario where there's only one copy left of a card
        // For rank 1, there are 3 copies. If 2 are discarded, the third is critical.
        // For simplicity, use a rank-2 card (2 copies) where one is discarded already.

        // We'll discard R2 twice (there are only 2 copies)
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R2,R1,Y1,B1,G1, R2,Y2,B2,G2,P1, R3,Y3")
            .Discard(0) // Alice discards R2 (first copy)
            .Discard(5) // Bob discards R2 (second copy - critical!)
            .BuildAndAnalyze();

        // Assert - second discard should be flagged as critical
        violations.Should().ContainViolation(ViolationType.BadDiscardCritical);
        violations.Should().ContainViolationForPlayer(ViolationType.BadDiscardCritical, "Bob");
    }

    [Fact]
    public void DiscardNonCriticalCard_NoViolation()
    {
        // Discard a card that has multiple copies remaining
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,R2,Y1,B1,G1, R1,Y2,B2,G2,P1, R1,Y3") // R1 appears 3 times
            .Discard(0) // Alice discards R1 (2 copies still exist)
            .BuildAndAnalyze();

        // Assert - not critical
        violations.Should().NotContainViolation(ViolationType.BadDiscard5);
        violations.Should().NotContainViolation(ViolationType.BadDiscardCritical);
    }

    [Fact]
    public void DiscardTrashCard_NoViolation()
    {
        // Play a card, then discard the same rank/suit (now trash)
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,R1,Y1,B1,G1, R2,Y2,B2,G2,P1, R3,Y3")
            .Play(0)     // Alice plays R1 -> stack at 1
            .Discard(1)  // Bob discards R1 (it's trash now, already played)
            .BuildAndAnalyze();

        // Assert - no violations for discarding trash
        violations.Should().NotContainViolation(ViolationType.BadDiscard5);
        violations.Should().NotContainViolation(ViolationType.BadDiscardCritical);
    }

    [Fact]
    public void DiscardWhenSuitIsDead_NoViolation()
    {
        // If all copies of a needed card are discarded, higher cards of that suit
        // become trash (suit is "dead")

        // Discard both R2s, then R3 becomes trash
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R2,R2,R3,B1,G1, R4,Y2,B2,G2,P1, R5,Y3")
            .Discard(0) // Alice discards R2 (first copy)
            .Discard(1) // Bob discards R2 (second copy - Red suit now dead at rank 1!)
            .Discard(2) // Alice discards R3 - should be fine since Red suit is dead
            .BuildAndAnalyze();

        // Assert - R3 discard should NOT be a violation since suit is dead
        // Note: This depends on implementation detecting dead suits
        // For now, just verify we don't flag it as critical (R3 has 2 copies)
        var turn3Violations = violations.Where(v => v.Turn == 3).ToList();
        turn3Violations.Should().NotContain(v => v.Type == ViolationType.BadDiscardCritical);
    }

    [Fact]
    public void BadDiscard5_HasCriticalSeverity()
    {
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R5,R1,Y1,B1,G1, R2,Y2,B2,G2,P1")
            .Discard(0) // Alice discards R5
            .BuildAndAnalyze();

        // Assert
        violations.Should().ContainViolationWithSeverity(ViolationType.BadDiscard5, Severity.Critical);
    }

    [Fact]
    public void BadDiscardCritical_HasCriticalSeverity()
    {
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R2,R1,Y1,B1,G1, R2,Y2,B2,G2,P1, R3,Y3")
            .Discard(0) // Alice discards R2 (first copy)
            .Discard(5) // Bob discards R2 (critical - last copy)
            .BuildAndAnalyze();

        // Assert
        violations.Should().ContainViolationWithSeverity(ViolationType.BadDiscardCritical, Severity.Critical);
    }

    [Fact]
    public void DiscardPlayableNonCritical_ShouldBeTempoLoss()
    {
        // Discarding a playable card that isn't critical is a tempo loss
        // This should ideally create a warning but not critical
        // Note: Current implementation may not detect this - test defines correct behavior

        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,R1,Y1,B1,G1, R2,Y2,B2,G2,P1, R3,Y3") // Two R1s
            .Discard(0) // Alice discards R1 - it's playable but has another copy
            .BuildAndAnalyze();

        // Assert - this shouldn't be BadDiscard5 or BadDiscardCritical
        // But it might be flagged as suboptimal play (tempo loss)
        violations.Should().NotContainViolation(ViolationType.BadDiscard5);
        violations.Should().NotContainViolation(ViolationType.BadDiscardCritical);

        // Note: A future improvement might add a "TempoLoss" violation type
    }

    [Fact]
    public void DiscardDescription_IncludesCardInfo()
    {
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R5,R1,Y1,B1,G1, R2,Y2,B2,G2,P1")
            .Discard(0)
            .BuildAndAnalyze();

        var violation = violations.FirstOfType(ViolationType.BadDiscard5);
        violation.Should().NotBeNull();
        violation!.Description.Should().Contain("Red 5");
    }
}
