using Microsoft.Extensions.Logging;
using Petroineos.PowerPosition.Service.Interfaces;

namespace Petroineos.PowerPosition.Service.Metrics
{
    public class ServiceMetrics : IMetricsService
    {
        private readonly ILogger<ServiceMetrics> _logger;
        private readonly ServiceConfiguration _config;

        // Simple in-memory counters
        private long _successfulRuns = 0;
        private long _failedRuns = 0;
        private long _totalTradesProcessed = 0;
        private DateTime _startTime = DateTime.Now;

        public ServiceMetrics(ILogger<ServiceMetrics> logger, ServiceConfiguration config)
        {
            _logger = logger;
            _config = config;
        }

        public void RecordSuccessfulRun(int tradesProcessed = 0)
        {
            Interlocked.Increment(ref _successfulRuns);
            Interlocked.Add(ref _totalTradesProcessed, tradesProcessed);

            if (_config.EnableDetailedLogging)
            {
                _logger.LogInformation(
                    "Metrics: Successful runs: {SuccessfulRuns}, Total trades: {TotalTrades}",
                    _successfulRuns, _totalTradesProcessed);
            }
        }

        public void RecordFailedRun()
        {
            Interlocked.Increment(ref _failedRuns);

            _logger.LogWarning(
                "Metrics: Failed runs: {FailedRuns}, Success rate: {SuccessRate:P2}",
                _failedRuns, GetSuccessRate());
        }

        public void LogMetricsSummary()
        {
            var uptime = DateTime.Now - _startTime;
            var successRate = GetSuccessRate();

            _logger.LogInformation(
                "Service Metrics Summary - Uptime: {Uptime:h\\:mm\\:ss}, " +
                "Successful runs: {SuccessfulRuns}, Failed runs: {FailedRuns}, " +
                "Success rate: {SuccessRate:P2}, Total trades processed: {TotalTrades}",
                uptime, _successfulRuns, _failedRuns, successRate, _totalTradesProcessed);
        }

        private double GetSuccessRate()
        {
            var total = _successfulRuns + _failedRuns;
            return total == 0 ? 1.0 : (double)_successfulRuns / total;
        }
    }
}