using FluentAssertions;
using MyWebApi.Models;
using MyWebApi.Services;
using MyWebApi.Tests.Builders;
using MyWebApi.Tests.Helpers;
using Xunit;

namespace MyWebApi.Tests.Tests.Level1_Beginner;

/// <summary>
/// Tests for Good Touch Principle violations.
/// Good Touch: Every clue should only touch cards that will eventually be played.
/// Violations:
/// - Touching trash cards (already played)
/// - Touching duplicates (same card clued elsewhere)
/// - Touching "future trash" (card will never be playable due to dead suit)
///
/// Per H-Group conventions, the clue giver is blamed for bad touches.
/// </summary>
public class GoodTouchTests
{
    [Fact]
    public void ClueTouchesTrashCard_CreatesViolation()
    {
        // Arrange: Play R1, then clue R1 to Bob (R1 is now trash)
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,R2,Y1,B1,G1, R1,Y2,B2,G2,P1, R3,Y3")
            .Play(0) // Alice plays R1 -> Red at 1
            .ColorClue(0, "Red") // Bob clues Alice Red - touches R2 (good) but what about...
            .BuildAndAnalyze();

        // Actually, let's redo this test more clearly
        // We need Bob to clue Alice, and Alice has a trash card

        // Better test:
        var (game2, states2, violations2) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,R1,Y1,B1,G1, R2,Y2,B2,G2,P1, R3,Y3") // Alice has R1 at index 0 and 1
            .Play(0) // Alice plays R1 -> Red at 1
            // Now Alice has: [R1(trash), Y1, B1, G1] after drawing
            .RankClue(0, 1) // Bob clues Alice "1" - touches R1 (trash!) and Y1, B1, G1
            .BuildAndAnalyze();

        // Assert
        violations2.Should().ContainViolation(ViolationType.GoodTouchViolation);
    }

    [Fact]
    public void ClueTouchesDuplicateInOtherHand_CreatesViolation()
    {
        // Arrange: Both players have R2, Alice's is clued, Bob clues his own... wait
        // Actually: Alice clues Bob's R2, then later someone clues Alice's R2

        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob", "Charlie")
            .WithDeck(
                "R2,Y1,B1,G1,P1," +  // Alice
                "R2,Y2,B2,G2,P2," +  // Bob
                "R3,Y3,B3,G3,P3," +  // Charlie
                "R4,Y4")             // Draw
            .RankClue(1, 2) // Alice clues Bob "2" - touches R2 (good)
            .RankClue(0, 2) // Bob clues Alice "2" - touches R2 (duplicate of Bob's clued R2!)
            .BuildAndAnalyze();

        // Assert - Bob's clue touched a duplicate
        violations.Should().ContainViolation(ViolationType.GoodTouchViolation);
        violations.Should().ContainViolationForPlayer(ViolationType.GoodTouchViolation, "Bob");
    }

    [Fact]
    public void ClueTouchesDuplicateInSameHand_CreatesViolation()
    {
        // Even worse: clue touches two of the same card in one hand
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R2,R2,Y1,B1,G1, R3,Y2,B2,G2,P1, R4,Y3") // Alice has two R2s
            .RankClue(0, 2) // Bob clues Alice "2" - touches BOTH R2s (one is a duplicate!)
            .BuildAndAnalyze();

        // Assert - this is particularly bad as it creates confusion
        // Note: Current implementation may not detect same-hand duplicates
        // The test defines correct behavior
        violations.Should().ContainViolation(ViolationType.GoodTouchViolation,
            because: "cluing duplicates in the same hand violates good touch");
    }

    [Fact]
    public void ClueTouchesUsefulCard_NoViolation()
    {
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,R2,Y1,B1,G1, R3,Y2,B2,G2,P1, R4,Y3")
            .RankClue(0, 1) // Bob clues Alice "1" - touches R1 (playable!)
            .BuildAndAnalyze();

        // Assert
        violations.Should().NotContainViolation(ViolationType.GoodTouchViolation);
    }

    [Fact]
    public void ClueTouchesBothUsefulAndTrash_CreatesPartialViolation()
    {
        // Clue touches some good cards and some trash
        // This should still be flagged (the trash touch is bad)

        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,R1,Y1,Y1,G1, R2,Y2,B2,G2,P1, R3,Y3") // Alice: R1, R1, Y1, Y1, G1
            .Play(0) // Alice plays R1 -> Red at 1
            // Alice now has: [R1(trash), Y1, Y1, G1, new card]
            .RankClue(0, 1) // Bob clues Alice "1" - touches R1(trash), Y1, Y1, G1
            .BuildAndAnalyze();

        // Assert - violation for touching trash R1
        violations.Should().ContainViolation(ViolationType.GoodTouchViolation);
    }

    [Fact]
    public void ClueTouchesFutureTrash_CreatesViolation()
    {
        // "Future trash" = card that will never be playable because the suit is dead
        // E.g., R3 when both R2s are discarded

        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R2,R3,Y1,B1,G1, R2,Y2,B2,G2,P1, R4,Y3")  // R2 in both hands
            .Discard(0) // Alice discards R2 (first copy)
            .Discard(5) // Bob discards R2 (second copy) - Red suit now dead at rank 1!
            // Alice now has: R3(future trash), Y1, B1, G1, new card
            .RankClue(0, 3) // Bob clues Alice "3" - touches R3 (future trash!)
            .BuildAndAnalyze();

        // Assert - R3 is future trash since Red suit is dead
        // Note: This requires dead suit detection - test defines correct behavior
        violations.Should().ContainViolation(ViolationType.GoodTouchViolation,
            because: "R3 can never be played since both R2s are discarded");
    }

    [Fact]
    public void ColorClueTouchesTrash_CreatesViolation()
    {
        // Same as rank clue, but with color
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,R2,R1,B1,G1, R3,Y2,B2,G2,P1, R4,Y3") // Alice: R1, R2, R1, B1, G1
            .Play(0) // Alice plays R1 -> Red at 1
            // Alice: [R2, R1(trash), B1, G1, new]
            .ColorClue(0, "Red") // Bob clues Alice "Red" - touches R2(good) AND R1(trash)
            .BuildAndAnalyze();

        // Assert
        violations.Should().ContainViolation(ViolationType.GoodTouchViolation);
    }

    [Fact]
    public void GoodTouchViolation_HasWarningSeverity()
    {
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,R1,Y1,B1,G1, R2,Y2,B2,G2,P1, R3,Y3")
            .Play(0)
            .RankClue(0, 1)
            .BuildAndAnalyze();

        violations.Should().ContainViolationWithSeverity(ViolationType.GoodTouchViolation, Severity.Warning);
    }

    [Fact]
    public void GoodTouchViolation_DescriptionIncludesCardInfo()
    {
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,R1,Y1,B1,G1, R2,Y2,B2,G2,P1, R3,Y3")
            .Play(0) // R1 played
            .RankClue(0, 1) // Clue touches trash R1
            .BuildAndAnalyze();

        var violation = violations.FirstOfType(ViolationType.GoodTouchViolation);
        violation.Should().NotBeNull();
        violation!.Description.Should().Contain("Red 1");
        violation.Description.ToLower().Should().Contain("trash");
    }

    [Fact]
    public void MultipleTrashTouched_CreatesMultipleViolations()
    {
        // If a clue touches multiple trash cards, each should be reported
        // 3-player: turn order is Alice (0), Bob (1), Charlie (2), Alice (0)...
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob", "Charlie")
            .WithDeck(
                "R1,Y1,G1,B1,P1," +    // Alice (deck 0-4)
                "R2,Y2,G2,B2,P2," +    // Bob (deck 5-9)
                "R1,Y1,G3,B3,P3," +    // Charlie has R1(10), Y1(11) - will be trash
                "R3,Y3,B4,G4")         // Draw pile
            .Play(0)        // Turn 0: Alice plays R1 (Red stack = 1)
            .Play(5)        // Turn 1: Bob plays R2 (Red stack = 2)
            .Discard(12)    // Turn 2: Charlie discards G3
            .Play(1)        // Turn 3: Alice plays Y1 (Yellow stack = 1)
            .Play(6)        // Turn 4: Bob plays Y2 (Yellow stack = 2)
            .Discard(13)    // Turn 5: Charlie discards B3
            .RankClue(2, 1) // Turn 6: Alice clues Charlie "1" - touches R1(trash) and Y1(trash)
            .BuildAndAnalyze();

        // Assert - should have violations for both trash cards
        violations.OfType(ViolationType.GoodTouchViolation).Should().HaveCountGreaterOrEqualTo(2);
    }
}
