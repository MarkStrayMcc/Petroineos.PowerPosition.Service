using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Petroineos.PowerPosition.Service.Health;
using Petroineos.PowerPosition.Service.Interfaces;
using Petroineos.PowerPosition.Service.Metrics;

namespace Petroineos.PowerPosition.Service
{
    public class PowerPositionBackgroundService : BackgroundService
    {
        private readonly IPowerPositionWorker _worker;
        private readonly ServiceConfiguration _configuration;
        private readonly ILogger<PowerPositionBackgroundService> _logger;
        private readonly IHealthMonitor _healthMonitor;
        private readonly IMetricsService _metrics;
        private PeriodicTimer? _extractTimer;
        private PeriodicTimer? _cleanupTimer;
        private DateTime _lastCleanupRun = DateTime.MinValue;

        public PowerPositionBackgroundService(
            IPowerPositionWorker worker,
            ServiceConfiguration configuration,
            ILogger<PowerPositionBackgroundService> logger,
            IHealthMonitor healthMonitor,
            IMetricsService metrics)
        {
            _worker = worker;
            _configuration = configuration;
            _logger = logger;
            _healthMonitor = healthMonitor;
            _metrics = metrics;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Background service started. Performing initial run...");

            // Initial run when service starts
            await RunExtractAsync(stoppingToken);

            _logger.LogInformation("Scheduling extracts every {IntervalMinutes} minutes", _configuration.IntervalMinutes);

            // Setup independent timers
            _extractTimer = new PeriodicTimer(TimeSpan.FromMinutes(_configuration.IntervalMinutes));
            _cleanupTimer = new PeriodicTimer(TimeSpan.FromMinutes(5));

            // Run both timers independently using Task.WhenAny
            var extractTask = RunExtractTimerAsync(stoppingToken);
            var cleanupTask = RunCleanupTimerAsync(stoppingToken);

            // Wait for either task to complete (which happens when cancellation is requested)
            await Task.WhenAny(extractTask, cleanupTask);

            _logger.LogInformation("Background service stopping...");
        }

        private async Task RunExtractTimerAsync(CancellationToken stoppingToken)
        {
            try
            {
                while (await _extractTimer!.WaitForNextTickAsync(stoppingToken))
                {
                    await RunExtractAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Extract timer stopped due to cancellation");
            }
        }

        private async Task RunCleanupTimerAsync(CancellationToken stoppingToken)
        {
            try
            {
                while (await _cleanupTimer!.WaitForNextTickAsync(stoppingToken))
                {
                    await RunCleanupIfDueAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Cleanup timer stopped due to cancellation");
            }
        }

        internal async Task RunExtractAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting scheduled power position extraction...");
                var tradesProcessed = await _worker.GeneratePowerPositionAsync();

                // Record successful run for health monitoring and metrics
                _healthMonitor.RecordSuccessfulRun();
                _metrics.RecordSuccessfulRun(tradesProcessed);

                _logger.LogInformation("Scheduled power position extraction completed successfully. Trades processed: {TradesProcessed}", tradesProcessed);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Record failed run for metrics
                _metrics.RecordFailedRun();
                _logger.LogError(ex, "Error during scheduled power position extraction");
            }
        }

        internal Task RunCleanupIfDueAsync(CancellationToken cancellationToken = default)
        {
            if (!_configuration.EnableFileCleanup)
            {
                _logger.LogDebug("File cleanup is disabled");
                return Task.CompletedTask;
            }

            // Check if it's time for cleanup (e.g., once per day)
            if (DateTime.Now - _lastCleanupRun >= TimeSpan.FromHours(_configuration.CleanupIntervalHours))
            {
                try
                {
                    _logger.LogInformation("Starting scheduled file cleanup...");
                    var retentionPeriod = TimeSpan.FromDays(_configuration.FileRetentionDays);
                    _worker.CleanupOldFiles(retentionPeriod);
                    _lastCleanupRun = DateTime.Now;
                    _logger.LogInformation("Scheduled file cleanup completed successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during scheduled file cleanup");
                    // Don't re-throw - cleanup failures shouldn't stop the main service
                }
            }
            else
            {
                _logger.LogDebug("File cleanup not due yet. Next cleanup at: {NextCleanupTime}",
                    _lastCleanupRun.AddHours(_configuration.CleanupIntervalHours));
            }

            return Task.CompletedTask;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Power Position Background Service starting...");
            await base.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Power Position Background Service stopping...");

            // Log final metrics before stopping
            _metrics.LogMetricsSummary();

            _extractTimer?.Dispose();
            _cleanupTimer?.Dispose();

            await base.StopAsync(cancellationToken);
            _logger.LogInformation("Power Position Background Service stopped successfully");
        }

        public override void Dispose()
        {
            _extractTimer?.Dispose();
            _cleanupTimer?.Dispose();
            base.Dispose();
        }
    }
}