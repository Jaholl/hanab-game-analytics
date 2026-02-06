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

        // At Level 2+, check blame attribution
        if (context.Options.Level >= ConventionLevel.Level2_Intermediate && card.HasAnyClue)
        {
            var relevantClue = context.ClueHistory
                .Where(c => c.TargetPlayerIndex == context.CurrentPlayerIndex &&
                            c.TouchedDeckIndices.Contains(deckIndex))
                .OrderByDescending(c => c.Turn)
                .FirstOrDefault();

            if (relevantClue != null)
            {
                bool validFinesseExists = AnalysisHelpers.CheckForValidFinesse(
                    relevantClue, context.StateBefore, context.Game, card);

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
