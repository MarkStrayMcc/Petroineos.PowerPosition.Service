using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Petroineos.PowerPosition.Service
{
    public class PowerPositionBackgroundService : BackgroundService
    {
        private readonly ILogger<PowerPositionBackgroundService> _logger;

        public PowerPositionBackgroundService(ILogger<PowerPositionBackgroundService> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Background service started - TODO: Implement scheduling logic");

            // Placeholder - will be implemented in future PR
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                _logger.LogInformation("Scheduled execution - TODO: Call PowerPositionWorker");
            }
        }
    }
}