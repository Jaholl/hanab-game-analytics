using MyWebApi.Models;

namespace MyWebApi.Services.Analysis;

/// <summary>
/// Interface for convention violation checkers.
/// Each checker is responsible for detecting a specific type of violation.
/// </summary>
public interface IViolationChecker
{
    /// <summary>
    /// The convention level at which this checker becomes active.
    /// </summary>
    ConventionLevel Level { get; }

    /// <summary>
    /// The action types this checker applies to.
    /// </summary>
    IReadOnlySet<ActionType> ApplicableActionTypes { get; }

    /// <summary>
    /// Check for violations in the current action context.
    /// </summary>
    void Check(AnalysisContext context);
}
