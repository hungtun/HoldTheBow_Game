using Hobow_Server.Handlers;

namespace Hobow_Server.Services;

public class EnemyAIService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EnemyAIService> _logger;

    public EnemyAIService(IServiceProvider serviceProvider, ILogger<EnemyAIService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {        
        _logger.LogInformation("[EnemyAIService] Starting enemy AI service...");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var enemyHandler = scope.ServiceProvider.GetRequiredService<IEnemyHandler>();
                var enemies = enemyHandler.GetAllEnemies();
                
                if (enemies.Any())
                {
                    _logger.LogDebug("[EnemyAIService] Updating AI for {EnemyCount} enemies", enemies.Count());
                    await enemyHandler.UpdateEnemyAIAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EnemyAIService] Error in enemy AI update");
            }
            await Task.Delay(100, stoppingToken);
        }
    }
}