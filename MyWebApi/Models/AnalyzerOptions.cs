namespace MyWebApi.Models;

/// <summary>
/// Configuration options for the RuleAnalyzer.
/// Controls which convention level to use for violation detection.
/// </summary>
public class AnalyzerOptions
{
    /// <summary>
    /// The H-Group convention level to use for analysis.
    /// Default is Level1_Beginner.
    /// </summary>
    public ConventionLevel Level { get; set; } = ConventionLevel.Level1_Beginner;

    /// <summary>
    /// Violations introduced at each level. Higher levels inherit all lower-level violations.
    /// </summary>
    private static readonly Dictionary<ConventionLevel, HashSet<string>> ViolationsByLevel = new()
    {
        [ConventionLevel.Level0_Basic] = new HashSet<string>
        {
            ViolationType.Misplay,
            ViolationType.BadDiscard5,
            ViolationType.BadDiscardCritical,
            ViolationType.IllegalDiscard
        },
        [ConventionLevel.Level1_Beginner] = new HashSet<string>
        {
            ViolationType.GoodTouchViolation,
            ViolationType.MCVPViolation,
            ViolationType.MissedSave,
            ViolationType.MissedPrompt,
            ViolationType.MissedFinesse,
            ViolationType.BrokenFinesse
        },
        [ConventionLevel.Level2_Intermediate] = new HashSet<string>
        {
            ViolationType.FiveStall,
            ViolationType.StompedFinesse,
            ViolationType.WrongPrompt,
            ViolationType.DoubleDiscardAvoidance,
            ViolationType.BadPlayClue
        },
        [ConventionLevel.Level3_Advanced] = new HashSet<string>
        {
            ViolationType.FixClue,
            ViolationType.SarcasticDiscard,
            ViolationType.WrongOnesOrder,
            ViolationType.MisplayCostViolation,
            ViolationType.InformationLock
        }
    };

    /// <summary>
    /// Returns the set of violation types that are enabled for the current level.
    /// Higher levels include all violations from lower levels.
    /// </summary>
    public HashSet<string> EnabledViolations
    {
        get
        {
            var enabled = new HashSet<string>();
            for (var lvl = ConventionLevel.Level0_Basic; lvl <= Level; lvl++)
            {
                if (ViolationsByLevel.TryGetValue(lvl, out var violations))
                    enabled.UnionWith(violations);
            }
            return enabled;
        }
    }

    /// <summary>
    /// Creates options for a specific convention level.
    /// </summary>
    public static AnalyzerOptions ForLevel(ConventionLevel level) => new() { Level = level };

    /// <summary>
    /// Creates options for Level 0 (basic rules only).
    /// </summary>
    public static AnalyzerOptions Basic => ForLevel(ConventionLevel.Level0_Basic);

    /// <summary>
    /// Creates options for Level 1 (beginner conventions).
    /// </summary>
    public static AnalyzerOptions Beginner => ForLevel(ConventionLevel.Level1_Beginner);

    /// <summary>
    /// Creates options for Level 2 (intermediate conventions).
    /// </summary>
    public static AnalyzerOptions Intermediate => ForLevel(ConventionLevel.Level2_Intermediate);

    /// <summary>
    /// Creates options for Level 3 (advanced conventions).
    /// </summary>
    public static AnalyzerOptions Advanced => ForLevel(ConventionLevel.Level3_Advanced);
}
