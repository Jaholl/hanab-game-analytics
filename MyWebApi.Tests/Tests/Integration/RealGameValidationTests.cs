using FluentAssertions;
using MyWebApi.Models;
using MyWebApi.Services;
using Xunit;
using Xunit.Abstractions;

namespace MyWebApi.Tests.Tests.Integration;

/// <summary>
/// Validates analysis logic against real game #1746851 from hanab.live.
/// Players: nberlin(0), arcidox(1), oaskar24(2), jaholl(3) — 4 players, hand size 4.
/// </summary>
public class RealGameValidationTests
{
    private readonly ITestOutputHelper _output;

    public RealGameValidationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static GameExport CreateGame1746851()
    {
        return new GameExport
        {
            Id = 1746851,
            Players = new List<string> { "nberlin", "arcidox", "oaskar24", "jaholl" },
            Deck = new List<DeckCard>
            {
                new() { SuitIndex = 4, Rank = 5 }, // 0
                new() { SuitIndex = 4, Rank = 1 }, // 1
                new() { SuitIndex = 4, Rank = 1 }, // 2
                new() { SuitIndex = 2, Rank = 1 }, // 3
                new() { SuitIndex = 4, Rank = 2 }, // 4
                new() { SuitIndex = 2, Rank = 5 }, // 5
                new() { SuitIndex = 0, Rank = 3 }, // 6
                new() { SuitIndex = 3, Rank = 1 }, // 7
                new() { SuitIndex = 4, Rank = 3 }, // 8
                new() { SuitIndex = 1, Rank = 1 }, // 9
                new() { SuitIndex = 4, Rank = 2 }, // 10
                new() { SuitIndex = 0, Rank = 1 }, // 11
                new() { SuitIndex = 2, Rank = 3 }, // 12
                new() { SuitIndex = 1, Rank = 4 }, // 13
                new() { SuitIndex = 0, Rank = 1 }, // 14
                new() { SuitIndex = 2, Rank = 2 }, // 15
                new() { SuitIndex = 2, Rank = 3 }, // 16
                new() { SuitIndex = 4, Rank = 4 }, // 17
                new() { SuitIndex = 0, Rank = 5 }, // 18
                new() { SuitIndex = 1, Rank = 3 }, // 19
                new() { SuitIndex = 2, Rank = 4 }, // 20
                new() { SuitIndex = 1, Rank = 2 }, // 21
                new() { SuitIndex = 1, Rank = 4 }, // 22
                new() { SuitIndex = 3, Rank = 5 }, // 23
                new() { SuitIndex = 0, Rank = 2 }, // 24
                new() { SuitIndex = 1, Rank = 1 }, // 25
                new() { SuitIndex = 2, Rank = 1 }, // 26
                new() { SuitIndex = 3, Rank = 4 }, // 27
                new() { SuitIndex = 2, Rank = 4 }, // 28
                new() { SuitIndex = 1, Rank = 5 }, // 29
                new() { SuitIndex = 4, Rank = 4 }, // 30
                new() { SuitIndex = 3, Rank = 4 }, // 31
                new() { SuitIndex = 3, Rank = 3 }, // 32
                new() { SuitIndex = 3, Rank = 1 }, // 33
                new() { SuitIndex = 2, Rank = 1 }, // 34
                new() { SuitIndex = 4, Rank = 3 }, // 35
                new() { SuitIndex = 2, Rank = 2 }, // 36
                new() { SuitIndex = 0, Rank = 2 }, // 37
                new() { SuitIndex = 3, Rank = 2 }, // 38
                new() { SuitIndex = 1, Rank = 1 }, // 39
                new() { SuitIndex = 3, Rank = 3 }, // 40
                new() { SuitIndex = 3, Rank = 1 }, // 41
                new() { SuitIndex = 1, Rank = 3 }, // 42
                new() { SuitIndex = 0, Rank = 4 }, // 43
                new() { SuitIndex = 1, Rank = 2 }, // 44
                new() { SuitIndex = 0, Rank = 1 }, // 45
                new() { SuitIndex = 0, Rank = 4 }, // 46
                new() { SuitIndex = 0, Rank = 3 }, // 47
                new() { SuitIndex = 3, Rank = 2 }, // 48
                new() { SuitIndex = 4, Rank = 1 }, // 49
            },
            Actions = new List<GameAction>
            {
                new() { Type = ActionType.RankClue, Target = 2, Value = 1 },     // 0: nberlin clues oaskar24 1
                new() { Type = ActionType.RankClue, Target = 3, Value = 3 },     // 1: arcidox clues jaholl 3
                new() { Type = ActionType.Play, Target = 11, Value = 0 },        // 2: oaskar24 plays R1
                new() { Type = ActionType.RankClue, Target = 0, Value = 5 },     // 3: jaholl clues nberlin 5
                new() { Type = ActionType.Play, Target = 3, Value = 0 },         // 4: nberlin plays G1
                new() { Type = ActionType.Discard, Target = 4, Value = 0 },      // 5: arcidox discards P2
                new() { Type = ActionType.Play, Target = 9, Value = 0 },         // 6: oaskar24 plays Y1
                new() { Type = ActionType.RankClue, Target = 1, Value = 5 },     // 7: jaholl clues arcidox 5
                new() { Type = ActionType.Play, Target = 17, Value = 0 },        // 8: nberlin plays P4 — MISPLAY!
                new() { Type = ActionType.ColorClue, Target = 0, Value = 2 },    // 9: arcidox clues nberlin Green
                new() { Type = ActionType.RankClue, Target = 0, Value = 1 },     // 10: oaskar24 clues nberlin 1
                new() { Type = ActionType.Play, Target = 15, Value = 0 },        // 11: jaholl plays G2
                new() { Type = ActionType.Play, Target = 1, Value = 0 },         // 12: nberlin plays P1
                new() { Type = ActionType.Play, Target = 7, Value = 0 },         // 13: arcidox plays B1
                new() { Type = ActionType.RankClue, Target = 3, Value = 2 },     // 14: oaskar24 clues jaholl 2
                new() { Type = ActionType.Play, Target = 12, Value = 0 },        // 15: jaholl plays G3
                new() { Type = ActionType.Play, Target = 20, Value = 0 },        // 16: nberlin plays G4
                new() { Type = ActionType.ColorClue, Target = 3, Value = 1 },    // 17: arcidox clues jaholl Yellow
                new() { Type = ActionType.ColorClue, Target = 1, Value = 0 },    // 18: oaskar24 clues arcidox Red
                new() { Type = ActionType.Play, Target = 24, Value = 0 },        // 19: jaholl plays R2
                new() { Type = ActionType.Discard, Target = 2, Value = 0 },      // 20: nberlin discards P1
                new() { Type = ActionType.RankClue, Target = 2, Value = 2 },     // 21: arcidox clues oaskar24 2
                new() { Type = ActionType.Play, Target = 10, Value = 0 },        // 22: oaskar24 plays P2
                new() { Type = ActionType.Play, Target = 21, Value = 0 },        // 23: jaholl plays Y2
                new() { Type = ActionType.Discard, Target = 22, Value = 0 },     // 24: nberlin discards Y4
                new() { Type = ActionType.ColorClue, Target = 2, Value = 4 },    // 25: arcidox clues oaskar24 Purple
                new() { Type = ActionType.Play, Target = 8, Value = 0 },         // 26: oaskar24 plays P3
                new() { Type = ActionType.Discard, Target = 14, Value = 0 },     // 27: jaholl discards R1
                new() { Type = ActionType.Play, Target = 30, Value = 0 },        // 28: nberlin plays P4
                new() { Type = ActionType.Play, Target = 6, Value = 0 },         // 29: arcidox plays R3
                new() { Type = ActionType.Play, Target = 19, Value = 0 },        // 30: oaskar24 plays Y3
                new() { Type = ActionType.Play, Target = 13, Value = 0 },        // 31: jaholl plays Y4
                new() { Type = ActionType.RankClue, Target = 1, Value = 5 },     // 32: nberlin clues arcidox 5
                new() { Type = ActionType.Discard, Target = 34, Value = 0 },     // 33: arcidox discards G1
                new() { Type = ActionType.ColorClue, Target = 3, Value = 1 },    // 34: oaskar24 clues jaholl Yellow
                new() { Type = ActionType.Play, Target = 29, Value = 0 },        // 35: jaholl plays Y5
                new() { Type = ActionType.Play, Target = 0, Value = 0 },         // 36: nberlin plays P5
                new() { Type = ActionType.ColorClue, Target = 3, Value = 3 },    // 37: arcidox clues jaholl Blue
                new() { Type = ActionType.RankClue, Target = 0, Value = 4 },     // 38: oaskar24 clues nberlin 4
                new() { Type = ActionType.Play, Target = 38, Value = 0 },        // 39: jaholl plays B2
                new() { Type = ActionType.Discard, Target = 25, Value = 0 },     // 40: nberlin discards Y1
                new() { Type = ActionType.Discard, Target = 37, Value = 0 },     // 41: arcidox discards R2
                new() { Type = ActionType.Discard, Target = 16, Value = 0 },     // 42: oaskar24 discards G3
                new() { Type = ActionType.Play, Target = 32, Value = 0 },        // 43: jaholl plays B3
                new() { Type = ActionType.Play, Target = 27, Value = 0 },        // 44: nberlin plays B4
                new() { Type = ActionType.Play, Target = 23, Value = 0 },        // 45: arcidox plays B5
                new() { Type = ActionType.ColorClue, Target = 1, Value = 0 },    // 46: oaskar24 clues arcidox Red
                new() { Type = ActionType.ColorClue, Target = 2, Value = 0 },    // 47: jaholl clues oaskar24 Red
                new() { Type = ActionType.ColorClue, Target = 1, Value = 1 },    // 48: nberlin clues arcidox Yellow
                new() { Type = ActionType.Play, Target = 5, Value = 0 },         // 49: arcidox plays G5
                new() { Type = ActionType.Play, Target = 43, Value = 0 },        // 50: oaskar24 plays R4
                new() { Type = ActionType.RankClue, Target = 0, Value = 1 },     // 51: jaholl clues nberlin 1
                new() { Type = ActionType.ColorClue, Target = 2, Value = 4 },    // 52: nberlin clues oaskar24 Purple — no Purple cards?
                new() { Type = ActionType.Play, Target = 18, Value = 0 },        // 53: arcidox plays R5
            },
            Options = new GameOptions { Variant = "No Variant" }
        };
    }

    [Fact]
    public void Game1746851_SimulationCompletes()
    {
        var game = CreateGame1746851();
        var simulator = new GameStateSimulator();
        var states = simulator.SimulateGame(game);

        // 4 players, 54 actions → 55 states (initial + one per action)
        states.Should().HaveCount(55);
        _output.WriteLine($"States generated: {states.Count}");

        // Verify initial hand deal (4 players × 4 cards = deck indices 0-15)
        var initial = states[0];
        initial.Hands.Should().HaveCount(4);
        foreach (var hand in initial.Hands)
            hand.Should().HaveCount(4);

        _output.WriteLine("Initial hands:");
        for (int p = 0; p < 4; p++)
        {
            var cards = string.Join(", ", initial.Hands[p].Select(c =>
                $"{SuitName(c.SuitIndex)}{c.Rank}(d{c.DeckIndex})"));
            _output.WriteLine($"  {game.Players[p]}: {cards}");
        }
    }

    [Fact]
    public void Game1746851_Level0_BasicRuleViolations()
    {
        var (violations, game) = AnalyzeAtLevel(ConventionLevel.Level0_Basic);

        _output.WriteLine($"=== Level 0 Violations ({violations.Count}) ===");
        PrintViolations(violations, game);

        // Game has known misplay: action 8, nberlin plays P4 (deck 17) when stacks are at 0/0/0/0/0
        // P4 is not playable (needs P1,P2,P3 first)
        violations.Should().Contain(v => v.Type == ViolationType.Misplay,
            "nberlin misplays Purple 4 on turn 9");
    }

    [Fact]
    public void Game1746851_Level1_BeginnerConventions()
    {
        var (violations, game) = AnalyzeAtLevel(ConventionLevel.Level1_Beginner);

        _output.WriteLine($"=== Level 1 Violations ({violations.Count}) ===");
        PrintViolations(violations, game);

        // Should still contain the Level 0 misplay
        violations.Should().Contain(v => v.Type == ViolationType.Misplay);
    }

    [Fact]
    public void Game1746851_Level2_IntermediateConventions()
    {
        var (violations, game) = AnalyzeAtLevel(ConventionLevel.Level2_Intermediate);

        _output.WriteLine($"=== Level 2 Violations ({violations.Count}) ===");
        PrintViolations(violations, game);
    }

    [Fact]
    public void Game1746851_Level3_AdvancedConventions()
    {
        var (violations, game) = AnalyzeAtLevel(ConventionLevel.Level3_Advanced);

        _output.WriteLine($"=== Level 3 Violations ({violations.Count}) ===");
        PrintViolations(violations, game);
    }

    [Fact]
    public void Game1746851_FullGameTrace()
    {
        var game = CreateGame1746851();
        var simulator = new GameStateSimulator();
        var states = simulator.SimulateGame(game);
        var numPlayers = game.Players.Count;

        _output.WriteLine("=== Full Game Trace ===");
        _output.WriteLine($"Players: {string.Join(", ", game.Players)}");
        _output.WriteLine($"Actions: {game.Actions.Count}");
        _output.WriteLine("");

        for (int i = 0; i < game.Actions.Count; i++)
        {
            var action = game.Actions[i];
            var playerIdx = i % numPlayers;
            var player = game.Players[playerIdx];
            var stateBefore = states[i];
            var stateAfter = states[i + 1];

            var desc = DescribeAction(action, game, stateBefore, playerIdx);
            _output.WriteLine($"Turn {i + 1} ({player}): {desc}");
            _output.WriteLine($"  Stacks: R={stateAfter.PlayStacks[0]} Y={stateAfter.PlayStacks[1]} G={stateAfter.PlayStacks[2]} B={stateAfter.PlayStacks[3]} P={stateAfter.PlayStacks[4]}  Clues={stateAfter.ClueTokens}  Strikes={stateAfter.Strikes}");
        }

        // Final score
        var finalState = states[^1];
        var score = finalState.PlayStacks.Sum();
        _output.WriteLine($"\nFinal Score: {score}/25");
        _output.WriteLine($"Strikes: {finalState.Strikes}");
    }

    [Fact]
    public void Game1746851_ViolationSummaryAllLevels()
    {
        _output.WriteLine("=== Violation Summary Across All Levels ===\n");

        foreach (var level in new[] {
            ConventionLevel.Level0_Basic,
            ConventionLevel.Level1_Beginner,
            ConventionLevel.Level2_Intermediate,
            ConventionLevel.Level3_Advanced })
        {
            var (violations, game) = AnalyzeAtLevel(level);
            var analyzer = new RuleAnalyzer(AnalyzerOptions.ForLevel(level));
            var summary = analyzer.CreateSummary(violations);

            _output.WriteLine($"--- {level} ({violations.Count} violations) ---");
            foreach (var kv in summary.ByType.OrderByDescending(x => x.Value))
                _output.WriteLine($"  {kv.Key}: {kv.Value}");
            foreach (var kv in summary.BySeverity.OrderByDescending(x => x.Value))
                _output.WriteLine($"  [{kv.Key}]: {kv.Value}");
            _output.WriteLine("");
        }
    }

    [Fact]
    public void Game1746851_NoExceptionsAtAnyLevel()
    {
        // Ensure the analyzer doesn't crash on this real game
        var game = CreateGame1746851();
        var simulator = new GameStateSimulator();
        var states = simulator.SimulateGame(game);

        foreach (var level in new[] {
            ConventionLevel.Level0_Basic,
            ConventionLevel.Level1_Beginner,
            ConventionLevel.Level2_Intermediate,
            ConventionLevel.Level3_Advanced })
        {
            var act = () =>
            {
                var analyzer = new RuleAnalyzer(AnalyzerOptions.ForLevel(level));
                analyzer.AnalyzeGame(game, states);
            };
            act.Should().NotThrow($"analysis at {level} should not crash");
        }
    }

    private (List<RuleViolation> violations, GameExport game) AnalyzeAtLevel(ConventionLevel level)
    {
        var game = CreateGame1746851();
        var simulator = new GameStateSimulator();
        var states = simulator.SimulateGame(game);
        var analyzer = new RuleAnalyzer(AnalyzerOptions.ForLevel(level));
        var violations = analyzer.AnalyzeGame(game, states);
        return (violations, game);
    }

    private void PrintViolations(List<RuleViolation> violations, GameExport game)
    {
        foreach (var v in violations.OrderBy(v => v.Turn))
        {
            _output.WriteLine($"  Turn {v.Turn} [{v.Severity}] {v.Player}: {v.Type} — {v.Description}");
        }
    }

    private static string SuitName(int idx) => idx switch
    {
        0 => "R", 1 => "Y", 2 => "G", 3 => "B", 4 => "P", _ => "?"
    };

    private string DescribeAction(GameAction action, GameExport game, GameState state, int playerIdx)
    {
        switch (action.Type)
        {
            case ActionType.Play:
                var playCard = game.Deck[action.Target];
                var playable = state.PlayStacks[playCard.SuitIndex] == playCard.Rank - 1;
                return $"Play {SuitName(playCard.SuitIndex)}{playCard.Rank} (d{action.Target}) {(playable ? "✓" : "✗ MISPLAY")}";
            case ActionType.Discard:
                var discCard = game.Deck[action.Target];
                return $"Discard {SuitName(discCard.SuitIndex)}{discCard.Rank} (d{action.Target})";
            case ActionType.ColorClue:
                var colorName = SuitName(action.Value);
                return $"Clue {game.Players[action.Target]} {colorName}";
            case ActionType.RankClue:
                return $"Clue {game.Players[action.Target]} {action.Value}";
            default:
                return "???";
        }
    }
}
