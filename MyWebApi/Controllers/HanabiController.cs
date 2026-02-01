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
    private readonly RuleAnalyzer _analyzer;

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
        _analyzer = new RuleAnalyzer();
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
    public async Task<ActionResult<GameAnalysisResponse>> GetGameAnalysis(int gameId)
    {
        _logger.LogInformation("Analyzing game {GameId}", gameId);

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

            // Simulate game and analyze
            var states = _simulator.SimulateGame(game);
            var violations = _analyzer.AnalyzeGame(game, states);

            response.Violations = violations;
            response.Summary = _analyzer.CreateSummary(violations);
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
}
