using FluentAssertions;
using FluentAssertions.Collections;
using FluentAssertions.Execution;
using MyWebApi.Models;

namespace MyWebApi.Tests.Helpers;

/// <summary>
/// FluentAssertions extensions for testing RuleViolations.
/// </summary>
public static class ViolationAssertions
{
    /// <summary>
    /// Asserts that a violation of the specified type exists.
    /// </summary>
    public static AndConstraint<GenericCollectionAssertions<RuleViolation>> ContainViolation(
        this GenericCollectionAssertions<RuleViolation> assertions,
        string violationType,
        string because = "",
        params object[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .Given(() => assertions.Subject)
            .ForCondition(violations => violations.Any(v => v.Type == violationType))
            .FailWith("Expected violations to contain a {0} violation{reason}, but found: [{1}]",
                violationType,
                string.Join(", ", assertions.Subject.Select(v => v.Type)));

        return new AndConstraint<GenericCollectionAssertions<RuleViolation>>(assertions);
    }

    /// <summary>
    /// Asserts that no violation of the specified type exists.
    /// </summary>
    public static AndConstraint<GenericCollectionAssertions<RuleViolation>> NotContainViolation(
        this GenericCollectionAssertions<RuleViolation> assertions,
        string violationType,
        string because = "",
        params object[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .Given(() => assertions.Subject)
            .ForCondition(violations => !violations.Any(v => v.Type == violationType))
            .FailWith("Expected violations to not contain a {0} violation{reason}, but it was found",
                violationType);

        return new AndConstraint<GenericCollectionAssertions<RuleViolation>>(assertions);
    }

    /// <summary>
    /// Asserts that a violation exists with specific player attribution.
    /// </summary>
    public static AndConstraint<GenericCollectionAssertions<RuleViolation>> ContainViolationForPlayer(
        this GenericCollectionAssertions<RuleViolation> assertions,
        string violationType,
        string player,
        string because = "",
        params object[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .Given(() => assertions.Subject)
            .ForCondition(violations => violations.Any(v => v.Type == violationType && v.Player == player))
            .FailWith("Expected violations to contain a {0} violation for player {1}{reason}, but found: [{2}]",
                violationType,
                player,
                string.Join(", ", assertions.Subject.Select(v => $"{v.Type} ({v.Player})")));

        return new AndConstraint<GenericCollectionAssertions<RuleViolation>>(assertions);
    }

    /// <summary>
    /// Asserts that a violation exists at a specific turn.
    /// </summary>
    public static AndConstraint<GenericCollectionAssertions<RuleViolation>> ContainViolationAtTurn(
        this GenericCollectionAssertions<RuleViolation> assertions,
        string violationType,
        int turn,
        string because = "",
        params object[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .Given(() => assertions.Subject)
            .ForCondition(violations => violations.Any(v => v.Type == violationType && v.Turn == turn))
            .FailWith("Expected violations to contain a {0} violation at turn {1}{reason}, but found: [{2}]",
                violationType,
                turn,
                string.Join(", ", assertions.Subject.Select(v => $"{v.Type} (turn {v.Turn})")));

        return new AndConstraint<GenericCollectionAssertions<RuleViolation>>(assertions);
    }

    /// <summary>
    /// Asserts that a violation exists with specific severity.
    /// </summary>
    public static AndConstraint<GenericCollectionAssertions<RuleViolation>> ContainViolationWithSeverity(
        this GenericCollectionAssertions<RuleViolation> assertions,
        string violationType,
        string severity,
        string because = "",
        params object[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .Given(() => assertions.Subject)
            .ForCondition(violations => violations.Any(v => v.Type == violationType && v.Severity == severity))
            .FailWith("Expected violations to contain a {0} violation with severity {1}{reason}, but found: [{2}]",
                violationType,
                severity,
                string.Join(", ", assertions.Subject.Where(v => v.Type == violationType).Select(v => $"{v.Type} ({v.Severity})")));

        return new AndConstraint<GenericCollectionAssertions<RuleViolation>>(assertions);
    }

    /// <summary>
    /// Asserts that NO violation of the specified type exists at a specific turn.
    /// </summary>
    public static AndConstraint<GenericCollectionAssertions<RuleViolation>> NotContainViolationAtTurn(
        this GenericCollectionAssertions<RuleViolation> assertions,
        string violationType,
        int turn,
        string because = "",
        params object[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .Given(() => assertions.Subject)
            .ForCondition(violations => !violations.Any(v => v.Type == violationType && v.Turn == turn))
            .FailWith("Expected no {0} violation at turn {1}{reason}, but found one",
                violationType,
                turn);

        return new AndConstraint<GenericCollectionAssertions<RuleViolation>>(assertions);
    }

    /// <summary>
    /// Asserts that NO violation of the specified type exists for a specific player.
    /// </summary>
    public static AndConstraint<GenericCollectionAssertions<RuleViolation>> NotContainViolationForPlayer(
        this GenericCollectionAssertions<RuleViolation> assertions,
        string violationType,
        string player,
        string because = "",
        params object[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .Given(() => assertions.Subject)
            .ForCondition(violations => !violations.Any(v => v.Type == violationType && v.Player == player))
            .FailWith("Expected no {0} violation for player {1}{reason}, but found one",
                violationType,
                player);

        return new AndConstraint<GenericCollectionAssertions<RuleViolation>>(assertions);
    }

    /// <summary>
    /// Asserts that violations contain a specific count of a violation type.
    /// </summary>
    public static AndConstraint<GenericCollectionAssertions<RuleViolation>> ContainViolationCount(
        this GenericCollectionAssertions<RuleViolation> assertions,
        string violationType,
        int expectedCount,
        string because = "",
        params object[] becauseArgs)
    {
        var actualCount = assertions.Subject.Count(v => v.Type == violationType);

        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .ForCondition(actualCount == expectedCount)
            .FailWith("Expected exactly {0} {1} violation(s){reason}, but found {2}",
                expectedCount,
                violationType,
                actualCount);

        return new AndConstraint<GenericCollectionAssertions<RuleViolation>>(assertions);
    }

    /// <summary>
    /// Asserts that no violations exist at all.
    /// </summary>
    public static AndConstraint<GenericCollectionAssertions<RuleViolation>> BeCleanGame(
        this GenericCollectionAssertions<RuleViolation> assertions,
        string because = "",
        params object[] becauseArgs)
    {
        Execute.Assertion
            .BecauseOf(because, becauseArgs)
            .Given(() => assertions.Subject)
            .ForCondition(violations => !violations.Any())
            .FailWith("Expected no violations{reason}, but found: [{0}]",
                string.Join(", ", assertions.Subject.Select(v => $"{v.Type} by {v.Player} on turn {v.Turn}")));

        return new AndConstraint<GenericCollectionAssertions<RuleViolation>>(assertions);
    }

    /// <summary>
    /// Gets all violations of a specific type for further assertions.
    /// </summary>
    public static IEnumerable<RuleViolation> OfType(
        this IEnumerable<RuleViolation> violations,
        string violationType)
    {
        return violations.Where(v => v.Type == violationType);
    }

    /// <summary>
    /// Gets the first violation of a specific type.
    /// </summary>
    public static RuleViolation? FirstOfType(
        this IEnumerable<RuleViolation> violations,
        string violationType)
    {
        return violations.FirstOrDefault(v => v.Type == violationType);
    }
}
