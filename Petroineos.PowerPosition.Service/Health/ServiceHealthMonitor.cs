using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Petroineos.PowerPosition.Service.Interfaces;

namespace Petroineos.PowerPosition.Service.Health
{
    public class ServiceHealthMonitor : IHostedService, IHealthMonitor
    {
        private readonly ILogger<ServiceHealthMonitor> _logger;
        private readonly ServiceConfiguration _config;
        private Timer? _healthTimer;
        private DateTime _lastSuccessfulRun = DateTime.MinValue;

        public ServiceHealthMonitor(ILogger<ServiceHealthMonitor> logger, ServiceConfiguration config)
        {
            _logger = logger;
            _config = config;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Health monitor started");
            _healthTimer = new Timer(CheckHealth, null, TimeSpan.Zero, TimeSpan.FromMinutes(5));
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _healthTimer?.Dispose();
            return Task.CompletedTask;
        }

        public void RecordSuccessfulRun() => _lastSuccessfulRun = DateTime.Now;

        private void CheckHealth(object? state)
        {
            try
            {
                var timeSinceLastRun = DateTime.Now - _lastSuccessfulRun;

                // Alert if no successful runs in 2x the normal interval
                if (timeSinceLastRun > TimeSpan.FromMinutes(_config.IntervalMinutes * 2))
                {
                    _logger.LogWarning(
                        "Health check alert: No successful runs for {Minutes} minutes",
                        timeSinceLastRun.TotalMinutes);
                }

                // Check disk space
                CheckDiskSpace();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during health check");
            }
        }

        private void CheckDiskSpace()
        {
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(_config.OutputDirectory) ?? "C:\\");
                if (drive.AvailableFreeSpace < 100 * 1024 * 1024) // 100MB threshold
                {
                    _logger.LogWarning(
                        "Low disk space alert: {AvailableGB:0.0}GB available on {Drive}",
                        drive.AvailableFreeSpace / (1024 * 1024 * 1024.0),
                        drive.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not check disk space");
            }
        }
    }
}