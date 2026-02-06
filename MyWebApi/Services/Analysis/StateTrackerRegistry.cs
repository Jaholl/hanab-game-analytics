using MyWebApi.Models;

namespace MyWebApi.Services.Analysis;

/// <summary>
/// Collects and provides state trackers.
/// </summary>
public class StateTrackerRegistry
{
    private readonly List<IStateTracker> _trackers = new();

    public void Register(IStateTracker tracker)
    {
        _trackers.Add(tracker);
    }

    public IReadOnlyList<IStateTracker> GetTrackersForAction(ActionType actionType)
    {
        return _trackers
            .Where(t => t.ApplicableActionTypes.Contains(actionType))
            .ToList();
    }
}
