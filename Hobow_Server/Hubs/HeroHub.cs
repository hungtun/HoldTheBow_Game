using System.Threading.Tasks;
using System.Linq;
using Hobow_Server.Handlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SharedLibrary.Requests;
using SharedLibrary.Responses;

namespace Hobow_Server.Hubs;

[Authorize]
public class HeroHub : Hub
{
    private readonly IHeroMoveHandler _heroMoveHandler;
    private readonly IHeroSpawnHandler _heroSpawnHandler;
    private readonly IPlayerSessionHandler _playerSessionHandler;

    public HeroHub(IHeroMoveHandler heroMoveHandler, IHeroSpawnHandler heroSpawnHandler, IPlayerSessionHandler playerSessionHandler)
    {
        _heroMoveHandler = heroMoveHandler;
        _heroSpawnHandler = heroSpawnHandler;
        _playerSessionHandler = playerSessionHandler;
    }

    public async Task Move(HeroMoveRequest request)
    {
        var response = _heroMoveHandler.ProcessMove(request, Context.UserIdentifier ?? Context.ConnectionId);

        if (response != null)
        {
            Console.WriteLine($"[Server] Player {request.HeroId} move to {request.Direction}");
            // Gửi đến tất cả client (bao gồm chính mình để sync)
            await Clients.All.SendAsync("HeroMoved", response);
            
            Console.WriteLine($"[Server] Player {response.HeroId} move to {request.Direction} at ({response.X}, {response.Y})");
        }
    }

    public async Task Spawn(HeroSpawnRequest request)
    {
        var response = _heroSpawnHandler.ProcessSpawn(request, Context.UserIdentifier ?? Context.ConnectionId);
        
        if (response != null)
        {
            // Gửi đến tất cả client (bao gồm chính mình để sync)
            await Clients.All.SendAsync("HeroSpawned", response);
        }
    }
    
    public async Task RequestCurrentPlayers()
    {
        var allHeroes = _heroMoveHandler.GetAllHeroes();
        
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
        var userId = Context.UserIdentifier ?? Context.ConnectionId;
        await _playerSessionHandler.HandleLogoutAsync(heroId, userId);
        // Gửi ACK về cho client trước khi client tự đóng kết nối
        await Clients.Caller.SendAsync("LogoutAck", heroId);
    }

    // heroId được truyền trực tiếp từ client trong lời gọi Logout
}