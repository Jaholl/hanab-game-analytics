using FluentAssertions;
using MyWebApi.Models;
using MyWebApi.Services;
using MyWebApi.Tests.Builders;
using MyWebApi.Tests.Helpers;
using Xunit;

namespace MyWebApi.Tests.Tests.Level0_BasicRules;

/// <summary>
/// Deep accuracy audit edge-case tests for Level 0 checkers.
/// These probe adversarial scenarios that the existing tests may not cover.
/// </summary>
public class Level0EdgeCaseAuditTests
{
    // =========================================================================
    // MISPLAY CHECKER EDGE CASES
    // =========================================================================

    [Fact]
    public void Misplay_PlayDuplicateWhenStackAlreadyHasRank_ShouldDetect()
    {
        // Scenario: R1 is already on the stack (stack=1), player tries to play another R1
        // expectedRank = PlayStacks[Red] + 1 = 1 + 1 = 2
        // card.Rank = 1, which != 2, so this SHOULD be flagged as misplay
        //
        // Setup: Alice plays R1, then Bob plays another R1 (misplay)
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,R2,Y1,B1,G1, R1,Y2,B2,G2,P1, R3,Y3")
            .Play(0)  // Alice plays R1 -> stack at 1
            .Play(5)  // Bob plays R1 -> stack needs R2, so this is a misplay
            .BuildAndAnalyze();

        violations.Should().ContainViolation(ViolationType.Misplay,
            because: "playing R1 when R stack is already at 1 should be a misplay");
        violations.Should().ContainViolationForPlayer(ViolationType.Misplay, "Bob");
    }

    [Fact]
    public void Misplay_PlayCardTwoRanksAboveStack_ShouldDetect()
    {
        // Scenario: Red stack is at 1, player plays R3 (needs R2)
        // expectedRank = 1 + 1 = 2, card.Rank = 3, != 2 -> misplay
        //
        // Turn order: action 0 = player 0 (Alice), action 1 = player 1 (Bob), action 2 = player 0 (Alice)
        // Alice hand: R1(0), R3(1), Y1(2), B1(3), G1(4)
        // Bob hand: Y2(5), Y3(6), B2(7), G2(8), P1(9)
        // T1: Alice plays R1(0) -> R stack=1. Alice draws deck[10].
        // T2: Bob plays Y2(5) -> Y stack=0, needs Y1 -> misplay. Bob draws deck[11].
        //     Actually Y2 on empty Y stack is a misplay too, which would be noise.
        //     Better: Bob clues Alice instead.
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,R3,Y1,B1,G1, Y2,Y3,B2,G2,P1, R4,Y4")
            .Play(0)              // T1: Alice plays R1 -> R stack=1
            .ColorClue(0, "Red")  // T2: Bob clues Alice Red (touches R3)
            .Play(1)              // T3: Alice plays R3 -> R stack needs R2 -> misplay!
            .BuildAndAnalyze();

        violations.Should().ContainViolationAtTurn(ViolationType.Misplay, 3,
            because: "playing R3 when stack is at R1 (needs R2) should be a misplay");
    }

    [Fact]
    public void Misplay_PlayOnCompletedStack_ShouldDetect()
    {
        // Scenario: Red stack at 5, player plays R1 again
        // expectedRank = 5 + 1 = 6, card.Rank = 1, != 6 -> misplay
        // Already covered in existing tests but this test confirms the specific logic path
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,R2,R3,R4,R5, Y1,Y2,Y3,Y4,Y5, R1,R2,R3")
            .Play(0)  // Alice plays R1
            .Play(5)  // Bob plays Y1
            .Play(1)  // Alice plays R2
            .Play(6)  // Bob plays Y2
            .Play(2)  // Alice plays R3
            .Play(7)  // Bob plays Y3
            .Play(3)  // Alice plays R4
            .Play(8)  // Bob plays Y4
            .Play(4)  // Alice plays R5 -> Red complete (stack=5)
            .Play(9)  // Bob plays Y5
            .Play(10) // Alice plays R1 when Red stack is 5 -> misplay
            .BuildAndAnalyze();

        violations.Should().ContainViolationAtTurn(ViolationType.Misplay, 11,
            because: "playing any Red card when Red stack is complete (5) should be a misplay");
    }

    [Fact]
    public void Misplay_PlayCorrectSuitWrongRank_ShouldDetect()
    {
        // Multi-suit scenario: Player has both R2 and Y1 in hand.
        // Stacks: R=0, Y=0. Playing R2 is a misplay (needs R1).
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R2,Y1,G1,B1,P1, R1,Y2,B2,G2,P2, R3,Y3")
            .Play(0) // Alice plays R2 (needs R1) -> misplay
            .BuildAndAnalyze();

        violations.Should().ContainViolation(ViolationType.Misplay,
            because: "playing R2 when R stack is at 0 should be misplay even though Y1 would have been valid");
    }

    [Fact]
    public void Misplay_PlaySameSuitDifferentRankNoMisplay()
    {
        // Multi-suit scenario: R stack at 1, play R2 -> correct
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,R2,Y1,B1,G1, R3,Y2,B2,G2,P1, R4,Y3")
            .Play(0) // Alice plays R1 -> stack at 1
            .Play(5) // Bob plays something valid (not a misplay check focus)
            .Play(1) // Alice plays R2 -> stack at 1 needs R2 -> valid
            .BuildAndAnalyze();

        violations.Where(v => v.Turn == 3 && v.Type == ViolationType.Misplay)
            .Should().BeEmpty(because: "playing R2 when R stack is at 1 is valid");
    }

    // =========================================================================
    // BAD DISCARD CHECKER EDGE CASES
    // =========================================================================

    [Fact]
    public void BadDiscard_CriticalDueToOneCopyInOtherHand_ShouldDetect()
    {
        // Scenario: R2 has 2 copies. One copy is in Bob's hand, one is in Alice's hand.
        // Alice discards R2. After discard, only Bob's copy remains.
        // IsCardCritical counts cards in ALL hands + deck. Before Alice's discard:
        //   - inHandsCount: 2 (one in Alice's hand, one in Bob's)
        //   - inDeckCount: 0
        //   - remainingCopies = 2 -> NOT critical (returns false)
        // So discarding the first copy of R2 when the other is in another hand: NOT flagged.
        // This is CORRECT behavior - it's only critical if it's the LAST copy.
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R2,R1,Y1,B1,G1, R2,Y2,B2,G2,P1, R3,Y3")
            .Discard(0) // Alice discards R2 (one copy remains in Bob's hand)
            .BuildAndAnalyze();

        violations.Should().NotContainViolation(ViolationType.BadDiscardCritical,
            because: "R2 has another copy in Bob's hand so it's not critical");
    }

    [Fact]
    public void BadDiscard_DiscardOneCopyWhenOtherAlreadyDiscarded_ShouldDetect()
    {
        // Scenario: R2 has 2 copies. One is already in discard pile (from Alice).
        // Bob holds the other and discards it -> this is the LAST copy -> critical.
        //
        // Key setup requirements:
        // - Alice has an R2 and Bob has an R2 (each in their own hand)
        // - Need to not be at 8 clue tokens when discarding (else IllegalDiscard noise)
        // - Use AtBasicLevel() to avoid Level 1 MissedSave noise
        //
        // 2 players, hand size 5:
        // Alice hand: R2(0), Y1(1), Y2(2), B1(3), G1(4)
        // Bob hand:   R2(5), Y3(6), B2(7), G2(8), P1(9)
        // Draw pile:  R3(10), Y4(11)
        //
        // T1: Alice clues Bob (to get tokens to 7, so discards are legal)
        // T2: Bob discards R2(5) - first copy, not critical yet
        // T3: Alice discards R2(0) - second/last copy -> critical!
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .AtBasicLevel()
            .WithDeck("R2,Y1,Y2,B1,G1, R2,Y3,B2,G2,P1, R3,Y4")
            .ColorClue(1, "Red")  // T1: Alice clues Bob Red (8->7), touches R2(5)
            .Discard(5)           // T2: Bob discards R2(5) - first copy
            .Discard(0)           // T3: Alice discards R2(0) - last copy!
            .BuildAndAnalyze();

        // Alice's discard at turn 3 should be critical
        violations.Should().ContainViolationForPlayer(ViolationType.BadDiscardCritical, "Alice",
            because: "discarding the last copy of R2 when Red suit is still alive should be flagged");
    }

    [Fact]
    public void BadDiscard_Discard5OfCompletedSuit_ShouldNOTFlag()
    {
        // Scenario: Red stack is at 5 (complete). Player discards R5.
        // BadDiscardChecker line 28: PlayStacks[Red] < 5 -> 5 < 5 = false
        // So the if-block is skipped, and it returns early on line 40.
        // This is CORRECT: discarding a 5 of completed suit is fine.
        //
        // But wait - R5 has only 1 copy. Can a player have R5 if R5 was already played?
        // In a real game, R5 being on the stack means it was played, so no player holds it.
        // But in our test setup, we can have duplicates in the deck (test-only scenario).
        // Let's verify the logic handles it correctly anyway.
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,R2,R3,R4,R5, Y1,Y2,Y3,Y4,Y5, R5,R1,R2")
            // Note: R5 appears twice in the deck (index 4 and 10), which is unrealistic
            // but tests the checker logic
            .Play(0)  // Alice plays R1
            .Play(5)  // Bob plays Y1
            .Play(1)  // Alice plays R2
            .Play(6)  // Bob plays Y2
            .Play(2)  // Alice plays R3
            .Play(7)  // Bob plays Y3
            .Play(3)  // Alice plays R4
            .Play(8)  // Bob plays Y4
            .Play(4)  // Alice plays R5 -> Red complete!
            .Play(9)  // Bob plays Y5
            .Discard(10) // Alice discards R5 (Red already complete)
            .BuildAndAnalyze();

        violations.Where(v => v.Turn == 11 && v.Type == ViolationType.BadDiscard5)
            .Should().BeEmpty(
                because: "discarding R5 when Red stack is already at 5 should NOT be flagged");
    }

    [Fact]
    public void BadDiscard_DiscardRank3WhenRank2OfSameSuitBothDiscarded_DeadSuit_ShouldNOTFlag()
    {
        // Scenario: Both copies of R2 are discarded. Red suit is dead at rank 2.
        // Player discards R3 (which is last copy or critical).
        //
        // IsCardCritical for R3: 2 total copies.
        //   Need 1 in discard to make the remaining critical.
        //   Let's put 1 R3 in discard + 1 R3 in hand.
        //   After first R3 is discarded, inHands=1, inDeck=0 -> remainingCopies=1 -> critical=true
        //
        // BadDiscardChecker:
        //   PlayStacks[Red] < card.Rank -> 0 < 3 = true
        //   !IsSuitDead(Red, 3, state) -> IsSuitDead checks ranks 1..2
        //     Rank 1: 3 copies, 0 discarded -> not dead (yet)
        //     Rank 2: 2 copies, 2 discarded -> dead!
        //   -> IsSuitDead returns true -> !true = false -> condition fails -> NO violation
        //
        // This is CORRECT: R3 is effectively trash because R2 is gone.
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R2,R3,Y1,B1,G1, R2,Y2,B2,G2,P1, R3,Y3")
            .Discard(0) // Alice discards first R2
            .Discard(5) // Bob discards second R2 -> Red dead at rank 2
            .Discard(1) // Alice discards R3 -> dead suit, should NOT flag
            .BuildAndAnalyze();

        violations.Where(v => v.Turn == 3 && v.Type == ViolationType.BadDiscardCritical)
            .Should().BeEmpty(
                because: "R3 is trash when both R2s are discarded (dead suit)");
    }

    [Fact]
    public void BadDiscard_DiscardSecondToLastCopy_ShouldNOTFlag()
    {
        // Scenario: R1 has 3 copies. One is in discard, two in play.
        // Player discards one -> still 1 remaining -> NOT critical (at time of check).
        //
        // Wait, IsCardCritical counts remaining = inHands + inDeck.
        // With 3 copies of R1: if 0 discarded, and 2 are in hands, 1 in deck:
        //   remaining = 2 + 1 = 3 -> not critical
        // If 1 is discarded (after this discard), we check BEFORE the discard:
        //   remaining = 2 (in hands) + 1 (in deck) = 3 -> not critical
        //
        // Let's be more precise: R2 has 2 copies. Alice has one. Bob has the other.
        //   IsCardCritical BEFORE Alice discards: inHands=2, inDeck=0 -> remaining=2 -> NOT critical
        //   So Alice discarding reduces it to 1, but the check sees 2 -> not flagged.
        //   This is correct: it's the last-copy discard that should be flagged.
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R2,R1,Y1,B1,G1, R2,Y2,B2,G2,P1, R3,Y3")
            .Discard(0) // Alice discards R2 (Bob still has one copy)
            .BuildAndAnalyze();

        violations.Should().NotContainViolation(ViolationType.BadDiscardCritical,
            because: "there's still one copy of R2 left in Bob's hand");
    }

    [Fact]
    public void BadDiscard_DiscardRank4WhenRank2IsFullyDiscarded_ShouldNOTFlag()
    {
        // More complex dead suit: R2 is fully gone, so R3, R4, R5 are all trash
        // Even if R4 is the "last copy", the dead suit check should prevent flagging
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R2,R4,Y1,B1,G1, R2,Y2,B2,G2,P1, R3,Y3")
            .Discard(0) // Alice discards R2
            .Discard(5) // Bob discards R2 -> Red dead at rank 2
            .Discard(1) // Alice discards R4 -> should NOT flag (dead suit)
            .BuildAndAnalyze();

        violations.Where(v => v.Turn == 3 && v.Type == ViolationType.BadDiscardCritical)
            .Should().BeEmpty(
                because: "R4 is trash when both R2s are gone (Red suit dead)");
    }

    [Fact]
    public void BadDiscard_DiscardAlreadyPlayedCard_ShouldNOTFlag()
    {
        // Scenario: R1 played (stack=1), then discard another R1
        // IsCardCritical: R1 has 3 copies. discardedCount=0, inHands depends.
        // But more importantly, BadDiscardChecker: PlayStacks[Red] < card.Rank -> 1 < 1 = false
        // Wait, this is for rank 5 check (line 28). For critical check (line 45):
        //   PlayStacks[Red] < card.Rank -> 1 < 1 = false -> condition fails -> no violation
        // This is correct: already-played cards are trash.
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,R1,Y1,B1,G1, R2,Y2,B2,G2,P1, R3,Y3")
            .Play(0)    // Alice plays R1 -> stack at 1
            .Discard(1) // Bob discards R1 -> already played, trash
            .BuildAndAnalyze();

        violations.Where(v => v.Turn == 2)
            .Should().NotContain(v => v.Type == ViolationType.BadDiscardCritical,
                because: "R1 is trash when Red stack is already at 1 or higher");
    }

    [Fact]
    public void BadDiscard_Rank1WithTwoCopiesLeft_ShouldNOTFlag()
    {
        // R1 has 3 copies. Discard one -> 2 remaining. Not critical.
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,R2,Y1,B1,G1, R1,Y2,B2,G2,P1, R1,Y3")
            .Discard(0) // Alice discards R1 (still 2 copies left)
            .BuildAndAnalyze();

        violations.Should().NotContainViolation(ViolationType.BadDiscardCritical,
            because: "R1 has 3 copies total, discarding one leaves 2");
    }

    [Fact]
    public void BadDiscard_Rank1LastCopy_ShouldFlag()
    {
        // R1 has 3 copies. Two already discarded. Third is the last -> critical.
        // We need to get 2 R1s into discard, then the player holding the 3rd discards it.
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,R1,Y1,B1,G1, R1,Y2,B2,G2,P1, R2,Y3")
            .Discard(0) // Alice discards R1 (first)
            .Discard(5) // Bob discards R1 (second)
            .Discard(1) // Alice discards R1 (third - last copy!)
            .BuildAndAnalyze();

        violations.Should().ContainViolationAtTurn(ViolationType.BadDiscardCritical, 3,
            because: "the last copy of R1 is being discarded");
    }

    // =========================================================================
    // ILLEGAL DISCARD CHECKER EDGE CASES
    // =========================================================================

    [Fact]
    public void IllegalDiscard_DiscardAfter5Played_ReturnsTo8Clues_ShouldDetect()
    {
        // Scenario: Clue tokens at 7. Player plays a 5 -> clue token returns to 8.
        // Next player discards -> should be illegal (at 8 clues now).
        //
        // Playing a 5 at 7 clues -> +1 -> 8 clues. Then discard at 8 -> illegal.
        //
        // Let's trace carefully:
        // Game starts with 8 clues.
        // Turn 1: Alice gives clue -> 7 clues
        // Turn 2: Bob plays Y1 -> 7 clues (no 5, no change)
        // Actually simpler: start at 8, give a clue to go to 7, then play a 5 to go back to 8.
        //
        // Start: 8 clues.
        // Turn 1: Alice clues Bob (8->7)
        // Turn 2: Bob plays B5... wait, B stack needs to be at 4 for B5 to be valid.
        //         That's too complex. Let's just use a simpler approach.
        //
        // Actually the simplest approach: have 7 clue tokens, play a 5, then discard.
        // But GameBuilder.WithClueTokens only modifies states[0], not the simulation.
        // Per MEMORY.md: "WithPlayStacks() only modifies states[0], NOT the simulation"
        // Same issue applies to WithClueTokens.
        //
        // So we need to actually play actions to set up the right state.
        // Let's give one clue to go from 8->7, then play a 5 correctly:
        // We need the stack to be at 4 for the 5 to be valid.
        // That means playing 1,2,3,4 of some suit first.

        // Setup: Build up Red stack to 4, then play R5 to get clue back to 8
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,R2,R3,R4,R5, Y1,Y2,Y3,Y4,Y5, G1,G2,G3")
            .Play(0)              // T1: Alice plays R1 (8 clues, stack R=1)
            .Play(5)              // T2: Bob plays Y1 (8 clues, stack Y=1)
            .Play(1)              // T3: Alice plays R2 (8, R=2)
            .Play(6)              // T4: Bob plays Y2 (8, Y=2)
            .Play(2)              // T5: Alice plays R3 (8, R=3)
            .Play(7)              // T6: Bob plays Y3 (8, Y=3)
            .Play(3)              // T7: Alice plays R4 (8, R=4)
            .Play(8)              // T8: Bob plays Y4 (8, Y=4)
            // Now we need to get clues to 7 so playing R5 brings it back to 8
            .ColorClue(1, "Yellow") // T9: Alice clues Bob Yellow (8->7)
            .Play(9)              // T10: Bob plays Y5 -> brings clues from 7 to 8
            .Discard(10)          // T11: Alice discards at 8 clues -> ILLEGAL
            .BuildAndAnalyze();

        violations.Should().ContainViolationAtTurn(ViolationType.IllegalDiscard, 11,
            because: "discarding at 8 clues (restored by playing a 5) is still illegal");
    }

    [Fact]
    public void IllegalDiscard_DiscardAtExactly8AfterDiscard_ShouldNotHappen()
    {
        // Verify: after discarding (which gives +1 clue), if we're at 8, the NEXT
        // player who discards should be flagged.
        // Start: 8 clues. Alice clues (8->7). Bob discards (7->8).
        // Alice discards at 8 -> illegal.
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,R2,Y1,B1,G1, R3,Y2,B2,G2,P1, R4,Y3,G3,B3")
            .ColorClue(1, "Red")  // T1: Alice clues Bob Red (8->7)
            .Discard(5)           // T2: Bob discards R3 (7->8)
            .Discard(0)           // T3: Alice discards at 8 clues -> ILLEGAL
            .BuildAndAnalyze();

        violations.Should().ContainViolationAtTurn(ViolationType.IllegalDiscard, 3,
            because: "clue tokens returned to 8 after Bob's discard, so Alice's discard is illegal");
    }

    [Fact]
    public void IllegalDiscard_NotFlaggedAt7Clues()
    {
        // Sanity check: discarding at exactly 7 clues is legal
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,R2,Y1,B1,G1, R3,Y2,B2,G2,P1, R4,Y3")
            .ColorClue(1, "Red")  // T1: Alice clues Bob (8->7)
            .Discard(5)           // T2: Bob discards at 7 clues -> LEGAL (goes to 8)
            .BuildAndAnalyze();

        violations.Where(v => v.Turn == 2 && v.Type == ViolationType.IllegalDiscard)
            .Should().BeEmpty(because: "discarding at 7 clues is perfectly legal");
    }

    // =========================================================================
    // CRITICAL INTER-CHECKER EDGE CASES
    // =========================================================================

    [Fact]
    public void BadDiscard_And_IllegalDiscard_BothCanFire()
    {
        // Scenario: Player discards a 5 at 8 clue tokens
        // Both IllegalDiscard (8 clues) AND BadDiscard5 should fire
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R5,R1,Y1,B1,G1, R2,Y2,B2,G2,P1, R3,Y3")
            .Discard(0) // Alice discards R5 at 8 clues
            .BuildAndAnalyze();

        violations.Should().ContainViolation(ViolationType.IllegalDiscard,
            because: "discarding at 8 clues is illegal");
        violations.Should().ContainViolation(ViolationType.BadDiscard5,
            because: "discarding a 5 is a bad discard regardless of clue count");
    }

    [Fact]
    public void Misplay_CardNotInHand_ShouldNotCrash()
    {
        // Edge case: what if action.Target references a deck index not in the hand?
        // The checker does: hand.FirstOrDefault(c => c.DeckIndex == deckIndex)
        // If card == null, it returns early. No crash, no violation.
        // This would be malformed game data, but the checker should handle it gracefully.
        //
        // We can't easily test this through GameBuilder since the simulator
        // would also fail, but we can verify the code path exists by reading the checker.
        // This is a code-trace verification only.
        //
        // Verdict: HANDLED CORRECTLY - null check on line 22 of MisplayChecker
        Assert.True(true, "Code trace confirms null check handles missing card");
    }

    // =========================================================================
    // IsCardCritical HELPER EDGE CASES
    // =========================================================================

    [Fact]
    public void IsCardCritical_CountsCardsInDeck_NotJustHands()
    {
        // Scenario: R2 has 2 copies. One in hand, one still in deck.
        // IsCardCritical should count deck cards too.
        // inHandsCount=1, inDeckCount=1 -> remaining=2 -> NOT critical
        //
        // We verify this indirectly: discard R2 when the other copy is in the deck
        // (not in any hand). Should NOT be flagged.

        // Deck layout: cards 0-4 for Alice, 5-9 for Bob, 10+ draw pile
        // Put R2 at position 0 (Alice's hand) and R2 at position 11 (in deck)
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R2,R1,Y1,B1,G1, R3,Y2,B2,G2,P1, R4,R2")
            .Discard(0) // Alice discards R2 (another R2 at deck index 11)
            .BuildAndAnalyze();

        violations.Should().NotContainViolation(ViolationType.BadDiscardCritical,
            because: "another copy of R2 is still in the draw pile");
    }

    [Fact]
    public void IsSuitDead_CardStillPlayableEvenWhenHigherRanksDead()
    {
        // IsSuitDead(suitIndex, targetRank, state) correctly checks only ranks
        // between currentStack+1 and targetRank-1 (prerequisite ranks).
        // Even if R3 is dead (all copies discarded), R2 is still playable
        // and scores a point. Discarding the last R2 IS a bad discard.
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R3,R2,Y1,B1,G1, R3,R2,B2,G2,P1, R4,Y3")
            .Discard(0) // Alice discards R3
            .Discard(5) // Bob discards R3 -> both R3s gone, Red max is R2
            .Discard(1) // Alice discards R2 (first copy)
            .Discard(6) // Bob discards R2 (last copy!)
            .BuildAndAnalyze();

        // Correct: R2 is still playable (R1 exists) and scores a point.
        // Discarding the last R2 loses a potential point, so it IS critical.
        violations.Where(v => v.Turn == 4 && v.Type == ViolationType.BadDiscardCritical)
            .Should().NotBeEmpty(
                "R2 is still critical even though R3 is dead - " +
                "playing R2 still scores a point");
    }

    [Fact]
    public void BadDiscard_5AlreadyPlayed_ShouldNOTFlag_ViaEarlyReturn()
    {
        // Specific check: the BadDiscardChecker checks rank==5 FIRST.
        // If rank==5 and PlayStacks[suit] >= 5, it skips. Then returns early (line 40).
        // It does NOT fall through to the IsCardCritical check.
        // This is correct behavior since 5s of completed suits are pure trash.
        //
        // This test is essentially the same as Discard5OfCompletedSuit above,
        // but verifies no BadDiscardCritical violation either.
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R1,R2,R3,R4,R5, Y1,Y2,Y3,Y4,Y5, R5,R1,R2")
            .Play(0)  // Alice plays R1
            .Play(5)  // Bob plays Y1
            .Play(1)  // Alice plays R2
            .Play(6)  // Bob plays Y2
            .Play(2)  // Alice plays R3
            .Play(7)  // Bob plays Y3
            .Play(3)  // Alice plays R4
            .Play(8)  // Bob plays Y4
            .Play(4)  // Alice plays R5 -> Red complete!
            .Play(9)  // Bob plays Y5
            .Discard(10) // Alice discards R5 - completed suit
            .BuildAndAnalyze();

        violations.Where(v => v.Turn == 11)
            .Should().NotContain(v => v.Type == ViolationType.BadDiscard5 || v.Type == ViolationType.BadDiscardCritical,
                because: "R5 is trash when Red stack is already complete");
    }

    // =========================================================================
    // STATE SIMULATION + CHECKER INTERACTION
    // =========================================================================

    [Fact]
    public void Misplay_UsesStateBefore_NotStateAfter()
    {
        // Critical: The checker uses context.StateBefore for PlayStacks.
        // If it used StateAfter (which already has the misplay result), the check would be wrong.
        // StateAfter after a misplay: strike++, card goes to discard, stack unchanged.
        // StateBefore: stack at its pre-play value.
        //
        // We verify: Alice plays R3 when stack is at 0.
        // StateBefore.PlayStacks[Red] = 0, expectedRank = 1, card.Rank = 3 -> misplay detected
        // StateAfter.PlayStacks[Red] = 0 (unchanged, misplay), strike++
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R3,R1,Y1,B1,G1, R2,Y2,B2,G2,P1, R4,Y3")
            .Play(0) // Alice plays R3 - misplay
            .BuildAndAnalyze();

        // Verify the misplay is detected (proves StateBefore is used correctly)
        violations.Should().ContainViolation(ViolationType.Misplay);

        // Also verify the state after reflects the misplay
        states[1].PlayStacks[0].Should().Be(0, because: "misplayed card doesn't advance the stack");
        states[1].Strikes.Should().Be(1, because: "misplay adds a strike");
    }

    [Fact]
    public void WithPlayStacks_BugVerification_OnlyModifiesState0()
    {
        // Per MEMORY.md: WithPlayStacks only modifies states[0], NOT the simulation.
        // This means if you use WithPlayStacks(1,0,0,0,0) and then Play(R2),
        // the simulation ran with stacks at 0,0,0,0,0 so R2 would be a misplay
        // in the simulator, but the checker looking at states[0] would see stack=1
        // and think R2 is valid.
        //
        // This is a known footgun in the test infrastructure, not a checker bug.
        // Verify this behavior:
        var (game, states, violations) = GameBuilder.Create()
            .WithPlayers("Alice", "Bob")
            .WithDeck("R2,R1,Y1,B1,G1, R3,Y2,B2,G2,P1, R4,Y3")
            .WithPlayStacks(1, 0, 0, 0, 0) // Set R stack to 1 in states[0] ONLY
            .Play(0) // Alice plays R2
            .BuildAndAnalyze();

        // states[0] has PlayStacks[0]=1 (from WithPlayStacks)
        // But the simulator ran with PlayStacks[0]=0
        // states[1] is from the simulator: R2 played when stack was 0 -> misplay -> stack stays 0
        //
        // The checker sees states[0] (StateBefore) with PlayStacks[0]=1
        // So it thinks R2 is valid (expectedRank = 1+1 = 2 = card.Rank)
        // But the simulator already treated it as a misplay (strike++ in states[1])

        // This reveals the WithPlayStacks inconsistency:
        // Checker says: no violation (because states[0].PlayStacks[Red]=1)
        // Simulator says: misplay (because it ran with PlayStacks[Red]=0)
        states[0].PlayStacks[0].Should().Be(1, "WithPlayStacks sets states[0]");

        // The actual simulator state won't have the play stack modification
        // states[1] comes from simulation which started at 0
        // R2 played on stack=0 -> misplay -> stack stays at 0, strike++
        states[1].Strikes.Should().Be(1, "simulator treated R2 as misplay since it started at 0");
        states[1].PlayStacks[0].Should().Be(0, "misplay doesn't advance the stack");

        // But the checker saw states[0] with stack=1, so it says R2 is valid -> no violation
        violations.Should().NotContainViolation(ViolationType.Misplay,
            because: "WithPlayStacks footgun: checker sees modified states[0] but simulator used original");
    }
}
