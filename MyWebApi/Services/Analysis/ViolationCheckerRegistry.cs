using MyWebApi.Models;

namespace MyWebApi.Services.Analysis;

/// <summary>
/// Collects and provides violation checkers up to the configured convention level.
/// </summary>
public class ViolationCheckerRegistry
{
    private readonly List<IViolationChecker> _checkers = new();

    public void Register(IViolationChecker checker)
    {
        _checkers.Add(checker);
    }

    public IReadOnlyList<IViolationChecker> GetCheckersForLevel(ConventionLevel level)
    {
        return _checkers.Where(c => c.Level <= level).ToList();
    }

    public IReadOnlyList<IViolationChecker> GetCheckersForAction(ConventionLevel level, ActionType actionType)
    {
        return _checkers
            .Where(c => c.Level <= level && c.ApplicableActionTypes.Contains(actionType))
            .ToList();
    }
}
