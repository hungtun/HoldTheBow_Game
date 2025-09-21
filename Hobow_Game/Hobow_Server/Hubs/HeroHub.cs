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
        _logger.LogInformation("[HeroHub] Client {ConnectionId} connected", Context.ConnectionId);
        
        var userIdClaim = Context.User?.FindFirst("id")?.Value;
        if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out var userId))
        {
            _logger.LogInformation("[HeroHub] User {UserId} connected with connection {ConnectionId}", userId, Context.ConnectionId);
        }
        
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            _logger.LogInformation("[HeroHub] Client {ConnectionId} disconnected", Context.ConnectionId);
            
            var userIdClaim = Context.User?.FindFirst("id")?.Value;
            if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out var userId))
            {
                _logger.LogInformation("[HeroHub] User {UserId} disconnected from connection {ConnectionId}", userId, Context.ConnectionId);
                
                await _heroHandler.RemoveUserActiveHeroAsync(userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HeroHub] Error handling disconnect");
        }
        
        await base.OnDisconnectedAsync(exception);
    }


    /// <summary>
    /// Handle hero movement intent event (new event-based approach)
    /// </summary>
    public async Task OnHeroMoveIntent(HeroMoveIntentEvent moveIntent)
    {
        try
        {
            var updateEvent = _heroHandler.ProcessMoveIntent(moveIntent, Context.UserIdentifier ?? Context.ConnectionId);

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
        var response = _heroHandler.ProcessSpawn(request, Context.UserIdentifier ?? Context.ConnectionId);
        
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
                ServerTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            
            await Clients.Caller.SendAsync("HeroSpawned", spawnResponse);
        }
        
        Console.WriteLine($"[HeroHub] Sent {allHeroes.Count()} current heroes to new client");
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
            
            _logger.LogInformation("[HeroHub] User {UserId} logged out from connection {ConnectionId}", userId, connectionId);
            
            await _heroHandler.RemoveUserActiveHeroAsync(userId);
            
            await Clients.Caller.SendAsync("LogoutAck", heroId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HeroHub] Failed to logout user");
            await Clients.Caller.SendAsync("Error", "Failed to logout");
        }
    }

    /// <summary>
    /// Handle bow state intent event (new event-based approach)
    /// </summary>
    public async Task OnBowStateIntent(BowStateIntentEvent bowStateIntent)
    {
        try
        {
            var updateEvent = _heroHandler.ProcessBowStateIntent(bowStateIntent, Context.UserIdentifier ?? Context.ConnectionId);

            if (updateEvent != null)
            {
                Console.WriteLine($"[Server] Hero {bowStateIntent.HeroId} bow state: angle={updateEvent.AngleDeg:F1} charging={updateEvent.IsCharging} charge%={updateEvent.ChargePercent:F2}");
                
                // Send bow state update to all other clients (not the sender)
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
        Console.WriteLine($"[Server] Client requesting map data for: {request.MapId}");
        
        var response = _mapDataHandler.GetMapData(request.MapId);
        
        if (response.Success)
        {
            Console.WriteLine($"[Server] Sending map data to client: {response.Data.Layers.Count} layers, {response.Data.Layers.Sum(l => l.Objects.Count)} collision objects");
            await Clients.Caller.SendAsync("MapDataReceived", response);
        }
        else
        {
            Console.WriteLine($"[Server] Failed to get map data: {response.Error}");
            await Clients.Caller.SendAsync("MapDataError", response);
        }
    }

    public async Task ConfirmMapDataReceived(MapDataReceivedRequest request)
    {
        Console.WriteLine($"[Server] Hero {request.HeroId} confirmed received map data for: {request.MapId}");
        
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

    /// <summary>
    /// Handle arrow shoot intent event (new event-based approach)
    /// </summary>
    public async Task OnArrowShootIntent(ArrowShootIntentEvent shootIntent)
    {
        try
        {
            Console.WriteLine($"[Server] Received arrow shoot intent from hero {shootIntent.HeroId}, charge: {shootIntent.ChargePercent:F2}");
            
            var spawnedEvent = _arrowHandler.ProcessArrowShootIntent(shootIntent, Context.UserIdentifier ?? Context.ConnectionId);

            if (spawnedEvent != null)
            {
                Console.WriteLine($"[Server] Hero {shootIntent.HeroId} shot arrow {spawnedEvent.ArrowId}, damage: {spawnedEvent.Damage:F1}");
                
                // Send arrow spawned event to all clients
                await Clients.All.SendAsync("ArrowSpawned", spawnedEvent);
                Console.WriteLine($"[Server] Sent ArrowSpawned event to all clients");
            }
            else
            {
                Console.WriteLine($"[Server] Failed to process arrow shoot intent for hero {shootIntent.HeroId}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HeroHub] Error processing arrow shoot intent for hero {HeroId}", shootIntent.HeroId);
        }
    }
}