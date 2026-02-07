using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using MyWebApi.Models;
using MyWebApi.Services;

namespace MyWebApi.Controllers;

[ApiController]
[Route("[controller]")]
public class HanabiController : ControllerBase
{
    private readonly IHanabiService _hanabiService;
    private readonly ILogger<HanabiController> _logger;
    private readonly IMemoryCache _cache;
    private readonly GameStateSimulator _simulator;

    // Supported variants (standard 5-suit)
    private static readonly HashSet<string> SupportedVariants = new(StringComparer.OrdinalIgnoreCase)
    {
        "No Variant"
    };

    public HanabiController(IHanabiService hanabiService, ILogger<HanabiController> logger, IMemoryCache cache)
    {
        _hanabiService = hanabiService;
        _logger = logger;
        _cache = cache;
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

    // Percentile lookup tables from 119-player population study (No Variant, 50 games each)
    // Each array: [p0, p10, p20, p30, p40, p50, p60, p70, p80, p90, p100]
    private static readonly double[] PlayRatePercentiles =
        { 0.3100, 0.3570, 0.3796, 0.3967, 0.4066, 0.4212, 0.4311, 0.4414, 0.4600, 0.4715, 0.5576 };
    private static readonly double[] DiscardRatePercentiles =
        { 0.1498, 0.1739, 0.1848, 0.1924, 0.2083, 0.2163, 0.2222, 0.2340, 0.2418, 0.2496, 0.2853 };
    private static readonly double[] ClueRatePercentiles =
        { 0.2302, 0.3333, 0.3451, 0.3516, 0.3586, 0.3712, 0.3767, 0.3839, 0.3952, 0.4048, 0.4468 };
    private static readonly double[] ErrorRatePercentiles =
        { 0.0032, 0.0130, 0.0156, 0.0171, 0.0197, 0.0227, 0.0270, 0.0305, 0.0373, 0.0485, 0.1691 };
    private static readonly double[] MissedSavesPerGamePercentiles =
        { 0.5400, 0.8400, 0.9400, 1.0600, 1.2200, 1.3400, 1.5000, 1.7400, 1.9800, 2.2800, 5.0700 };
    private static readonly double[] MissedTechPerGamePercentiles =
        { 0.0400, 0.2200, 0.2400, 0.3000, 0.3600, 0.4000, 0.5000, 0.6000, 0.6800, 0.9200, 1.6600 };
    private static readonly double[] MisreadSavesPerGamePercentiles =
        { 0.0000, 0.0000, 0.0200, 0.0400, 0.0500, 0.0600, 0.0800, 0.0800, 0.1000, 0.1400, 0.4800 };
    private static readonly double[] GoodTouchPerCluePercentiles =
        { 0.0159, 0.0602, 0.0758, 0.0889, 0.0970, 0.1124, 0.1224, 0.1365, 0.1549, 0.1951, 0.2865 };

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
            // Fetch history first (cheap) to build a cache key that includes the latest game ID
            var history = await _hanabiService.GetHistoryAsync(username, 0, size);
            var games = history.Rows;

            var latestGameId = games.Count > 0 ? games[0].Id : 0;
            var cacheKey = $"playstyle:{username.ToLowerInvariant()}:{size}:{level}:{latestGameId}";

            if (_cache.TryGetValue(cacheKey, out PlaystyleResponse? cachedResult) && cachedResult != null)
            {
                _logger.LogInformation("Serving cached playstyle for {Username} (key={CacheKey})", username, cacheKey);
                return Ok(cachedResult);
            }

            _logger.LogInformation("Computing playstyle for {Username} (key={CacheKey})", username, cacheKey);

            var semaphore = new SemaphoreSlim(5);
            var lockObj = new object();

            int gamesAnalyzed = 0;
            int totalActions = 0, plays = 0, discards = 0, colorClues = 0, rankClues = 0;
            int misplays = 0, badDiscards = 0, goodTouchViolations = 0, mcvpViolations = 0;
            int missedSaves = 0, missedPrompts = 0, missedFinesses = 0, misreadSaves = 0;

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
                    int gMisreadSaves = playerViolations.Count(v => v.Type == ViolationType.MisreadSave);

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
                        misreadSaves += gMisreadSaves;
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
                rates.MissedSavesPerGame = Math.Round((double)missedSaves / gamesAnalyzed, 2);
                rates.MissedTechPerGame = Math.Round((double)(missedPrompts + missedFinesses) / gamesAnalyzed, 2);
                rates.MisreadSavesPerGame = Math.Round((double)misreadSaves / gamesAnalyzed, 2);
                rates.GoodTouchPerClue = totalClues > 0 ? Math.Round((double)goodTouchViolations / totalClues, 4) : 0;
                rates.ColorClueRate = totalClues > 0 ? Math.Round((double)colorClues / totalClues, 4) : 0.5;

                // All dimensions use population percentiles (0-100)
                // Inverted: high raw rate = bad, so 100 - percentile
                dimensions.Accuracy = Math.Round(100 - ToPercentile(rates.ErrorRate, ErrorRatePercentiles), 1);
                dimensions.Teamwork = Math.Round(100 - ToPercentile(rates.MissedSavesPerGame, MissedSavesPerGamePercentiles), 1);
                dimensions.Technique = Math.Round(100 - ToPercentile(rates.MissedTechPerGame, MissedTechPerGamePercentiles), 1);
                dimensions.MisreadSaves = Math.Round(100 - ToPercentile(rates.MisreadSavesPerGame, MisreadSavesPerGamePercentiles), 1);
                dimensions.CleanClues = Math.Round(100 - ToPercentile(rates.GoodTouchPerClue, GoodTouchPerCluePercentiles), 1);

                // Neutral behavioral: direct percentile
                dimensions.Boldness = Math.Round(ToPercentile(rates.PlayRate, PlayRatePercentiles), 1);
                dimensions.Efficiency = Math.Round(ToPercentile(rates.ClueRate, ClueRatePercentiles), 1);
                dimensions.DiscardFrequency = Math.Round(ToPercentile(rates.DiscardRate, DiscardRatePercentiles), 1);

                // Raw ratio (not percentile-based): 0 = all rank, 100 = all color
                dimensions.ColorPreference = Math.Round(rates.ColorClueRate * 100, 1);
            }

            var result = new PlaystyleResponse
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
                MisreadSaves = misreadSaves,
                Rates = rates,
                Dimensions = dimensions
            };

            _cache.Set(cacheKey, result, TimeSpan.FromHours(1));

            return Ok(result);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch history for playstyle profile");
            return StatusCode(502, "Failed to fetch data from hanab.live");
        }
    }
}
