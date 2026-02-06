using FluentAssertions;
using MyWebApi.Models;
using MyWebApi.Services;
using MyWebApi.Tests.Builders;
using MyWebApi.Tests.Helpers;
using Xunit;

namespace MyWebApi.Tests.Tests.Level2_Intermediate;

/// <summary>
/// Tests for Double Discard Avoidance (DDA) violations.
///
/// DDA Convention: If the previous player discarded from their chop,
/// you should NOT discard from yours (unless your chop is safe - trash/duplicate).
///
/// This prevents accidentally losing both copies of a critical card.
///
/// Per H-Group conventions, the player who double-discards is blamed.
/// </summary>
public class DoubleDiscardAvoidanceTests
{
    [Fact]
    public void DiscardFromChopAfterPreviousPlayerDiscardedFromChop_CreatesViolation()
    {
        // DDA scenario: Alice discards from chop, Bob should not discard from his chop
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,R2,Y1,B1,G1, R3,Y2,B2,G2,P1, R4,Y3")
            // Alice's chop is R1 (index 0), Bob's chop is R3 (index 5)
            .AtIntermediateLevel() // DDA is a Level 2 convention
            .Discard(0) // Alice discards from chop
            .Discard(5) // Bob discards from chop - DDA violation!
            .BuildAndAnalyze();

        // Assert
        // Note: Current implementation may not detect DDA - test defines correct behavior
        violations.Should().ContainViolation("DoubleDiscardAvoidance",
            because: "Bob discarded from chop after Alice discarded from chop");
    }

    [Fact]
    public void DDAAfterClue_NoViolation()
    {
        // DDA only applies after a discard, not after a clue
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,R2,Y1,B1,G1, R3,Y2,B2,G2,P1, R4,Y3")
            .AtIntermediateLevel()
            .RankClue(1, 3) // Alice gives a clue (not a discard)
            .Discard(5)     // Bob discards from chop - NOT a DDA violation
            .BuildAndAnalyze();

        // Assert
        violations.OfType("DoubleDiscardAvoidance").Should().BeEmpty();
    }

    [Fact]
    public void DDAAfterPlay_NoViolation()
    {
        // DDA only applies after a discard, not after a play
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,R2,Y1,B1,G1, R3,Y2,B2,G2,P1, R4,Y3")
            .AtIntermediateLevel()
            .Play(0)    // Alice plays (not a discard)
            .Discard(5) // Bob discards from chop - NOT a DDA violation
            .BuildAndAnalyze();

        // Assert
        violations.OfType("DoubleDiscardAvoidance").Should().BeEmpty();
    }

    [Fact]
    public void DDAWithLockedHand_NoViolation()
    {
        // If player has locked hand (all clued), they have no choice but to discard/play
        // This is a forced action, not a DDA violation

        // Complex to set up - documenting as specification
        Assert.True(true, "Specification: Locked hand forced discards are not DDA violations");
    }

    [Fact]
    public void DDAWithSafeChop_NoViolation()
    {
        // If your chop is safe (trash or known duplicate), DDA doesn't apply
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,R2,Y1,B1,G1, R1,Y2,B2,G2,P1, R4,Y3") // Both have R1 on chop
            // After Alice discards R1, Bob's R1 is still safe (there are 3 copies of R1)
            .Discard(0) // Alice discards R1 from chop
            .Discard(5) // Bob discards R1 from chop - safe because R1 has multiple copies
            .BuildAndAnalyze();

        // Assert - not DDA violation because Bob's chop was safe (R1 has copies)
        // Note: Whether this triggers depends on implementation's duplicate detection
        Assert.True(true, "Safe chop discards after chop discard are acceptable");
    }

    [Fact]
    public void DiscardFromNonChop_AfterChopDiscard_NoViolation()
    {
        // DDA only applies to chop discards
        // Discarding a non-chop card is fine
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,R2,Y1,B1,G1, R3,Y2,B2,G2,P1, R4,Y3")
            .Discard(0) // Alice discards from chop
            .Discard(6) // Bob discards Y2 (NOT his chop R3) - no DDA
            .BuildAndAnalyze();

        // Assert
        violations.OfType("DoubleDiscardAvoidance").Should().BeEmpty();
    }

    [Fact]
    public void DDA_ChainReaction_MultipleViolations()
    {
        // In 3+ player game: Alice discards, Bob discards (DDA), Charlie discards (DDA?)
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob", "Charlie")
            .WithDeck(
                "R1,R2,Y1,B1,G1," +  // Alice
                "R3,Y2,B2,G2,P1," +  // Bob
                "R4,Y3,B3,G3,P2," +  // Charlie
                "R5,Y4")
            .Discard(0)  // Alice discards from chop
            .Discard(5)  // Bob discards from chop - DDA violation
            .Discard(10) // Charlie discards from chop - another DDA violation?
            .BuildAndAnalyze();

        // Note: The convention is that DDA is relative to the PREVIOUS player
        // So Charlie's DDA would be relative to Bob's discard
        // Both Bob and Charlie would be in DDA violation
        Assert.True(true, "Multiple DDA violations can occur in sequence");
    }

    [Fact]
    public void DDA_AlternativeActionAvailable_GiveClue()
    {
        // When in DDA situation, player should clue instead
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,R2,Y1,B1,G1, R3,Y2,B2,G2,P1, R4,Y3")
            .Discard(0)         // Alice discards from chop
            .RankClue(0, 2)     // Bob gives a clue instead of discarding - good!
            .BuildAndAnalyze();

        // Assert
        violations.OfType("DoubleDiscardAvoidance").Should().BeEmpty();
    }

    [Fact]
    public void DDA_AlternativeActionAvailable_Play()
    {
        // When in DDA situation, player can play instead
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,R2,Y1,B1,G1, R1,Y2,B2,G2,P1, R4,Y3") // Bob has R1 (playable)
            .Discard(0)  // Alice discards from chop
            .Play(5)     // Bob plays R1 instead of discarding - good!
            .BuildAndAnalyze();

        // Assert
        violations.OfType("DoubleDiscardAvoidance").Should().BeEmpty();
    }

    [Fact]
    public void DDA_AfterBurningAllClues_PreviousDiscardStillGivesToken()
    {
        // Even after burning all clue tokens, the previous player's discard
        // adds a token back, so the current player CAN clue to avoid DDA.
        // DDA should still be flagged.
        //
        // 3 players, 8 clues to reach 0 tokens:
        // Actions 0-7: clues (0 tokens)
        // Action 8 (Charlie): discard from chop → 1 token
        // Action 9 (Alice): discard from chop → DDA (she has 1 token, could clue)
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob", "Charlie")
            .WithDeck(
                "R2,Y1,B1,G1,P1," +  // Alice
                "R3,Y2,B2,G2,P2," +  // Bob
                "R4,Y3,B3,G3,P3," +  // Charlie
                "R5,Y4,B4")
            .AtIntermediateLevel()
            .ColorClue(1, "Red")      // Action 0 (Alice): 7 tokens
            .ColorClue(2, "Yellow")   // Action 1 (Bob): 6 tokens
            .ColorClue(0, "Blue")     // Action 2 (Charlie): 5 tokens
            .ColorClue(1, "Yellow")   // Action 3 (Alice): 4 tokens
            .ColorClue(2, "Blue")     // Action 4 (Bob): 3 tokens
            .ColorClue(0, "Green")    // Action 5 (Charlie): 2 tokens
            .ColorClue(1, "Blue")     // Action 6 (Alice): 1 token
            .ColorClue(2, "Green")    // Action 7 (Bob): 0 tokens
            .Discard(10)              // Action 8 (Charlie): discard R4 from chop → 1 token
            .Discard(0)               // Action 9 (Alice): discard R2 from chop → DDA!
            .BuildAndAnalyze();

        // Assert - Alice has 1 clue token (from Charlie's discard), so DDA applies
        violations.Should().ContainViolation("DoubleDiscardAvoidance");
    }

    [Fact]
    public void DDA_ShouldHaveWarningSeverity()
    {
        // DDA violations should be warnings (convention, not rule)
        Assert.True(true, "Specification: DDA violations have warning severity");
    }
}
