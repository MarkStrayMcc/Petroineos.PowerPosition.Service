using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Petroineos.PowerPosition.Service
{
    public class PowerPositionBackgroundService : BackgroundService
    {
        private readonly PowerPositionWorker _worker;
        private readonly ServiceConfiguration _configuration;
        private readonly ILogger<PowerPositionBackgroundService> _logger;
        private Timer? _timer;

        public PowerPositionBackgroundService(
            PowerPositionWorker worker,
            ServiceConfiguration configuration,
            ILogger<PowerPositionBackgroundService> logger)
        {
            _worker = worker;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Background service started. Performing initial run...");

            // Initial run when service starts
            await RunExtractAsync();

            _logger.LogInformation("Scheduling extracts every {IntervalMinutes} minutes", _configuration.IntervalMinutes);

            // Schedule periodic execution
            _timer = new Timer(async _ => await RunExtractAsync(), null,
                TimeSpan.FromMinutes(_configuration.IntervalMinutes),
                TimeSpan.FromMinutes(_configuration.IntervalMinutes));

            // Keep the service running until stopped
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private async Task RunExtractAsync()
        {
            try
            {
                _logger.LogInformation("Starting scheduled power position extraction...");
                await _worker.GeneratePowerPositionAsync();
                _logger.LogInformation("Scheduled power position extraction completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scheduled power position extraction");
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Background service stopping...");
            _timer?.Dispose();
            await base.StopAsync(stoppingToken);
        }
    }
}