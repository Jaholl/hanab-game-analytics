using FluentAssertions;
using MyWebApi.Models;
using MyWebApi.Services;
using MyWebApi.Tests.Builders;
using MyWebApi.Tests.Helpers;
using Xunit;

namespace MyWebApi.Tests.Tests.Level0_BasicRules;

/// <summary>
/// Tests for misplay detection.
/// Misplays occur when a player plays a card that cannot be legally played.
/// Per H-Group conventions, the player who misplayed is blamed.
/// </summary>
public class MisplayTests
{
    [Fact]
    public void PlayWrongCard_CreatesViolation()
    {
        // Arrange: Player plays R3 when R stack is at 0 (needs R1)
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R3,R1,Y1,B1,G1, R2,Y2,B2,G2,P1, R1,Y3") // First 5 to Alice, next 5 to Bob
            .Play(0) // Alice plays R3 (deck index 0) - this is a misplay
            .BuildAndAnalyze();

        // Assert
        violations.Should().ContainViolation(ViolationType.Misplay);
        violations.Should().ContainViolationForPlayer(ViolationType.Misplay, "Alice");
    }

    [Fact]
    public void PlayCorrectCard_NoViolation()
    {
        // Arrange: Player plays R1 when R stack is at 0 (needs R1)
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,R2,Y1,B1,G1, R3,Y2,B2,G2,P1, P2,Y3")
            .Play(0) // Alice plays R1 - valid play
            .BuildAndAnalyze();

        // Assert
        violations.Should().NotContainViolation(ViolationType.Misplay);
    }

    [Fact]
    public void PlayCardTooHigh_CreatesViolation()
    {
        // Arrange: Player plays R2 when R stack is at 0 (needs R1)
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R2,R1,Y1,B1,G1, R3,Y2,B2,G2,P1, P2,Y3")
            .Play(0) // Alice plays R2 - misplay (stack at 0, needs 1)
            .BuildAndAnalyze();

        // Assert
        violations.Should().ContainViolation(ViolationType.Misplay);
        var violation = violations.FirstOfType(ViolationType.Misplay);
        violation!.Description.Should().Contain("Red 2");
        violation.Description.Should().Contain("needed 1");
    }

    [Theory]
    [InlineData(0)] // Red
    [InlineData(1)] // Yellow
    [InlineData(2)] // Green
    [InlineData(3)] // Blue
    [InlineData(4)] // Purple
    public void PlayWrongCard_AnySuit_CreatesViolation(int suitIndex)
    {
        // Arrange: Create deck where first card is rank 3 of the test suit
        var suitChar = new[] { 'R', 'Y', 'G', 'B', 'P' }[suitIndex];
        var deck = $"{suitChar}3,R1,Y1,B1,G1, R2,Y2,B2,G2,P1";

        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck(deck)
            .Play(0) // Play the rank 3 card - misplay
            .BuildAndAnalyze();

        // Assert
        violations.Should().ContainViolation(ViolationType.Misplay);
    }

    [Fact]
    public void PlayOnCompletedStack_CreatesViolation()
    {
        // This tests playing a card when the stack is already at 5
        // First we need to build up a stack, then try to play another card of that suit

        // For this test, we'll create a simpler scenario:
        // Play R1, R2, R3, R4, R5, then try to play another Red card
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,R2,R3,R4,R5, Y1,Y2,Y3,Y4,Y5, R1,R2,R3") // R1 twice in deck
            .Play(0)  // Alice plays R1 -> stack at 1
            .Play(5)  // Bob plays Y1
            .Play(1)  // Alice plays R2 -> stack at 2
            .Play(6)  // Bob plays Y2
            .Play(2)  // Alice plays R3 -> stack at 3
            .Play(7)  // Bob plays Y3
            .Play(3)  // Alice plays R4 -> stack at 4
            .Play(8)  // Bob plays Y4
            .Play(4)  // Alice plays R5 -> stack at 5
            .Play(9)  // Bob plays Y5
            .Play(10) // Alice plays another R1 - stack is at 5, this is a misplay
            .BuildAndAnalyze();

        // Assert - should have a misplay on turn 11
        violations.Should().ContainViolationAtTurn(ViolationType.Misplay, 11);
    }

    [Fact]
    public void PlayAfterStackAdvanced_NoViolation()
    {
        // Player has R2, waits for R1 to be played, then plays R2
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,Y1,G1,B1,P1, R2,Y2,G2,B2,P2, R3,Y3")
            .Play(0)  // Alice plays R1 -> stack at 1
            .Play(5)  // Bob plays R2 -> valid since stack is now at 1
            .BuildAndAnalyze();

        // Assert - no misplays
        violations.Should().NotContainViolation(ViolationType.Misplay);
    }

    [Fact]
    public void MultiplePlayersCanMisplay()
    {
        // Both players misplay in sequence
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R3,Y1,G1,B1,P1, Y3,Y2,G2,B2,P2, R1,Y1")
            .Play(0)  // Alice plays R3 - misplay
            .Play(5)  // Bob plays Y3 - misplay
            .BuildAndAnalyze();

        // Assert - both players should have misplays
        var misplays = violations.OfType(ViolationType.Misplay).ToList();
        misplays.Should().HaveCount(2);
        misplays.Should().Contain(v => v.Player == "Alice");
        misplays.Should().Contain(v => v.Player == "Bob");
    }

    [Fact]
    public void Misplay_HasCorrectSeverity()
    {
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R3,R1,Y1,B1,G1, R2,Y2,B2,G2,P1")
            .Play(0) // Alice misplays R3
            .BuildAndAnalyze();

        // Assert - misplays should be critical severity
        violations.Should().ContainViolationWithSeverity(ViolationType.Misplay, Severity.Critical);
    }

    [Fact]
    public void Misplay_IncludesCardIdentifier()
    {
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R3,R1,Y1,B1,G1, R2,Y2,B2,G2,P1")
            .Play(0) // Alice misplays R3
            .BuildAndAnalyze();

        // Assert - violation should have card info
        var violation = violations.FirstOfType(ViolationType.Misplay);
        violation.Should().NotBeNull();
        violation!.Card.Should().NotBeNull();
        violation.Card!.SuitIndex.Should().Be(CardBuilder.Red);
        violation.Card.Rank.Should().Be(3);
    }
}
