using Microsoft.AspNetCore.Mvc;
using SharedLibrary;
using Hobow_Server.Services;
using Microsoft.AspNetCore.Authorization;
using SharedLibrary.Requests;
using Microsoft.EntityFrameworkCore;
using SharedLibrary.Responses;

namespace Hobow_Server.Controllers;

[Authorize]
[ApiController]
[Route("[controller]")]
public class HeroController : ControllerBase
{
    private readonly IHeroService _heroService;
    private readonly GameDbContext _context;

    public HeroController(IHeroService heroService, GameDbContext context)
    {
        _heroService = heroService;
        _context = context;


        var user = new User
        {
            Username = "TestUser",
            PasswordHash = "password",
        };

        _context.Add(user);
        _context.SaveChanges();
    }
    [HttpGet("{id}")]
    public HeroResponse Get([FromRoute] int id)
    {
        var hero = _context.Heroes.Include(h => h.User).First(h => h.Id == id);
        _heroService.DoSomething();
        return new HeroResponse
        {
            Id = hero.Id,
            Name = hero.Name,
            Level = hero.Level,
            UserId = hero.User.Id,
            Username = hero.User.Username
        };
    }

    [HttpPost]
    public HeroResponse Post(CreateHeroRequest request)
    {
        var userId = int.Parse(User.FindFirst("id").Value);
        var user = _context.Users.Include(u => u.Heroes).First(u => u.Id == userId);

        var hero = new Hero(){
            Name = request.Name,
            User = user
        };

        _context.Add(hero);
        _context.SaveChanges();

        return new SharedLibrary.Responses.HeroResponse
        {
            Id = hero.Id,
            Name = hero.Name,
            Level = hero.Level,
            UserId = hero.User.Id,
            Username = hero.User.Username
        };
    }

    [Authorize]
    [HttpGet("my")]
    public IEnumerable<HeroResponse> MyHeroes()
    {
        var userIdClaim = User.FindFirst("id")?.Value;
        if (string.IsNullOrEmpty(userIdClaim)) return Enumerable.Empty<HeroResponse>();
        var userId = int.Parse(userIdClaim);

        var heroes = _context.Heroes.Include(h => h.User).Where(h => h.User.Id == userId).ToList();
        return heroes.Select(hero => new HeroResponse
        {
            Id = hero.Id,
            Name = hero.Name,
            Level = hero.Level,
            UserId = hero.User.Id,
            Username = hero.User.Username
        });
    }

    [Authorize]
    [HttpPost("select/{id}")]
    public ActionResult<HeroResponse> SelectHero([FromRoute] int id)
    {
        var userIdClaim = User.FindFirst("id")?.Value;
        if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();

        var userId = int.Parse(userIdClaim);
        var hero = _context.Heroes.Include(h => h.User).FirstOrDefault(h => h.Id == id);
        if (hero == null || hero.User.Id != userId) return Forbid();

        return new HeroResponse
        {
            Id = hero.Id,
            Name = hero.Name,
            Level = hero.Level,
            UserId = hero.User.Id,
            Username = hero.User.Username
        };
    }
}