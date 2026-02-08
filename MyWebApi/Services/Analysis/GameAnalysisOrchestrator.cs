using MyWebApi.Models;
using MyWebApi.Services.Analysis.Checkers.Level0;
using MyWebApi.Services.Analysis.Checkers.Level1;
using MyWebApi.Services.Analysis.Checkers.Level2;
using MyWebApi.Services.Analysis.Checkers.Level3;
using MyWebApi.Services.Analysis.Trackers;

namespace MyWebApi.Services.Analysis;

/// <summary>
/// Orchestrates game analysis by running state trackers and violation checkers
/// in the correct order for each action.
/// </summary>
public class GameAnalysisOrchestrator
{
    private readonly ViolationCheckerRegistry _checkerRegistry;
    private readonly StateTrackerRegistry _trackerRegistry;

    public GameAnalysisOrchestrator()
    {
        _checkerRegistry = new ViolationCheckerRegistry();
        _trackerRegistry = new StateTrackerRegistry();
        RegisterDefaults();
    }

    private void RegisterDefaults()
    {
        // State trackers
        _trackerRegistry.Register(new ClueHistoryTracker());
        _trackerRegistry.Register(new PendingFinesseTracker());
        _trackerRegistry.Register(new EarlyGameTracker());

        // Level 0
        _checkerRegistry.Register(new MisplayChecker());
        _checkerRegistry.Register(new BadDiscardChecker());
        _checkerRegistry.Register(new IllegalDiscardChecker());

        // Level 1
        _checkerRegistry.Register(new GoodTouchChecker());
        _checkerRegistry.Register(new MCVPChecker());
        _checkerRegistry.Register(new MissedSaveChecker());
        _checkerRegistry.Register(new MissedPromptChecker());
        _checkerRegistry.Register(new FinesseSetupChecker());
        _checkerRegistry.Register(new BrokenFinesseChecker());

        // Level 2
        _checkerRegistry.Register(new DoubleDiscardAvoidanceChecker());
        _checkerRegistry.Register(new FiveStallChecker());
        _checkerRegistry.Register(new StompedFinesseChecker());
        _checkerRegistry.Register(new WrongPromptChecker());

        // Level 3
        _checkerRegistry.Register(new PlayingMultipleOnesChecker());
        _checkerRegistry.Register(new SarcasticDiscardChecker());
        _checkerRegistry.Register(new FixClueChecker());
        _checkerRegistry.Register(new MisplayCostChecker());
        _checkerRegistry.Register(new InformationLockChecker());
    }

    public List<RuleViolation> AnalyzeGame(GameExport game, List<GameState> states, AnalyzerOptions options)
    {
        var context = new AnalysisContext
        {
            Game = game,
            States = states,
            Options = options
        };

        for (int i = 0; i < game.Actions.Count; i++)
        {
            var action = game.Actions[i];
            context.ActionIndex = i;
            context.Action = action;
            context.StateBefore = states[i];
            context.StateAfter = states[i + 1];
            context.CurrentPlayerIndex = i % game.Players.Count;
            context.CurrentPlayer = game.Players[context.CurrentPlayerIndex];
            context.Turn = i + 1;

            // Run trackers first (they accumulate state but don't produce violations)
            var trackers = _trackerRegistry.GetTrackersForAction(action.Type);
            foreach (var tracker in trackers)
            {
                tracker.Track(context);
            }

            // Run checkers for the current level
            var checkers = _checkerRegistry.GetCheckersForAction(options.Level, action.Type);
            foreach (var checker in checkers)
            {
                checker.Check(context);
            }
        }

        // Filter violations by the enabled level
        return context.Violations
            .Where(v => options.EnabledViolations.Contains(v.Type))
            .ToList();
    }
}
