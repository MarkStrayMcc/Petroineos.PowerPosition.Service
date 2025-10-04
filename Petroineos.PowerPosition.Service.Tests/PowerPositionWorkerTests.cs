using Microsoft.Extensions.Logging;
using Moq;
using Services;

namespace Petroineos.PowerPosition.Service.Tests
{
    public class PowerPositionWorkerTests
    {
        private readonly Mock<ILogger<PowerPositionWorker>> _loggerMock;
        private readonly Mock<IPowerService> _powerServiceMock;
        private readonly ServiceConfiguration _config;
        private readonly PowerPositionWorker _worker;

        public PowerPositionWorkerTests()
        {
            _loggerMock = new Mock<ILogger<PowerPositionWorker>>();
            _powerServiceMock = new Mock<IPowerService>();
            _config = new ServiceConfiguration { OutputDirectory = "C:\\Test" };
            _worker = new PowerPositionWorker(_powerServiceMock.Object, _loggerMock.Object, _config);
        }

        [Theory]
        [InlineData(1, "23:00")]
        [InlineData(2, "00:00")]
        [InlineData(3, "01:00")]
        [InlineData(12, "10:00")]
        [InlineData(24, "22:00")]
        public void PeriodToTimeString_ShouldConvertPeriodsToCorrectTimeStrings(int period, string expectedTime)
        {
            // Act
            var result = _worker.PeriodToTimeString(period);

            // Assert
            Assert.Equal(expectedTime, result);
        }

        [Fact]
        public async Task GeneratePowerPositionAsync_WithPowerServiceException_ShouldRetry()
        {
            // Arrange
            _powerServiceMock.Setup(ps => ps.GetTradesAsync(It.IsAny<DateTime>()))
                           .ThrowsAsync(new PowerServiceException("Test exception"));

            var worker = new PowerPositionWorker(_powerServiceMock.Object, _loggerMock.Object, _config);

            // Act & Assert
            await Assert.ThrowsAsync<PowerServiceException>(() => worker.GeneratePowerPositionAsync());

            // Verify retry attempts
            _powerServiceMock.Verify(ps => ps.GetTradesAsync(It.IsAny<DateTime>()),
                Times.Exactly(_config.RetryCount));
        }

        [Fact]
        public async Task GeneratePowerPositionAsync_WithSuccessAfterRetry_ShouldComplete()
        {
            // Arrange
            var callCount = 0;
            _powerServiceMock.Setup(ps => ps.GetTradesAsync(It.IsAny<DateTime>()))
                           .ReturnsAsync(() =>
                           {
                               callCount++;
                               if (callCount == 1)
                                   throw new PowerServiceException("First attempt fails");
                               return new List<PowerTrade> { CreateTrade(new[] { 100.0 }) };
                           });

            var worker = new PowerPositionWorker(_powerServiceMock.Object, _loggerMock.Object, _config);

            // Act
            await worker.GeneratePowerPositionAsync();

            // Assert - should succeed on second attempt
            Assert.Equal(2, callCount);
        }

        [Fact]
        public void AggregateVolumes_ShouldSumVolumesFromMultipleTrades()
        {
            // Arrange
            var trades = new List<PowerTrade>
            {
                CreateTrade(new double[] { 100, 100, 100 }), // 3 periods
                CreateTrade(new double[] { 50, 50, 75 })     // 3 periods
            };

            // Act
            var result = _worker.AggregateVolumes(trades);

            // Assert
            Assert.Equal(3, result.Count);
            Assert.Equal(150, result["23:00"]); // 100 + 50
            Assert.Equal(150, result["00:00"]); // 100 + 50  
            Assert.Equal(175, result["01:00"]); // 100 + 75
        }

        [Fact]
        public void AggregateVolumes_ShouldSortTimesWith23First()
        {
            // Arrange
            var trades = new List<PowerTrade>
            {
                CreateTrade(new double[] { 100, 200, 300 }) // periods 1, 2, 3
            };

            // Act
            var result = _worker.AggregateVolumes(trades);

            // Assert - verify order is 23:00, 00:00, 01:00
            var keys = result.Keys.ToList();
            Assert.Equal("23:00", keys[0]);
            Assert.Equal("00:00", keys[1]);
            Assert.Equal("01:00", keys[2]);
        }

        [Fact]
        public void GenerateCsvContent_ShouldCreateCorrectFormat()
        {
            // Arrange
            var volumes = new Dictionary<string, double>
            {
                ["23:00"] = 150.5,
                ["00:00"] = 200.0
            };

            // Act
            var result = _worker.GenerateCsvContent(volumes);

            // Assert
            Assert.Contains("Local Time,Volume", result);
            Assert.Contains("23:00,150.5", result);
            Assert.Contains("00:00,200.0", result);
            Assert.Contains(Environment.NewLine, result);
        }

        [Fact]
        public void GenerateCsvContent_ShouldHaveHeaderAndDataRows()
        {
            // Arrange
            var volumes = new Dictionary<string, double>
            {
                ["23:00"] = 150.5
            };

            // Act
            var result = _worker.GenerateCsvContent(volumes);

            // Assert
            var lines = result.Split(Environment.NewLine);
            Assert.Equal(2, lines.Length); // Header + 1 data row
            Assert.Equal("Local Time,Volume", lines[0]);
            Assert.Equal("23:00,150.5", lines[1]);
        }

        [Fact]
        public void GenerateFileName_ShouldFollowNamingConvention()
        {
            // Arrange
            var testDate = new DateTime(2024, 1, 15, 14, 30, 0);

            // Act
            var result = _worker.GenerateFileName(testDate);

            // Assert
            Assert.Equal("PowerPosition_20240115_1430.csv", result);
        }

        [Theory]
        [InlineData(22, 59, "2024-01-15")]  // Before 23:00 → current day (trading day started yesterday at 23:00)
        [InlineData(23, 0, "2024-01-16")]   // At 23:00 → next day (start of new trading day)
        [InlineData(23, 30, "2024-01-16")]  // After 23:00 → next day
        [InlineData(0, 0, "2024-01-15")]    // Midnight → current day (still in trading day that started yesterday at 23:00)
        [InlineData(10, 0, "2024-01-15")]   // Morning → current day
        [InlineData(15, 30, "2024-01-15")]  // Afternoon → current day
        public void GetTradeDate_ShouldHandleLondonTradingDay(int hour, int minute, string expectedDate)
        {
            // Arrange
            var currentTime = new DateTime(2024, 1, 15, hour, minute, 0);

            // Act
            var result = _worker.GetTradeDate(currentTime);

            // Assert
            Assert.Equal(expectedDate, result.ToString("yyyy-MM-dd"));
        }

        [Fact]
        public void AggregateVolumes_WithEmptyTrades_ShouldReturnEmptyDictionary()
        {
            // Arrange
            var emptyTrades = Enumerable.Empty<PowerTrade>();

            // Act
            var result = _worker.AggregateVolumes(emptyTrades);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void AggregateVolumes_WithSingleTrade_ShouldReturnCorrectVolumes()
        {
            // Arrange
            var trades = new List<PowerTrade>
            {
                CreateTrade(new double[] { 100, 200 }) // 2 periods
            };

            // Act
            var result = _worker.AggregateVolumes(trades);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal(100, result["23:00"]);
            Assert.Equal(200, result["00:00"]);
        }

        private PowerTrade CreateTrade(double[] volumes)
        {
            var trade = PowerTrade.Create(DateTime.Today, volumes.Length);
            for (int i = 0; i < volumes.Length; i++)
            {
                trade.Periods[i].Volume = volumes[i];
            }
            return trade;
        }
    }
}