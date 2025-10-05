using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Petroineos.PowerPosition.Service
{
    public class PowerPositionBackgroundService : BackgroundService
    {
        private readonly PowerPositionWorker _worker;
        private readonly ServiceConfiguration _configuration;
        private readonly ILogger<PowerPositionBackgroundService> _logger;
        private PeriodicTimer? _extractTimer;
        private PeriodicTimer? _cleanupTimer;
        private DateTime _lastCleanupRun = DateTime.MinValue;

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
            await RunExtractAsync(stoppingToken);

            _logger.LogInformation("Scheduling extracts every {IntervalMinutes} minutes", _configuration.IntervalMinutes);

            // Setup timers
            _extractTimer = new PeriodicTimer(TimeSpan.FromMinutes(_configuration.IntervalMinutes));
            _cleanupTimer = new PeriodicTimer(TimeSpan.FromMinutes(5)); // Check every 5 minutes

            try
            {
                while (await _cleanupTimer.WaitForNextTickAsync(stoppingToken))
                {
                    // Run extract on its schedule
                    if (_extractTimer != null && await _extractTimer.WaitForNextTickAsync(stoppingToken))
                    {
                        await RunExtractAsync(stoppingToken);
                    }

                    // Run cleanup on its schedule (e.g., once per day)
                    await RunCleanupIfDueAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Background service stopping due to cancellation");
            }
        }

        private async Task RunExtractAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting scheduled power position extraction...");
                await _worker.GeneratePowerPositionAsync();
                _logger.LogInformation("Scheduled power position extraction completed successfully");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error during scheduled power position extraction");
            }
        }

        private async Task RunCleanupIfDueAsync(CancellationToken cancellationToken = default)
        {
            if (!_configuration.EnableFileCleanup)
                return;

            // Check if it's time for cleanup (e.g., once per day)
            if (DateTime.Now - _lastCleanupRun >= TimeSpan.FromHours(_configuration.CleanupIntervalHours))
            {
                try
                {
                    _logger.LogInformation("Starting file cleanup...");
                    var retentionPeriod = TimeSpan.FromDays(_configuration.FileRetentionDays);
                    _worker.CleanupOldFiles(retentionPeriod);
                    _lastCleanupRun = DateTime.Now;
                    _logger.LogInformation("File cleanup completed successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during file cleanup");
                }
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Background service stopping...");
            _extractTimer?.Dispose();
            _cleanupTimer?.Dispose();
            await base.StopAsync(stoppingToken);
        }
    }
}