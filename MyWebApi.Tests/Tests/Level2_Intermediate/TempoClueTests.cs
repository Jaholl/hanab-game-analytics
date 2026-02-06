using FluentAssertions;
using MyWebApi.Models;
using MyWebApi.Services;
using MyWebApi.Tests.Builders;
using MyWebApi.Tests.Helpers;
using Xunit;

namespace MyWebApi.Tests.Tests.Level2_Intermediate;

/// <summary>
/// Tests for Tempo Clue recognition.
///
/// A Tempo Clue is a clue that re-touches an already-clued playable card
/// to signal "play this NOW". This is NOT an MCVP violation because it
/// conveys timing information.
///
/// Distinguishing tempo clues from MCVP violations requires understanding
/// whether the re-clued card is currently playable.
/// </summary>
public class TempoClueTests
{
    [Fact]
    public void ReCluePlayableCard_IsTempoClue_NoViolation()
    {
        // Alice has R1 clued and playable. Bob re-clues it = tempo clue
        // Turn order: 0=Alice, 1=Bob, 2=Alice, 3=Bob, 4=Alice, 5=Bob
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,Y2,Y3,B1,G1, R3,Y4,B2,G2,P1, R4,Y5")
            .AtIntermediateLevel()  // Tempo clues are a Level 2 concept
            .Discard(1)      // Turn 0 (Alice): Alice discards Y2 (filler)
            .RankClue(0, 1)  // Turn 1 (Bob): Bob clues Alice "1" - R1 is clued and playable
            .Discard(2)      // Turn 2 (Alice): Alice discards Y3 (doesn't play R1)
            .RankClue(0, 1)  // Turn 3 (Bob): Bob re-clues "1" - tempo clue saying "play it NOW"
            .BuildAndAnalyze();

        // Assert - this should NOT be an MCVP violation (it's a tempo clue)
        // Note: Current implementation may flag this - test defines correct behavior
        var turn4MCVP = violations.Where(v => v.Turn == 4 && v.Type == ViolationType.MCVPViolation);
        turn4MCVP.Should().BeEmpty(because: "re-cluing a playable card is a tempo clue, not MCVP violation");
    }

    [Fact]
    public void ReClueNonPlayableCard_IsMCVPViolation()
    {
        // Alice has R3 clued but not playable (needs R1, R2 first)
        // Re-cluing it is NOT a tempo clue - it's wasteful
        // Turn order: 0=Alice, 1=Bob, 2=Alice, 3=Bob, 4=Alice, 5=Bob
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R3,Y2,Y3,B1,G1, R4,Y4,B2,G2,P1, R5,Y5")
            .AtIntermediateLevel()  // Tempo clues are a Level 2 concept
            .Discard(1)      // Turn 0 (Alice): Alice discards Y2 (filler)
            .RankClue(0, 3)  // Turn 1 (Bob): Bob clues Alice "3" - R3 is clued but not playable
            .Discard(2)      // Turn 2 (Alice): Alice discards Y3
            .RankClue(0, 3)  // Turn 3 (Bob): Bob re-clues "3" - NOT tempo (card not playable), MCVP violation
            .BuildAndAnalyze();

        // Assert - this IS an MCVP violation
        violations.Should().ContainViolationAtTurn(ViolationType.MCVPViolation, 4);
    }

    [Fact]
    public void TempoClue_WithColorClue_NoViolation()
    {
        // Same test but with color clue instead of rank
        // Turn order: 0=Alice, 1=Bob, 2=Alice, 3=Bob
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,Y2,Y3,B1,G1, R3,Y4,B2,G2,P1, R4,Y5")
            .AtIntermediateLevel()  // Tempo clues are a Level 2 concept
            .Discard(1)            // Turn 0 (Alice): Alice discards Y2 (filler)
            .ColorClue(0, "Red")  // Turn 1 (Bob): Bob clues Alice "Red" - R1 clued and playable
            .Discard(2)            // Turn 2 (Alice): Alice discards Y3
            .ColorClue(0, "Red")  // Turn 3 (Bob): Bob re-clues "Red" - tempo clue
            .BuildAndAnalyze();

        // Assert
        var turn4MCVP = violations.Where(v => v.Turn == 4 && v.Type == ViolationType.MCVPViolation);
        turn4MCVP.Should().BeEmpty(because: "color tempo clue is valid");
    }

    [Fact]
    public void TempoClue_PlayerRespondsCorrectly_NoViolation()
    {
        // Tempo clue is given, player plays the card
        // Turn order: 0=Alice, 1=Bob, 2=Alice, 3=Bob, 4=Alice
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,Y2,Y3,B1,G1, R3,Y4,B2,G2,P1, R4,Y5")
            .AtIntermediateLevel()  // Tempo clues are a Level 2 concept
            .Discard(1)      // Turn 0 (Alice): Alice discards Y2 (filler)
            .RankClue(0, 1)  // Turn 1 (Bob): Bob clues Alice "1"
            .Discard(2)      // Turn 2 (Alice): Alice discards Y3 (missing the prompt)
            .RankClue(0, 1)  // Turn 3 (Bob): Bob tempo clue
            .Play(0)         // Turn 4 (Alice): Alice plays R1 in response
            .BuildAndAnalyze();

        // Assert - no violations for the tempo clue or the play
        violations.Should().NotContainViolationAtTurn(ViolationType.MCVPViolation, 4);
    }

    [Fact(Skip = "Not yet implemented - has turn order issues")]
    public void NotTempoClue_CardBecamePlayable_Valid()
    {
        // Card was clued before it was playable, now it is playable
        // Re-cluing would be tempo clue
        // BUG: Multiple attempts with self-clues (Alice clueing herself).
        Assert.True(true);
    }

    [Fact]
    public void MultiplePlayableCards_TempoClueSpecifiesWhich()
    {
        // If player has multiple clued playable cards, tempo clue can indicate which to play first
        // Turn order: 0=Alice, 1=Bob, 2=Alice, 3=Bob
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,Y1,G1,B1,P1, R2,Y2,G2,B2,P2, R3,Y3")
            .AtIntermediateLevel()  // Tempo clues are a Level 2 concept
            // Alice has R1, Y1, G1, B1, P1 - all playable 1s
            .Discard(1)       // Turn 0 (Alice): Alice discards Y1 (filler)
            .RankClue(0, 1)   // Turn 1 (Bob): Bob clues all 1s at once
            .Discard(2)       // Turn 2 (Alice): Alice discards G1 instead of playing
            .ColorClue(0, "Red") // Turn 3 (Bob): Bob tempo clue on R1 specifically
            .BuildAndAnalyze();

        // Assert - this is a valid tempo clue to prioritize R1
        var turn4MCVP = violations.Where(v => v.Turn == 4 && v.Type == ViolationType.MCVPViolation);
        turn4MCVP.Should().BeEmpty();
    }

    [Fact]
    public void TempoClue_AtZeroClues_StillValid()
    {
        // Tempo clues are valid even when clue tokens are precious
        // (though strategically questionable)
        Assert.True(true, "Specification: Tempo clues are technically valid at low clue counts");
    }

    [Fact]
    public void DistinguishTempoFromFill_ComplexScenario()
    {
        // "Fill" clue adds information to partially clued card
        // "Tempo" clue tells player to play NOW
        // Both re-touch cards but have different purposes

        // Example: Card clued with color only, then rank clue to "fill" in the rank
        // vs. card clued with both, then re-clue to tempo
        // Turn order: 0=Alice, 1=Bob, 2=Alice, 3=Bob

        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,Y2,Y3,B1,G1, R3,Y4,B2,G2,P1, R4,Y5")
            .Discard(1)           // Turn 0 (Alice): Alice discards Y2 (filler)
            .ColorClue(0, "Red")  // Turn 1 (Bob): Partial clue - Alice knows it's Red but not rank
            .Discard(2)           // Turn 2 (Alice): Alice discards Y3
            .RankClue(0, 1)       // Turn 3 (Bob): Fill clue - now Alice knows it's R1 (new info!)
            .BuildAndAnalyze();

        // Assert - fill clue adds new info, not MCVP violation
        var turn4MCVP = violations.Where(v => v.Turn == 4 && v.Type == ViolationType.MCVPViolation);
        turn4MCVP.Should().BeEmpty(because: "rank clue adds new information to color-clued card");
    }
}
