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
}
