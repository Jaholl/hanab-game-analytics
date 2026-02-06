using MyWebApi.Models;

namespace MyWebApi.Services.Analysis;

/// <summary>
/// Interface for state trackers that accumulate information across actions
/// without producing violations themselves.
/// </summary>
public interface IStateTracker
{
    /// <summary>
    /// The action types this tracker applies to.
    /// </summary>
    IReadOnlySet<ActionType> ApplicableActionTypes { get; }

    /// <summary>
    /// Track state for the current action.
    /// </summary>
    void Track(AnalysisContext context);
}
