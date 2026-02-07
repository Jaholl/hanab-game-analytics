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

    [Fact]
    public void FullyKnownButNotPlayable_Discarded_NoViolation()
    {
        // Edge case: Bob has a fully-known card (R3 with Red+3 clued) but R3
        // is NOT playable (Red stack = 0, needs R1 first). Discarding a
        // fully-known non-playable card should not trigger InformationLock.
        // The checker correctly checks IsCardPlayable before flagging.
        //
        // 2-player, hand size 5.
        // Alice(0-4): R2,Y2,B1,G1,P1
        // Bob(5-9): R3,Y1,B2,G2,P2
        // Draw: R4,Y3
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R2,Y2,B1,G1,P1,R3,Y1,B2,G2,P2,R4,Y3")
            .AtAdvancedLevel()
            .ColorClue(1, "Red")  // T1 Alice: clue Bob Red -> marks R3
            .RankClue(0, 2)       // T2 Bob: clue Alice 2 (filler)
            .RankClue(1, 3)       // T3 Alice: clue Bob 3 -> R3 fully known
            .Discard(5)           // T4 Bob: discards R3 (fully known but NOT playable)
            .BuildAndAnalyze();

        // R3 is not playable (stacks at 0). No InformationLock violation.
        violations.Should().NotContainViolation(ViolationType.InformationLock);
    }

    [Fact]
    public void FullyKnownPlayable_DiscardedDifferentCard_NoViolation()
    {
        // Edge case: Bob has R1 fully known (playable), but he discards a
        // DIFFERENT card (not the fully-known one). This is not an InformationLock
        // violation because the checker only fires when the discarded card itself
        // is the fully-known playable card.
        //
        // 2-player, hand size 5.
        // Alice(0-4): R2,Y2,B1,G1,P1
        // Bob(5-9): R1,Y1,B2,G2,P2
        // Draw: R3,Y3
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R2,Y2,B1,G1,P1,R1,Y1,B2,G2,P2,R3,Y3")
            .AtAdvancedLevel()
            .ColorClue(1, "Red")  // T1 Alice: clue Bob Red -> marks R1
            .RankClue(0, 2)       // T2 Bob: clue Alice 2 (filler)
            .RankClue(1, 1)       // T3 Alice: clue Bob 1 -> R1 fully known
            .Discard(9)           // T4 Bob: discards P2 (chop) instead of playing R1
            .BuildAndAnalyze();

        // Bob discarded P2, not the fully-known R1. The checker only looks at
        // the discarded card, not other cards in hand. No violation on THIS discard.
        violations.Should().NotContainViolation(ViolationType.InformationLock);
    }

    [Fact]
    public void FullyKnownCard_SuitDead_DiscardIsCorrect()
    {
        // Edge case: Bob has G2 fully known (Green + 2 clued). But all G1s
        // have been discarded, making the Green suit dead. G2 will never be
        // playable. Discarding it is correct.
        //
        // 3-player, hand size 5.
        // We need G1 to be discarded. There are 3 copies of G1 in standard deck.
        // We need all 3 discarded to make G dead.
        // For simplicity, put G1s in positions where they get discarded.
        //
        // Alice(0-4): G1,G1,G1,Y1,P1  - three G1s
        // Bob(5-9): G2,Y2,B2,R2,P2
        // Charlie(10-14): Y3,B3,R3,P3,R4
        // Draw: R5,Y4,B4,G3,P4,B5,Y5
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob", "Charlie")
            .WithDeck("G1,G1,G1,Y1,P1,G2,Y2,B2,R2,P2,Y3,B3,R3,P3,R4,R5,Y4,B4,G3,P4,B5,Y5")
            .AtAdvancedLevel()
            .Discard(0)              // T1 Alice: discard G1 (idx 0)
            .Discard(9)              // T2 Bob: discard P2 (chop)
            .Discard(14)             // T3 Charlie: discard R4 (chop)
            .Discard(1)              // T4 Alice: discard G1 (idx 1)
            .Discard(8)              // T5 Bob: discard R2 (new chop)
            .Discard(13)             // T6 Charlie: discard P3
            .Discard(2)              // T7 Alice: discard G1 (idx 2) -> ALL G1s gone, Green dead
            .ColorClue(1, "Green")  // T8 Bob: err wait, this is Bob's turn? No.
            // T1=Alice(0), T2=Bob(1), T3=Charlie(2), T4=Alice(0)...
            // T7=Alice, T8=Bob, T9=Charlie, T10=Alice, T11=Bob
            .RankClue(0, 1)          // T8 Bob: clue Alice 1 (filler)
            .ColorClue(1, "Green")  // T9 Charlie: clue Bob Green -> marks G2
            .RankClue(1, 2)          // T10 Alice: clue Bob 2 -> G2 fully known
            .Discard(5)              // T11 Bob: discard G2 (fully known, but Green is dead)
            .BuildAndAnalyze();

        // G2 is fully known but not playable (suit is dead). No violation.
        violations.Should().NotContainViolation(ViolationType.InformationLock);
    }
}
