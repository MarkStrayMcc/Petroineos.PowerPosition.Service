using System.Globalization;
using Microsoft.Extensions.Logging;
using Petroineos.PowerPosition.Service.Interfaces;
using Services;

namespace Petroineos.PowerPosition.Service
{
    public class PowerPositionWorker : IPowerPositionWorker
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

        public async Task<int> GeneratePowerPositionAsync()
        {
            int retryCount = 0;

            while (retryCount <= _config.RetryCount) // Use <= to include the final attempt
            {
                try
                {
                    _logger.LogInformation("Starting power position extraction...");

                    var now = GetLondonNow();
                    var extractTime = now;
                    var tradeDate = GetTradeDate(now);

                    _logger.LogInformation("Retrieving trades for date: {TradeDate:yyyy-MM-dd}, Extract time: {ExtractTime:yyyy-MM-dd HH:mm}",
                        tradeDate, extractTime);

                    // Get power trades asynchronously from PowerService
                    IEnumerable<PowerTrade> trades;
                    int tradeCount = 0;
                    string? errorReason = null;

                    try
                    {
                        trades = await _powerService.GetTradesAsync(tradeDate);
                        tradeCount = trades.Count();
                        _logger.LogInformation("Successfully retrieved {TradeCount} trades from PowerService", tradeCount);
                    }
                    catch (Exception ex) when (ex is PowerServiceException || ex is Exception)
                    {
                        // For PowerService errors, we'll retry but track the error reason
                        _logger.LogWarning(ex, "Error retrieving trades for date {TradeDate} (attempt {RetryCount}/{MaxRetries})",
                            tradeDate, retryCount + 1, _config.RetryCount + 1);

                        // If this is the final attempt, we'll generate an error report
                        if (retryCount == _config.RetryCount)
                        {
                            errorReason = $"{ex.GetType().Name}: {ex.Message}";
                            _logger.LogError(ex, "Final attempt failed. Will generate error report.");
                        }

                        trades = Enumerable.Empty<PowerTrade>();
                        tradeCount = 0;

                        // If not final attempt, re-throw to trigger retry
                        if (retryCount < _config.RetryCount)
                        {
                            throw;
                        }
                    }

                    Dictionary<string, double> aggregatedVolumes;
                    if (tradeCount == 0 && errorReason == null)
                    {
                        _logger.LogWarning("No trades returned for date {TradeDate}. Generating report with zero volumes.", tradeDate);
                        aggregatedVolumes = GenerateEmptyVolumes();
                    }
                    else if (errorReason != null)
                    {
                        _logger.LogWarning("Service failure after all retry attempts. Generating error report.");
                        aggregatedVolumes = GenerateErrorVolumes();
                    }
                    else
                    {
                        aggregatedVolumes = AggregateVolumes(trades);
                    }

                    var csvContent = GenerateCsvContent(aggregatedVolumes, errorReason, extractTime);
                    var fileName = GenerateFileName(extractTime, errorReason != null);
                    var filePath = Path.Combine(_config.OutputDirectory, fileName);

                    Directory.CreateDirectory(_config.OutputDirectory);
                    await File.WriteAllTextAsync(filePath, csvContent);

                    _logger.LogInformation("Power position report generated successfully: {FilePath}", filePath);
                    _logger.LogInformation("Total trades processed: {TradeCount}, Total periods aggregated: {PeriodCount}",
                        tradeCount, aggregatedVolumes.Count);

                    return tradeCount; // Success, exit retry loop
                }
                catch (Exception ex)
                {
                    retryCount++;

                    if (retryCount > _config.RetryCount)
                    {
                        _logger.LogError(ex, "All retry attempts failed. Generating final error report.");
                        await GenerateFallbackErrorReport($"All retry attempts failed: {ex.GetType().Name} - {ex.Message}");
                        return 0;
                    }

                    var delay = TimeSpan.FromMilliseconds(_config.RetryDelayMilliseconds * Math.Pow(2, retryCount - 1));
                    _logger.LogInformation("Waiting {DelayMs}ms before retry {RetryCount}...", delay.TotalMilliseconds, retryCount + 1);
                    await Task.Delay(delay);
                }
            }

            return 0;
        }

        private async Task GenerateFallbackErrorReport(string errorReason)
        {
            try
            {
                var now = GetLondonNow();
                var extractTime = now;

                var aggregatedVolumes = GenerateErrorVolumes();
                var csvContent = GenerateCsvContent(aggregatedVolumes, errorReason, extractTime);
                var fileName = GenerateFileName(extractTime, true);
                var filePath = Path.Combine(_config.OutputDirectory, fileName);

                Directory.CreateDirectory(_config.OutputDirectory);
                await File.WriteAllTextAsync(filePath, csvContent);

                _logger.LogWarning("Fallback error report generated: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate fallback error report");
                // At this point, we've done everything we can
            }
        }

        public void CleanupOldFiles(TimeSpan retentionPeriod)
        {
            try
            {
                if (!Directory.Exists(_config.OutputDirectory))
                {
                    _logger.LogDebug("Output directory does not exist, nothing to cleanup: {OutputDirectory}", _config.OutputDirectory);
                    return;
                }

                var cutoff = DateTime.Now - retentionPeriod;
                var files = Directory.GetFiles(_config.OutputDirectory, "PowerPosition_*.csv");
                int deletedCount = 0;

                _logger.LogInformation("Starting file cleanup. Retention period: {RetentionDays} days, Cutoff: {Cutoff}",
                    retentionPeriod.TotalDays, cutoff);

                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.CreationTime < cutoff)
                        {
                            fileInfo.Delete();
                            deletedCount++;
                            _logger.LogDebug("Deleted old file: {FileName} (Created: {Created})",
                                fileInfo.Name, fileInfo.CreationTime);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete file: {FileName}", file);
                    }
                }

                _logger.LogInformation("File cleanup completed. Deleted {DeletedCount} files out of {TotalFiles} total files.",
                    deletedCount, files.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup old files");
                throw;
            }
        }

        public DateTime GetTradeDate(DateTime currentTime)
        {
            return currentTime.Hour >= 23 ? currentTime.Date.AddDays(1) : currentTime.Date;
        }

        public Dictionary<string, double> AggregateVolumes(IEnumerable<PowerTrade> trades)
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

            return aggregated
                .OrderBy(kvp =>
                {
                    var time = TimeSpan.Parse(kvp.Key + ":00");
                    return time.Hours == 23 ? TimeSpan.FromHours(-1) : time;
                })
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public Dictionary<string, double> GenerateEmptyVolumes()
        {
            var emptyVolumes = new Dictionary<string, double>();

            for (int period = 1; period <= 24; period++)
            {
                var timeString = PeriodToTimeString(period);
                emptyVolumes[timeString] = 0.0;
            }

            return emptyVolumes
                .OrderBy(kvp =>
                {
                    var time = TimeSpan.Parse(kvp.Key + ":00");
                    return time.Hours == 23 ? TimeSpan.FromHours(-1) : time;
                })
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public Dictionary<string, double> GenerateErrorVolumes()
        {
            var errorVolumes = new Dictionary<string, double>();

            for (int period = 1; period <= 24; period++)
            {
                var timeString = PeriodToTimeString(period);
                errorVolumes[timeString] = -999.0;
            }

            return errorVolumes
                .OrderBy(kvp =>
                {
                    var time = TimeSpan.Parse(kvp.Key + ":00");
                    return time.Hours == 23 ? TimeSpan.FromHours(-1) : time;
                })
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public string PeriodToTimeString(int period)
        {
            return period == 1 ? "23:00" : $"{period - 2:D2}:00";
        }

        internal string GenerateCsvContent(Dictionary<string, double> aggregatedVolumes, string? errorReason, DateTime extractTime)
        {
            var csvLines = new List<string> { "Local Time,Volume" };

            foreach (var kvp in aggregatedVolumes)
            {
                string volumeValue = errorReason != null ? "ERROR" : kvp.Value.ToString("F1", CultureInfo.InvariantCulture);
                csvLines.Add($"{kvp.Key},{volumeValue}");
            }

            if (errorReason != null)
            {
                // Add comprehensive error details as comments at the top
                csvLines.Insert(0, $"# Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                csvLines.Insert(0, $"# Extract Time: {extractTime:yyyy-MM-dd HH:mm:ss}");
                csvLines.Insert(0, $"# ERROR: {errorReason}");
                csvLines.Insert(0, $"# This report contains placeholder data due to service error");
            }

            return string.Join(Environment.NewLine, csvLines);
        }

        internal string GenerateFileName(DateTime extractTime, bool hasError = false)
        {
            var baseName = $"PowerPosition_{extractTime:yyyyMMdd}_{extractTime:HHmm}";
            return hasError ? $"{baseName}_ERROR.csv" : $"{baseName}.csv";
        }

        private static TimeZoneInfo GetLondonTimeZone()
        {
            // Windows and Linux time zone IDs differ slightly;
            // On Windows it's "GMT Standard Time", on Linux/macOS it's "Europe/London"
            // We'll try both.
            var tzIdsToTry = new[] { "Europe/London", "GMT Standard Time" };
            foreach (var id in tzIdsToTry)
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(id);
                }
                catch { }
            }
            // fallback to UTC (shouldn't happen in normal environments)
            return TimeZoneInfo.Utc;
        }

        private static DateTime GetLondonNow()
        {
            var utcNow = DateTime.UtcNow;
            var london = GetLondonTimeZone();
            return TimeZoneInfo.ConvertTimeFromUtc(utcNow, london);
        }
    }
}