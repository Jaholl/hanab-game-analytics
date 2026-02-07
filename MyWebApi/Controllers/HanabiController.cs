using Microsoft.AspNetCore.Mvc;
using MyWebApi.Models;
using MyWebApi.Services;

namespace MyWebApi.Controllers;

[ApiController]
[Route("[controller]")]
public class HanabiController : ControllerBase
{
    private readonly IHanabiService _hanabiService;
    private readonly ILogger<HanabiController> _logger;
    private readonly GameStateSimulator _simulator;

    // Supported variants (standard 5-suit)
    private static readonly HashSet<string> SupportedVariants = new(StringComparer.OrdinalIgnoreCase)
    {
        "No Variant"
    };

    public HanabiController(IHanabiService hanabiService, ILogger<HanabiController> logger)
    {
        _hanabiService = hanabiService;
        _logger = logger;
        _simulator = new GameStateSimulator();
    }

    [HttpGet("history/{username}")]
    public async Task<ActionResult<HanabiHistoryResponse>> GetHistory(
        string username,
        [FromQuery] int page = 0,
        [FromQuery] int size = 100)
    {
        _logger.LogInformation("Getting Hanabi history for {Username}", username);

        if (size < 1 || size > 10000)
        {
            return BadRequest("Size must be between 1 and 10000");
        }

        try
        {
            var result = await _hanabiService.GetHistoryAsync(username, page, size);
            return Ok(result);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch Hanabi history");
            return StatusCode(502, "Failed to fetch data from hanab.live");
        }
    }

    [HttpGet("game/{gameId}/analysis")]
    public async Task<ActionResult<GameAnalysisResponse>> GetGameAnalysis(
        int gameId,
        [FromQuery] int level = 1)
    {
        _logger.LogInformation("Analyzing game {GameId} at level {Level}", gameId, level);

        // Validate and convert level parameter
        if (level < 0 || level > 3)
        {
            return BadRequest("Level must be 0, 1, 2, or 3");
        }
        var conventionLevel = (ConventionLevel)level;
        var options = AnalyzerOptions.ForLevel(conventionLevel);

        try
        {
            var game = await _hanabiService.GetGameExportAsync(gameId);
            if (game == null)
            {
                return NotFound($"Game #{gameId} could not be loaded");
            }

            var response = new GameAnalysisResponse
            {
                Game = game,
                VariantName = game.Options.Variant
            };

            // Check if variant is supported
            if (!SupportedVariants.Contains(game.Options.Variant))
            {
                response.VariantSupported = false;
                response.Summary = new AnalysisSummary();
                return Ok(response);
            }

            // Simulate game and analyze with specified level
            var states = _simulator.SimulateGame(game);
            var analyzer = new RuleAnalyzer(options);
            var violations = analyzer.AnalyzeGame(game, states);

            response.Violations = violations;
            response.Summary = analyzer.CreateSummary(violations);
            response.VariantSupported = true;
            response.States = states;

            return Ok(response);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch game {GameId}", gameId);
            return StatusCode(502, "Failed to fetch data from hanab.live");
        }
    }

    [HttpGet("history/{username}/critical-trends")]
    public async Task<ActionResult<BatchCriticalMistakesResponse>> GetCriticalTrends(
        string username,
        [FromQuery] int size = 50,
        [FromQuery] int level = 2)
    {
        _logger.LogInformation("Getting critical trends for {Username}, size={Size}, level={Level}", username, size, level);

        if (size < 1 || size > 200)
        {
            return BadRequest("Size must be between 1 and 200");
        }

        if (level < 0 || level > 3)
        {
            return BadRequest("Level must be 0, 1, 2, or 3");
        }

        var conventionLevel = (ConventionLevel)level;
        var options = AnalyzerOptions.ForLevel(conventionLevel);

        try
        {
            var history = await _hanabiService.GetHistoryAsync(username, 0, size);
            var games = history.Rows;

            var semaphore = new SemaphoreSlim(5);
            var results = new List<GameCriticalSummary>();
            var lockObj = new object();

            var tasks = games.Select(async game =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var export = await _hanabiService.GetGameExportAsync(game.Id);
                    if (export == null) return;

                    if (!SupportedVariants.Contains(export.Options.Variant)) return;

                    var simulator = new GameStateSimulator();
                    var states = simulator.SimulateGame(export);
                    var analyzer = new RuleAnalyzer(options);
                    var violations = analyzer.AnalyzeGame(export, states);

                    var criticalCount = violations.Count(v =>
                        v.Severity == Severity.Critical &&
                        string.Equals(v.Player, username, StringComparison.OrdinalIgnoreCase));

                    var summary = new GameCriticalSummary
                    {
                        GameId = game.Id,
                        DateTime = game.DateTime,
                        CriticalCount = criticalCount,
                        Score = game.Score,
                        Players = export.Players
                    };

                    lock (lockObj)
                    {
                        results.Add(summary);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to analyze game {GameId} for critical trends", game.Id);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            // Sort chronologically (oldest first) by DateTime
            results.Sort((a, b) => string.Compare(a.DateTime, b.DateTime, StringComparison.Ordinal));

            return Ok(new BatchCriticalMistakesResponse
            {
                Player = username,
                Games = results
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch history for critical trends");
            return StatusCode(502, "Failed to fetch data from hanab.live");
        }
    }

    // Percentile lookup tables from 47-player population study (No Variant, 50 games each)
    // Each array: [p0, p10, p20, p30, p40, p50, p60, p70, p80, p90, p100]
    private static readonly double[] PlayRatePercentiles =
        { 0.3748, 0.3887, 0.4092, 0.4191, 0.4235, 0.4364, 0.4391, 0.4444, 0.4639, 0.4784, 0.4865 };
    private static readonly double[] DiscardRatePercentiles =
        { 0.1489, 0.1658, 0.1787, 0.1869, 0.2020, 0.2083, 0.2169, 0.2196, 0.2301, 0.2367, 0.2589 };
    private static readonly double[] ClueRatePercentiles =
        { 0.3063, 0.3158, 0.3328, 0.3470, 0.3542, 0.3608, 0.3662, 0.3743, 0.3887, 0.4018, 0.4385 };
    private static readonly double[] ErrorRatePercentiles =
        { 0.0000, 0.0141, 0.0188, 0.0220, 0.0241, 0.0284, 0.0294, 0.0305, 0.0338, 0.0458, 0.0689 };
    private static readonly double[] BadClueRatePercentiles =
        { 0.0670, 0.0982, 0.1295, 0.1459, 0.1600, 0.1724, 0.2068, 0.2524, 0.2917, 0.3372, 0.3612 };
    private static readonly double[] MissedSavesPerGamePercentiles =
        { 0.6100, 0.9000, 1.1600, 1.2900, 1.3800, 1.5700, 1.7100, 2.1800, 3.0600, 3.8800, 4.6800 };
    private static readonly double[] MissedTechPerGamePercentiles =
        { 0.1200, 0.2000, 0.2600, 0.2900, 0.3300, 0.4300, 0.5700, 0.6400, 0.7700, 1.0000, 1.2600 };
    private static readonly double[] MisplayRatePercentiles =
        { 0.0000, 0.0293, 0.0343, 0.0402, 0.0469, 0.0524, 0.0581, 0.0615, 0.0667, 0.0849, 0.1406 };

    /// <summary>
    /// Given a value and a sorted percentile table [p0..p100 in 10% steps],
    /// returns a 0-100 percentile score via linear interpolation.
    /// </summary>
    private static double ToPercentile(double value, double[] table)
    {
        if (value <= table[0]) return 0;
        if (value >= table[^1]) return 100;

        for (int i = 1; i < table.Length; i++)
        {
            if (value <= table[i])
            {
                double lo = table[i - 1];
                double hi = table[i];
                double fraction = (hi > lo) ? (value - lo) / (hi - lo) : 0;
                return ((i - 1) * 10) + (fraction * 10);
            }
        }
        return 100;
    }

    [HttpGet("history/{username}/playstyle")]
    public async Task<ActionResult<PlaystyleResponse>> GetPlaystyleProfile(
        string username,
        [FromQuery] int size = 50,
        [FromQuery] int level = 2)
    {
        _logger.LogInformation("Getting playstyle profile for {Username}, size={Size}, level={Level}", username, size, level);

        if (size < 1 || size > 200)
            return BadRequest("Size must be between 1 and 200");
        if (level < 0 || level > 3)
            return BadRequest("Level must be 0, 1, 2, or 3");

        var conventionLevel = (ConventionLevel)level;
        var options = AnalyzerOptions.ForLevel(conventionLevel);

        try
        {
            var history = await _hanabiService.GetHistoryAsync(username, 0, size);
            var games = history.Rows;

            var semaphore = new SemaphoreSlim(5);
            var lockObj = new object();

            int gamesAnalyzed = 0;
            int totalActions = 0, plays = 0, discards = 0, colorClues = 0, rankClues = 0;
            int misplays = 0, badDiscards = 0, goodTouchViolations = 0, mcvpViolations = 0;
            int missedSaves = 0, missedPrompts = 0, missedFinesses = 0;

            var tasks = games.Select(async game =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var export = await _hanabiService.GetGameExportAsync(game.Id);
                    if (export == null) return;
                    if (!SupportedVariants.Contains(export.Options.Variant)) return;

                    var playerIndex = export.Players.FindIndex(p =>
                        string.Equals(p, username, StringComparison.OrdinalIgnoreCase));
                    if (playerIndex < 0) return;

                    var numPlayers = export.Players.Count;

                    int gPlays = 0, gDiscards = 0, gColorClues = 0, gRankClues = 0;
                    for (int i = 0; i < export.Actions.Count; i++)
                    {
                        if (i % numPlayers != playerIndex) continue;
                        switch (export.Actions[i].Type)
                        {
                            case ActionType.Play: gPlays++; break;
                            case ActionType.Discard: gDiscards++; break;
                            case ActionType.ColorClue: gColorClues++; break;
                            case ActionType.RankClue: gRankClues++; break;
                        }
                    }

                    var simulator = new GameStateSimulator();
                    var states = simulator.SimulateGame(export);
                    var analyzer = new RuleAnalyzer(options);
                    var violations = analyzer.AnalyzeGame(export, states);

                    var playerViolations = violations.Where(v =>
                        string.Equals(v.Player, username, StringComparison.OrdinalIgnoreCase)).ToList();

                    int gMisplays = playerViolations.Count(v => v.Type == ViolationType.Misplay);
                    int gBadDiscards = playerViolations.Count(v =>
                        v.Type == ViolationType.BadDiscard5 || v.Type == ViolationType.BadDiscardCritical);
                    int gGoodTouch = playerViolations.Count(v => v.Type == ViolationType.GoodTouchViolation);
                    int gMCVP = playerViolations.Count(v => v.Type == ViolationType.MCVPViolation);
                    int gMissedSaves = playerViolations.Count(v => v.Type == ViolationType.MissedSave);
                    int gMissedPrompts = playerViolations.Count(v => v.Type == ViolationType.MissedPrompt);
                    int gMissedFinesses = playerViolations.Count(v =>
                        v.Type == ViolationType.MissedFinesse || v.Type == ViolationType.BrokenFinesse);

                    lock (lockObj)
                    {
                        gamesAnalyzed++;
                        totalActions += gPlays + gDiscards + gColorClues + gRankClues;
                        plays += gPlays;
                        discards += gDiscards;
                        colorClues += gColorClues;
                        rankClues += gRankClues;
                        misplays += gMisplays;
                        badDiscards += gBadDiscards;
                        goodTouchViolations += gGoodTouch;
                        mcvpViolations += gMCVP;
                        missedSaves += gMissedSaves;
                        missedPrompts += gMissedPrompts;
                        missedFinesses += gMissedFinesses;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to analyze game {GameId} for playstyle", game.Id);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            // Compute raw rates
            var rates = new PlaystyleRates();
            var dimensions = new PlaystyleDimensions();

            if (gamesAnalyzed > 0 && totalActions > 0)
            {
                int totalClues = colorClues + rankClues;

                rates.PlayRate = Math.Round((double)plays / totalActions, 4);
                rates.DiscardRate = Math.Round((double)discards / totalActions, 4);
                rates.ClueRate = Math.Round((double)totalClues / totalActions, 4);
                rates.ErrorRate = Math.Round((double)(misplays + badDiscards) / totalActions, 4);
                rates.BadClueRate = totalClues > 0 ? Math.Round((double)(goodTouchViolations + mcvpViolations) / totalClues, 4) : 0;
                rates.MissedSavesPerGame = Math.Round((double)missedSaves / gamesAnalyzed, 2);
                rates.MissedTechPerGame = Math.Round((double)(missedPrompts + missedFinesses) / gamesAnalyzed, 2);
                rates.MisplayRate = plays > 0 ? Math.Round((double)misplays / plays, 4) : 0;

                // All dimensions use population percentiles (0-100)
                // Inverted: high raw rate = bad, so 100 - percentile
                dimensions.Accuracy = Math.Round(100 - ToPercentile(rates.ErrorRate, ErrorRatePercentiles), 1);
                dimensions.ClueQuality = Math.Round(100 - ToPercentile(rates.BadClueRate, BadClueRatePercentiles), 1);
                dimensions.Teamwork = Math.Round(100 - ToPercentile(rates.MissedSavesPerGame, MissedSavesPerGamePercentiles), 1);
                dimensions.Technique = Math.Round(100 - ToPercentile(rates.MissedTechPerGame, MissedTechPerGamePercentiles), 1);
                dimensions.MisreadSaves = Math.Round(100 - ToPercentile(rates.MisplayRate, MisplayRatePercentiles), 1);

                // Neutral behavioral: direct percentile
                dimensions.Boldness = Math.Round(ToPercentile(rates.PlayRate, PlayRatePercentiles), 1);
                dimensions.Efficiency = Math.Round(ToPercentile(rates.ClueRate, ClueRatePercentiles), 1);
                dimensions.DiscardFrequency = Math.Round(ToPercentile(rates.DiscardRate, DiscardRatePercentiles), 1);
            }

            return Ok(new PlaystyleResponse
            {
                Player = username,
                GamesAnalyzed = gamesAnalyzed,
                TotalActions = totalActions,
                Plays = plays,
                Discards = discards,
                ColorClues = colorClues,
                RankClues = rankClues,
                Misplays = misplays,
                BadDiscards = badDiscards,
                GoodTouchViolations = goodTouchViolations,
                MCVPViolations = mcvpViolations,
                MissedSaves = missedSaves,
                MissedPrompts = missedPrompts,
                MissedFinesses = missedFinesses,
                Rates = rates,
                Dimensions = dimensions
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch history for playstyle profile");
            return StatusCode(502, "Failed to fetch data from hanab.live");
        }
    }
}
