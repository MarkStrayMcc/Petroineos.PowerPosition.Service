using System.Globalization;
using Microsoft.Extensions.Logging;
using Services;

namespace Petroineos.PowerPosition.Service
{
    public class PowerPositionWorker
    {
        private readonly ILogger<PowerPositionWorker> _logger;
        private readonly ServiceConfiguration _config;

        public PowerPositionWorker(
            ILogger<PowerPositionWorker> logger,
            ServiceConfiguration config)
        {
            _logger = logger;
            _config = config;
        }

        public async Task GeneratePowerPositionAsync()
        {
            try
            {
                _logger.LogInformation("Starting power position extraction...");

                // For this PR, we'll simulate trade data
                // In the next PR, we'll integrate with the actual PowerService
                var simulatedTrades = GenerateSimulatedTrades();

                var aggregatedVolumes = AggregateVolumes(simulatedTrades);
                var csvContent = GenerateCsvContent(aggregatedVolumes);

                // Generate filename with current timestamp
                var extractTime = DateTime.Now;
                var fileName = GenerateFileName(extractTime);
                var filePath = Path.Combine(_config.OutputDirectory, fileName);

                // Ensure directory exists
                Directory.CreateDirectory(_config.OutputDirectory);

                // Write CSV file
                await File.WriteAllTextAsync(filePath, csvContent);

                _logger.LogInformation("Power position report generated successfully: {FilePath}", filePath);
                _logger.LogInformation("Total trades processed: {TradeCount}, Total periods aggregated: {PeriodCount}",
                    simulatedTrades.Count(), aggregatedVolumes.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating power position report");
                throw;
            }
        }

        private IEnumerable<PowerTrade> GenerateSimulatedTrades()
        {
            var tradeDate = DateTime.Today;
            var trades = new List<PowerTrade>();

            // Simulate 2 trades as shown in the challenge example
            var trade1 = PowerTrade.Create(tradeDate, 24);
            var trade2 = PowerTrade.Create(tradeDate, 24);

            // Trade 1: All periods have 100 volume
            for (int i = 0; i < 24; i++)
            {
                trade1.Periods[i].Volume = 100;
            }

            // Trade 2: First 10 periods have 50, remaining have -20
            for (int i = 0; i < 24; i++)
            {
                trade2.Periods[i].Volume = i < 10 ? 50 : -20;
            }

            trades.Add(trade1);
            trades.Add(trade2);

            _logger.LogInformation("Generated {Count} simulated trades for date {TradeDate}",
                trades.Count, tradeDate.ToString("yyyy-MM-dd"));

            return trades;
        }

        internal DateTime GetTradeDate(DateTime currentTime)
        {
            // Trading day starts at 23:00 previous day
            // If current time is 23:00 or later, we're in the NEXT day's trading period
            // If current time is before 23:00, we're in TODAY's trading period

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

            // Fix the sorting logic
            return aggregated
                .OrderBy(kvp =>
                {
                    // Parse the time string to get hours for proper sorting
                    var timeParts = kvp.Key.Split(':');
                    var hour = int.Parse(timeParts[0]);

                    // For proper 24-hour sorting, we want 23:00 first, then 00:00, 01:00, etc.
                    // Convert 23:00 to -1 so it sorts before 00:00
                    return hour == 23 ? -1 : hour;
                })
                .ThenBy(kvp => kvp.Key)
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
    }
}