using Hobow_Server.Handlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SharedLibrary.Requests;
using SharedLibrary.Responses;

namespace Hobow_Server.Hubs;

[Authorize]
public class HeroHub : Hub
{
    private readonly IHeroHandler _heroHandler;
    private readonly IMapDataHandler _mapDataHandler;
    private readonly ILogger<HeroHub> _logger;

    public HeroHub(IHeroHandler heroHandler, IMapDataHandler mapDataHandler, ILogger<HeroHub> logger)
    {
        _heroHandler = heroHandler;
        _mapDataHandler = mapDataHandler;
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

    public async Task Move(HeroMoveRequest request)
    {
        var response = _heroHandler.ProcessMove(request, Context.UserIdentifier ?? Context.ConnectionId);

        if (response != null)
        {
            Console.WriteLine($"[Server] Player {request.HeroId} move to {request.Direction}");
            await Clients.All.SendAsync("HeroMoved", response);
            
            Console.WriteLine($"[Server] Player {response.HeroId} move to {request.Direction} at ({response.X}, {response.Y})");
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

    public async Task UpdateBowState(BowStateRequest request)
    {
        Console.WriteLine($"[HeroHub] UpdateBowState hero={request.HeroId} angle={request.AngleDeg:F1} charging={request.IsCharging} charge%={request.ChargePercent:F2}");
        var resp = new BowStateResponse
        {
            HeroId = request.HeroId,
            AngleDeg = request.AngleDeg,
            IsCharging = request.IsCharging,
            ChargePercent = request.ChargePercent,
            ServerTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        await Clients.Others.SendAsync("BowStateUpdated", resp);
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
}