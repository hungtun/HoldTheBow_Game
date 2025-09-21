using Hobow_Server.Handlers;

namespace Hobow_Server.Services;

/// <summary>
/// Background service to update arrow physics and remove stuck arrows
/// </summary>
public class ArrowUpdateService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ArrowUpdateService> _logger;

    public ArrowUpdateService(IServiceProvider serviceProvider, ILogger<ArrowUpdateService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[ArrowUpdateService] Starting arrow update service");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var arrowHandler = scope.ServiceProvider.GetRequiredService<IArrowHandler>();

                // Update arrow positions and check collisions
                await arrowHandler.UpdateArrowsAsync();

                // Remove arrows that have been stuck for 5 seconds
                await arrowHandler.RemoveStuckArrowsAsync();

                // Run at ~60 FPS
                await Task.Delay(16, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ArrowUpdateService] Error in arrow update loop");
                await Task.Delay(1000, stoppingToken); // Wait 1 second before retrying
            }
        }

        _logger.LogInformation("[ArrowUpdateService] Arrow update service stopped");
    }
}
