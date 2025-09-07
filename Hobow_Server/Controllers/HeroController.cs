using Microsoft.AspNetCore.Mvc;
using Hobow_Server.Services;
using Microsoft.AspNetCore.Authorization;
using SharedLibrary.Requests;
using SharedLibrary.Responses;

namespace Hobow_Server.Controllers;

[Authorize]
[ApiController]
[Route("[controller]")]
public class HeroController : ControllerBase
{
    private readonly IHeroService _heroService;

    public HeroController(IHeroService heroService)
    {
        _heroService = heroService;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<HeroResponse>> Get([FromRoute] int id)
    {
        try
        {
            var hero = await _heroService.GetHeroResponseAsync(id);
            return Ok(hero);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = "Internal server error" });
        }
    }

    [HttpPost]
    public async Task<ActionResult<HeroResponse>> Post(CreateHeroRequest request)
    {
        try
        {
            var userId = int.Parse(User.FindFirst("id").Value);
            var hero = await _heroService.CreateHeroResponseAsync(request, userId);
            return Ok(hero);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = "Internal server error" });
        }
    }

    [HttpGet("my")]
    public async Task<ActionResult<IEnumerable<HeroResponse>>> MyHeroes()
    {
        try
        {
            var userIdClaim = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized(new { Error = "User not authenticated" });

            var userId = int.Parse(userIdClaim);
            var heroes = await _heroService.GetUserHeroesResponseAsync(userId);
            return Ok(heroes);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = "Internal server error" });
        }
    }

    [HttpPost("select/{id}")]
    public async Task<ActionResult<HeroResponse>> SelectHero([FromRoute] int id)
    {
        try
        {
            var userIdClaim = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized(new { Error = "User not authenticated" });

            var userId = int.Parse(userIdClaim);            
            var hero = await _heroService.SelectHeroAsync(id, userId, "REST_API");

            if (hero == null)
                return NotFound(new { Error = $"Hero with ID {id} not found" });

            return Ok(hero);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = "Internal server error" });
        }
    }

    [HttpGet("active")]
    public async Task<ActionResult<HeroResponse>> GetActiveHero()
    {
        try
        {
            var userIdClaim = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized(new { Error = "User not authenticated" });

            var userId = int.Parse(userIdClaim);
            var hero = await _heroService.GetActiveHeroResponseAsync(userId);

            if (hero == null)
                return NotFound(new { Error = "No active hero selected" });

            return Ok(hero);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = "Internal server error" });
        }
    }

    [HttpPost("deselect")]
    public async Task<ActionResult> DeselectHero()
    {
        try
        {
            var userIdClaim = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized(new { Error = "User not authenticated" });

            var userId = int.Parse(userIdClaim);
            await _heroService.DeselectHeroAsync(userId);

            return Ok(new { Message = "Hero deselected successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = "Internal server error" });
        }
    }
}