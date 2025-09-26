using Hobow_Server.Handlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SharedLibrary.Requests;
using SharedLibrary.Responses;
using SharedLibrary.Events;

namespace Hobow_Server.Hubs;

[Authorize]
public class HeroHub : Hub
{
    private readonly IHeroHandler _heroHandler;
    private readonly IMapDataHandler _mapDataHandler;
    private readonly IArrowHandler _arrowHandler;
    private readonly ILogger<HeroHub> _logger;

    public HeroHub(IHeroHandler heroHandler, IMapDataHandler mapDataHandler, IArrowHandler arrowHandler, ILogger<HeroHub> logger)
    {
        _heroHandler = heroHandler;
        _mapDataHandler = mapDataHandler;
        _arrowHandler = arrowHandler;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {    
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {            
            var userIdClaim = Context.User?.FindFirst("id")?.Value;
            if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out var userId))
            {
                await _heroHandler.RemoveUserActiveHeroAsync(userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HeroHub] Error handling disconnect");
        }
        
        await base.OnDisconnectedAsync(exception);
    }


    public async Task OnHeroMoveIntent(HeroMoveIntentEvent moveIntent)
    {
        try
        {
            var updateEvent = _heroHandler.ProcessMoveIntent(moveIntent);

            if (updateEvent != null)
            {
                await Clients.All.SendAsync("HeroMoveUpdate", updateEvent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HeroHub] Error processing hero move intent for hero {HeroId}", moveIntent.HeroId);
        }
    }

    public async Task Spawn(HeroSpawnRequest request)
    {
        var response = _heroHandler.ProcessSpawn(request);
        if (response != null)
        {
            await Clients.All.SendAsync("HeroSpawned", response);
        }
    }
    
    public async Task RequestCurrentPlayers()
    {
        var allHeroes = _heroHandler.GetAllHeroes();
        
        foreach (var hero in allHeroes)
        {
            var spawnResponse = new HeroSpawnResponse
            {
                HeroId = hero.Id,
                X = hero.X,
                Y = hero.Y,
                MaxHealth = hero.MaxHealth,
                CurrentHealth = hero.CurrentHealth,
                Damage = hero.Damage,
                ServerTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            
            await Clients.Caller.SendAsync("HeroSpawned", spawnResponse);
        }
    }

    public async Task Logout(int heroId)
    {
        try
        {
            var userIdClaim = Context.User?.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
            {
                await Clients.Caller.SendAsync("Error", "User not authenticated");
                return;
            }

            var userId = int.Parse(userIdClaim);
            var connectionId = Context.ConnectionId;            
            await _heroHandler.RemoveUserActiveHeroAsync(userId);
            
            await Clients.Caller.SendAsync("LogoutAck", heroId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HeroHub] Failed to logout user");
            await Clients.Caller.SendAsync("Error", "Failed to logout");
        }
    }


    public async Task OnBowStateIntent(BowStateIntentEvent bowStateIntent)
    {
        try
        {
            var updateEvent = _heroHandler.ProcessBowStateIntent(bowStateIntent);

            if (updateEvent != null)
            {                
                await Clients.Others.SendAsync("BowStateUpdate", updateEvent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HeroHub] Error processing bow state intent for hero {HeroId}", bowStateIntent.HeroId);
        }
    }


    public async Task RequestMapData(MapDataRequest request)
    {        
        var response = _mapDataHandler.GetMapData(request.MapId);
        
        if (response.Success)
        {
            await Clients.Caller.SendAsync("MapDataReceived", response);
        }
        else
        {
            await Clients.Caller.SendAsync("MapDataError", response);
        }
    }

    public async Task ConfirmMapDataReceived(MapDataReceivedRequest request)
    {        
        var response = _mapDataHandler.ConfirmMapDataReceived(request.MapId, request.HeroId);
        
        if (response.Success)
        {
            await Clients.Caller.SendAsync("MapDataConfirmed", response);
        }
        else
        {
            await Clients.Caller.SendAsync("MapDataConfirmError", response);
        }
    }


    public async Task OnArrowShootIntent(ArrowShootIntentEvent shootIntent)
    {
        try
        {            
            var spawnedEvent = _arrowHandler.ProcessArrowShootIntent(shootIntent, Context.UserIdentifier ?? Context.ConnectionId);

            if (spawnedEvent != null)
            {                
                await Clients.All.SendAsync("ArrowSpawned", spawnedEvent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HeroHub] Error processing arrow shoot intent for hero {HeroId}", shootIntent.HeroId);
        }
    }
}