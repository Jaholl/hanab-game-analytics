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

    public HanabiController(IHanabiService hanabiService, ILogger<HanabiController> logger)
    {
        _hanabiService = hanabiService;
        _logger = logger;
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
}
