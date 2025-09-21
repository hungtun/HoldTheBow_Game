using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Hobow_Server.Handlers;
using SharedLibrary.Responses;
using SharedLibrary.Events;

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

        // Gửi tất cả enemy hiện tại cho client mới kết nối
        var enemies = _enemyHandler.GetAllEnemies();
        _logger.LogInformation("[EnemyHub] Sending {EnemyCount} existing enemies to new client", enemies.Count());
        
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


    /// <summary>
    /// Handle enemy movement intent event (new event-based approach)
    /// </summary>
    public async Task OnEnemyMoveIntent(EnemyMoveIntentEvent moveIntent)
    {
        try
        {
            var updateEvent = _enemyHandler.ProcessEnemyMoveIntent(moveIntent);

            if (updateEvent != null)
            {
                Console.WriteLine($"[Server] Enemy {moveIntent.EnemyId} move intent to ({updateEvent.X}, {updateEvent.Y})");
                
                // Send movement update to all clients
                await Clients.All.SendAsync("EnemyMoveUpdate", updateEvent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EnemyHub] Error processing enemy move intent for enemy {EnemyId}", moveIntent.EnemyId);
        }
    }
}