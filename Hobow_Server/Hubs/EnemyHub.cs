using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Hobow_Server.Handlers;
using SharedLibrary.Responses;

namespace Hobow_Server.Hubs;

[Authorize]
public class EnemyHub : Hub
{
    private readonly IEnemyHandler _enemyHandler;
    private readonly ILogger<EnemyHub> _logger;

    public EnemyHub(IEnemyHandler enemyHandler, ILogger<EnemyHub> logger)
    {
        _enemyHandler = enemyHandler;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("[EnemyHub] Client {ConnectionId} connected", Context.ConnectionId);

        var enemies = _enemyHandler.GetAllEnemies();
        foreach (var enemy in enemies)
        {
            var response = new EnemySpawnResponse
            {
                EnemyId = enemy.EnemyId,
                EnemyName = enemy.EnemyName,
                MapId = enemy.MapId,
                X = enemy.X,
                Y = enemy.Y
            };
            await Clients.Caller.SendAsync("EnemySpawned", response);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("[EnemyHub] Client {ConnectionId} disconnected", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    // Client gửi request → Hub chuyển đến Handler
    public async Task UpdateEnemyPosition(int enemyId, float x, float y)
    {
        try
        {
            // Hub chỉ chuyển request đến Handler
            await _enemyHandler.UpdateEnemyPositionAsync(enemyId, x, y);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EnemyHub] Failed to update enemy position");
            await Clients.Caller.SendAsync("Error", "Failed to update enemy position");
        }
    }
}