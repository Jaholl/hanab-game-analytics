using FluentAssertions;
using MyWebApi.Models;
using MyWebApi.Services;
using MyWebApi.Tests.Builders;
using MyWebApi.Tests.Helpers;
using Xunit;

namespace MyWebApi.Tests.Tests.Level0_BasicRules;

/// <summary>
/// Tests for illegal action detection.
/// Illegal actions are moves that violate the fundamental rules of Hanabi:
/// - Discarding at 8 clue tokens (must clue or play)
/// - Giving a clue at 0 tokens (must discard or play)
///
/// Per H-Group conventions, the player who made the illegal action is blamed.
/// </summary>
public class IllegalActionTests
{
    [Fact]
    public void DiscardAt8ClueTokens_ShouldBeIllegal()
    {
        // Arrange: Game starts at 8 clue tokens
        // Discarding at 8 clues is illegal - you MUST clue or play

        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,R2,Y1,B1,G1, R3,Y2,B2,G2,P1, R4,Y3")
            .Discard(0) // Alice discards at 8 clues - ILLEGAL
            .BuildAndAnalyze();

        // Assert - this should create a violation
        // Note: Current implementation may not check this - test defines correct behavior
        // This test may fail initially, indicating the rule needs implementation
        violations.Should().ContainViolation("IllegalDiscard",
            because: "discarding at 8 clue tokens is against Hanabi rules");
    }

    [Fact]
    public void DiscardAt7ClueTokens_IsLegal()
    {
        // Arrange: First reduce clue tokens, then discard
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,R2,Y1,B1,G1, R3,Y2,B2,G2,P1, R4,Y3")
            .ColorClue(1, "Red") // Alice clues Bob (8 -> 7 clues)
            .Discard(5) // Bob discards at 7 clues - legal
            .BuildAndAnalyze();

        // Assert - no illegal discard violation
        violations.OfType("IllegalDiscard").Should().BeEmpty();
    }

    [Fact(Skip = "No checker implemented yet")]
    public void GiveClueAt0Tokens_ShouldBeIllegal()
    {
        // Specification: Giving a clue at 0 clue tokens should be flagged as illegal.
        // The game simulator likely prevents this, but our analyzer should detect it
        // if it somehow happened. Implementation requires either modifying game state
        // directly or setting up a scenario that reaches 0 clue tokens and then clues.
    }

    [Fact]
    public void PlayAt0ClueTokens_IsLegal()
    {
        // Playing is always legal regardless of clue count
        // Set up a scenario where we have 0 clues

        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob", "Charlie")
            .WithDeck(
                "R1,R2,Y1,B1,G1," +     // Alice (5 cards)
                "R3,Y2,B2,G2,P1," +     // Bob (5 cards)
                "R4,Y3,B3,G3,P2," +     // Charlie (5 cards)
                "R5,Y4,B4,G4,P3")       // Draw pile
            // Give 8 clues to get to 0 tokens
            .ColorClue(1, "Red")   // Alice clues Bob (8->7)
            .ColorClue(2, "Yellow") // Bob clues Charlie (7->6)
            .ColorClue(0, "Red")   // Charlie clues Alice (6->5)
            .ColorClue(1, "Yellow") // Alice clues Bob (5->4)
            .ColorClue(2, "Blue")  // Bob clues Charlie (4->3)
            .ColorClue(0, "Yellow") // Charlie clues Alice (3->2)
            .ColorClue(1, "Blue")  // Alice clues Bob (2->1)
            .ColorClue(2, "Green") // Bob clues Charlie (1->0)
            .Play(10)              // Charlie plays R4 at 0 clues - legal!
            .BuildAndAnalyze();

        // Assert - no violations for playing at 0 clues
        violations.Where(v => v.Turn == 9).Should().NotContain(v => v.Type == "IllegalPlay");
    }

    [Fact]
    public void DiscardAt0ClueTokens_IsLegal()
    {
        // Discarding at 0 clues is legal (in fact, often necessary)
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob", "Charlie")
            .WithDeck(
                "R1,R2,Y1,B1,G1," +
                "R3,Y2,B2,G2,P1," +
                "R4,Y3,B3,G3,P2," +
                "R5,Y4,B4,G4,P3")
            // Give 8 clues
            .ColorClue(1, "Red")
            .ColorClue(2, "Yellow")
            .ColorClue(0, "Red")
            .ColorClue(1, "Yellow")
            .ColorClue(2, "Blue")
            .ColorClue(0, "Yellow")
            .ColorClue(1, "Blue")
            .ColorClue(2, "Green")
            // Now at 0 clues
            .Discard(10) // Charlie discards at 0 clues - legal
            .BuildAndAnalyze();

        // Assert - no illegal action for discarding at 0 clues
        violations.Where(v => v.Turn == 9).Should().NotContain(v => v.Type == "IllegalDiscard");
    }

    [Fact]
    public void DiscardAt8Clues_DescriptionShouldExplainWhy()
    {
        // The violation description should explain the rule
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,R2,Y1,B1,G1, R3,Y2,B2,G2,P1")
            .Discard(0)
            .BuildAndAnalyze();

        violations.Should().ContainViolation(ViolationType.IllegalDiscard,
            because: "discarding at 8 clue tokens is against Hanabi rules");
        var illegalDiscard = violations.FirstOfType(ViolationType.IllegalDiscard);
        illegalDiscard.Should().NotBeNull();
        illegalDiscard!.Description.Should().Contain("8",
            because: "should mention max clue tokens");
    }

    [Fact(Skip = "No checker implemented yet")]
    public void LockedHand_CannotDiscard()
    {
        // Specification: If all cards in hand are clued, the player has a "locked hand".
        // They cannot legally discard (no unclued cards to discard).
        // They must play or give a clue.
        // Actual locked hand detection requires specific game states that are
        // complex to set up and a dedicated checker to detect the violation.
    }
}
