using Hobow_Server.Handlers;

namespace Hobow_Server.Services;

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

                await arrowHandler.UpdateArrowsAsync();

                await arrowHandler.RemoveStuckArrowsAsync();

                await Task.Delay(16, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ArrowUpdateService] Error in arrow update loop");
                await Task.Delay(1000, stoppingToken); 
            }
        }

        _logger.LogInformation("[ArrowUpdateService] Arrow update service stopped");
    }
}
