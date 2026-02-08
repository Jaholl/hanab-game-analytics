using MyWebApi.Models;
using MyWebApi.Services.Analysis.Helpers;

namespace MyWebApi.Services.Analysis.Checkers.Level0;

/// <summary>
/// Detects misplays (playing cards that don't fit the current stack).
/// At Level 2+, includes blame attribution to clue-givers.
/// </summary>
public class MisplayChecker : IViolationChecker
{
    public ConventionLevel Level => ConventionLevel.Level0_Basic;

    private static readonly HashSet<ActionType> _applicableTypes = new() { ActionType.Play };
    public IReadOnlySet<ActionType> ApplicableActionTypes => _applicableTypes;

    public void Check(AnalysisContext context)
    {
        var hand = context.StateBefore.Hands[context.CurrentPlayerIndex];
        var deckIndex = context.Action.Target;
        var card = hand.FirstOrDefault(c => c.DeckIndex == deckIndex);
        if (card == null) return;

        var expectedRank = context.StateBefore.PlayStacks[card.SuitIndex] + 1;
        if (card.Rank == expectedRank) return; // Valid play

        var suitName = AnalysisHelpers.GetSuitName(card.SuitIndex);
        var stackValue = context.StateBefore.PlayStacks[card.SuitIndex];

        // Look up the most recent clue that touched this card (shared by L1+ MisreadSave and L2+ blame)
        ClueHistoryEntry? relevantClue = null;
        if (card.HasAnyClue)
        {
            relevantClue = context.ClueHistory
                .Where(c => c.TargetPlayerIndex == context.CurrentPlayerIndex &&
                            c.TouchedDeckIndices.Contains(deckIndex))
                .OrderByDescending(c => c.Turn)
                .FirstOrDefault();
        }

        // At Level 1+, detect misread saves: clued card was on chop when clued, player misplays it
        if (context.Options.Level >= ConventionLevel.Level1_Beginner && relevantClue != null)
        {
            var handAtClueTime = context.States[relevantClue.Turn - 1].Hands[context.CurrentPlayerIndex];
            var chopAtClueTime = AnalysisHelpers.GetChopCard(handAtClueTime);
            if (chopAtClueTime != null && chopAtClueTime.DeckIndex == deckIndex)
            {
                context.Violations.Add(new RuleViolation
                {
                    Turn = context.Turn,
                    Player = context.CurrentPlayer,
                    Type = ViolationType.MisreadSave,
                    Severity = Severity.Warning,
                    Description = $"Misread save clue as play clue: played {suitName} {card.Rank} but it was on chop when clued (needed {expectedRank})"
                });
            }
        }

        // At Level 2+, check blame attribution
        if (context.Options.Level >= ConventionLevel.Level2_Intermediate && relevantClue != null)
        {
            var stateAtClue = context.States[relevantClue.Turn - 1];
            bool validFinesseExists = AnalysisHelpers.CheckForValidFinesse(
                relevantClue, stateAtClue, context.Game, card);

            if (!validFinesseExists)
            {
                var clueGiver = context.Game.Players[relevantClue.ClueGiverIndex];
                var clueType = relevantClue.ClueType == ActionType.ColorClue
                    ? AnalysisHelpers.GetSuitName(relevantClue.ClueValue)
                    : relevantClue.ClueValue.ToString();

                bool wasFocus = relevantClue.FocusDeckIndex == deckIndex;
                string focusNote = wasFocus ? "" : " (player may have misread focus)";

                context.Violations.Add(new RuleViolation
                {
                    Turn = relevantClue.Turn,
                    Player = clueGiver,
                    Type = ViolationType.BadPlayClue,
                    Severity = Severity.Critical,
                    Description = $"Clue ({clueType}) to {context.CurrentPlayer} caused misplay of {suitName} {card.Rank} (needed {expectedRank}){focusNote}"
                });

                context.Violations.Add(new RuleViolation
                {
                    Turn = context.Turn,
                    Player = context.CurrentPlayer,
                    Type = ViolationType.Misplay,
                    Severity = Severity.Info,
                    Description = $"Played {suitName} {card.Rank} but needed {expectedRank} - misled by clue from {clueGiver}",
                    Card = new CardIdentifier
                    {
                        DeckIndex = deckIndex,
                        SuitIndex = card.SuitIndex,
                        Rank = card.Rank
                    }
                });
                return;
            }
        }

        // Standard misplay
        context.Violations.Add(new RuleViolation
        {
            Turn = context.Turn,
            Player = context.CurrentPlayer,
            Type = ViolationType.Misplay,
            Severity = Severity.Critical,
            Description = $"Played {suitName} {card.Rank} but {suitName} {stackValue} was on the stack (needed {expectedRank})",
            Card = new CardIdentifier
            {
                DeckIndex = deckIndex,
                SuitIndex = card.SuitIndex,
                Rank = card.Rank
            }
        });
    }
}
