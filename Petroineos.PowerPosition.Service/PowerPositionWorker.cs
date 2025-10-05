using System.Globalization;
using Microsoft.Extensions.Logging;
using Services;

namespace Petroineos.PowerPosition.Service
{
    public class PowerPositionWorker
    {
        private readonly IPowerService _powerService;
        private readonly ILogger<PowerPositionWorker> _logger;
        private readonly ServiceConfiguration _config;

        public PowerPositionWorker(
            IPowerService powerService,
            ILogger<PowerPositionWorker> logger,
            ServiceConfiguration config)
        {
            _powerService = powerService;
            _logger = logger;
            _config = config;
        }

        public async Task GeneratePowerPositionAsync()
        {
            int retryCount = 0;

            while (retryCount < _config.RetryCount)
            {
                try
                {
                    _logger.LogInformation("Starting power position extraction...");

                    var now = DateTime.Now;
                    var extractTime = now;
                    var tradeDate = GetTradeDate(now);

                    _logger.LogInformation("Retrieving trades for date: {TradeDate:yyyy-MM-dd}, Extract time: {ExtractTime:yyyy-MM-dd HH:mm}",
                        tradeDate, extractTime);

                    // Get power trades asynchronously from PowerService
                    IEnumerable<PowerTrade> trades;
                    try
                    {
                        trades = await _powerService.GetTradesAsync(tradeDate);
                        _logger.LogInformation("Successfully retrieved {TradeCount} trades from PowerService", trades.Count());
                    }
                    catch (PowerServiceException ex)
                    {
                        _logger.LogError(ex, "Power service error while retrieving trades for date {TradeDate}", tradeDate);
                        throw;
                    }

                    var aggregatedVolumes = AggregateVolumes(trades);
                    var csvContent = GenerateCsvContent(aggregatedVolumes);
                    var fileName = GenerateFileName(extractTime);
                    var filePath = Path.Combine(_config.OutputDirectory, fileName);

                    Directory.CreateDirectory(_config.OutputDirectory);
                    await File.WriteAllTextAsync(filePath, csvContent);

                    _logger.LogInformation("Power position report generated successfully: {FilePath}", filePath);
                    _logger.LogInformation("Total trades processed: {TradeCount}, Total periods aggregated: {PeriodCount}",
                        trades.Count(), aggregatedVolumes.Count);

                    return; // Success, exit retry loop
                }
                catch (PowerServiceException ex)
                {
                    retryCount++;
                    _logger.LogWarning(ex, "Power service error (attempt {RetryCount}/{MaxRetries})",
                        retryCount, _config.RetryCount);

                    if (retryCount >= _config.RetryCount)
                    {
                        _logger.LogError(ex, "Max retry attempts reached. Failed to generate power position report.");
                        throw;
                    }

                    // Exponential backoff
                    var delay = TimeSpan.FromMilliseconds(_config.RetryDelayMilliseconds * Math.Pow(2, retryCount - 1));
                    _logger.LogInformation("Waiting {DelayMs}ms before retry...", delay.TotalMilliseconds);
                    await Task.Delay(delay);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error generating power position report");
                    throw;
                }
            }
        }

        internal DateTime GetTradeDate(DateTime currentTime)
        {
            // Trading day starts at 23:00 previous day
            // If current time is 23:00 or later, we need trades for the next calendar day
            // If current time is before 23:00, we need trades for the current calendar day
            return currentTime.Hour >= 23 ? currentTime.Date.AddDays(1) : currentTime.Date;
        }

        internal Dictionary<string, double> AggregateVolumes(IEnumerable<PowerTrade> trades)
        {
            var aggregated = new Dictionary<string, double>();

            foreach (var trade in trades)
            {
                foreach (var period in trade.Periods)
                {
                    var timeString = PeriodToTimeString(period.Period);

                    if (aggregated.ContainsKey(timeString))
                    {
                        aggregated[timeString] += period.Volume;
                    }
                    else
                    {
                        aggregated[timeString] = period.Volume;
                    }
                }
            }

            // Proper 24-hour sorting with 23:00 first
            return aggregated
                .OrderBy(kvp =>
                {
                    var time = TimeSpan.Parse(kvp.Key + ":00");
                    return time.Hours == 23 ? TimeSpan.FromHours(-1) : time;
                })
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        internal string PeriodToTimeString(int period)
        {
            // Period 1 starts at 23:00
            // Period 2 starts at 00:00  
            // Period 3 starts at 01:00, etc.
            return period == 1 ? "23:00" : $"{period - 2:D2}:00";
        }

        internal string GenerateCsvContent(Dictionary<string, double> aggregatedVolumes)
        {
            var csvLines = new List<string> { "Local Time,Volume" };

            foreach (var kvp in aggregatedVolumes)
            {
                csvLines.Add($"{kvp.Key},{kvp.Value.ToString("F1", CultureInfo.InvariantCulture)}");
            }

            return string.Join(Environment.NewLine, csvLines);
        }

        internal string GenerateFileName(DateTime extractTime)
        {
            return $"PowerPosition_{extractTime:yyyyMMdd}_{extractTime:HHmm}.csv";
        }

        public void CleanupOldFiles(TimeSpan retentionPeriod)
        {
            try
            {
                if (!Directory.Exists(_config.OutputDirectory))
                    return;

                var cutoff = DateTime.Now - retentionPeriod;
                var files = Directory.GetFiles(_config.OutputDirectory, "PowerPosition_*.csv");

                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTime < cutoff)
                    {
                        fileInfo.Delete();
                        _logger.LogInformation("Deleted old file: {FileName}", file);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup old files");
            }
        }
    }
}