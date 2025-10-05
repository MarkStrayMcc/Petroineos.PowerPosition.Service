using Microsoft.Extensions.Logging;
using Moq;
using Services;

namespace Petroineos.PowerPosition.Service.Tests
{
    public class PowerPositionWorkerTests
    {
        private readonly Mock<IPowerService> _powerServiceMock;
        private readonly Mock<ILogger<PowerPositionWorker>> _loggerMock;
        private readonly ServiceConfiguration _config;
        private readonly PowerPositionWorker _worker;

        public PowerPositionWorkerTests()
        {
            _powerServiceMock = new Mock<IPowerService>();
            _loggerMock = new Mock<ILogger<PowerPositionWorker>>();
            _config = new ServiceConfiguration
            {
                OutputDirectory = Path.Combine(Path.GetTempPath(), "PowerPositionTests"),
                IntervalMinutes = 5,
                RetryCount = 3,
                RetryDelayMilliseconds = 50
            };
            _worker = new PowerPositionWorker(_powerServiceMock.Object, _loggerMock.Object, _config);
        }

        [Fact]
        public async Task GeneratePowerPositionAsync_ShouldReturnTradeCount_WhenSuccessful()
        {
            var trades = new List<PowerTrade>
            {
                CreateTrade(new double[] { 100, 200 }),
                CreateTrade(new double[] { 50, 75 })
            };

            _powerServiceMock
                .Setup(ps => ps.GetTradesAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(trades);

            var result = await _worker.GeneratePowerPositionAsync();

            Assert.Equal(2, result);
            _powerServiceMock.Verify(ps => ps.GetTradesAsync(It.IsAny<DateTime>()), Times.Once);
        }

        [Fact]
        public async Task GeneratePowerPositionAsync_ShouldRetryAndSucceed_OnPowerServiceException()
        {
            var callCount = 0;
            _powerServiceMock
                .Setup(ps => ps.GetTradesAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(() =>
                {
                    callCount++;
                    if (callCount == 1)
                        throw new PowerServiceException("First attempt fails");
                    return new List<PowerTrade> { CreateTrade(new double[] { 100 }) };
                });

            var result = await _worker.GeneratePowerPositionAsync();

            Assert.Equal(1, result);
            Assert.Equal(2, callCount);
        }

        [Fact]
        public async Task GeneratePowerPositionAsync_ShouldReturnZeroAndGenerateErrorFile_OnNonPowerServiceException()
        {
            _powerServiceMock
                .Setup(ps => ps.GetTradesAsync(It.IsAny<DateTime>()))
                .ThrowsAsync(new InvalidOperationException("Non-retryable exception"));

            var result = await _worker.GeneratePowerPositionAsync();

            Assert.Equal(0, result);
            _powerServiceMock.Verify(ps => ps.GetTradesAsync(It.IsAny<DateTime>()), Times.Exactly(4));
        }

        [Fact]
        public async Task GeneratePowerPositionAsync_ShouldCreateFallbackErrorFile_AfterAllRetriesFail()
        {
            var testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _config.OutputDirectory = testDir;

            _powerServiceMock
                .Setup(ps => ps.GetTradesAsync(It.IsAny<DateTime>()))
                .ThrowsAsync(new PowerServiceException("Always fails"));

            try
            {
                var result = await _worker.GeneratePowerPositionAsync();

                Assert.Equal(0, result);
                var file = Directory.GetFiles(testDir, "*_ERROR.csv").SingleOrDefault();
                Assert.NotNull(file);

                var content = await File.ReadAllTextAsync(file);
                Assert.Contains("# ERROR:", content);
                Assert.Contains("PowerServiceException", content);
            }
            finally
            {
                if (Directory.Exists(testDir))
                    Directory.Delete(testDir, true);
            }
        }

        [Fact]
        public async Task GeneratePowerPositionAsync_ShouldWriteCsvWithErrorPlaceholders_OnServiceFailure()
        {
            var testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _config.OutputDirectory = testDir;

            _powerServiceMock
                .Setup(ps => ps.GetTradesAsync(It.IsAny<DateTime>()))
                .ThrowsAsync(new PowerServiceException("Simulated failure"));

            try
            {
                await _worker.GeneratePowerPositionAsync();

                var file = Directory.GetFiles(testDir, "*_ERROR.csv").Single();
                var csv = await File.ReadAllTextAsync(file);

                Assert.Contains("# ERROR: PowerServiceException", csv);
                Assert.Contains("ERROR", csv);
            }
            finally
            {
                if (Directory.Exists(testDir))
                    Directory.Delete(testDir, true);
            }
        }

        [Fact]
        public async Task GeneratePowerPositionAsync_ShouldGenerateEmptyVolumesFile_WhenNoTradesReturned()
        {
            _powerServiceMock
                .Setup(ps => ps.GetTradesAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(Enumerable.Empty<PowerTrade>());

            var result = await _worker.GeneratePowerPositionAsync();

            Assert.Equal(0, result);
        }

        [Fact]
        public async Task GeneratePowerPositionAsync_ShouldUseExponentialBackoff_ForRetries()
        {
            var callCount = 0;
            _powerServiceMock
                .Setup(ps => ps.GetTradesAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(() =>
                {
                    callCount++;
                    if (callCount <= 2)
                        throw new PowerServiceException($"Attempt {callCount} fails");
                    return new List<PowerTrade> { CreateTrade(new double[] { 100 }) };
                });

            var result = await _worker.GeneratePowerPositionAsync();

            Assert.Equal(1, result);
            Assert.Equal(3, callCount);
        }

        [Fact]
        public void CleanupOldFiles_ShouldDeleteFiles_OlderThanRetentionPeriod()
        {
            var testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _config.OutputDirectory = testDir;

            try
            {
                Directory.CreateDirectory(testDir);

                var oldFile = Path.Combine(testDir, "PowerPosition_20200101_1200.csv");
                var newFile = Path.Combine(testDir, "PowerPosition_20240101_1200.csv");

                File.WriteAllText(oldFile, "test");
                File.WriteAllText(newFile, "test");
                File.SetCreationTime(oldFile, DateTime.Now.AddDays(-35));

                _worker.CleanupOldFiles(TimeSpan.FromDays(30));

                Assert.False(File.Exists(oldFile));
                Assert.True(File.Exists(newFile));
            }
            finally
            {
                if (Directory.Exists(testDir))
                    Directory.Delete(testDir, true);
            }
        }

        [Fact]
        public void CleanupOldFiles_ShouldHandleMissingDirectory_Gracefully()
        {
            _config.OutputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "NonExistent");
            var ex = Record.Exception(() => _worker.CleanupOldFiles(TimeSpan.FromDays(30)));
            Assert.Null(ex);
        }

        [Fact]
        public void CleanupOldFiles_ShouldLog_WhenFilesAreDeleted()
        {
            var testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _config.OutputDirectory = testDir;

            try
            {
                Directory.CreateDirectory(testDir);
                var oldFile = Path.Combine(testDir, "PowerPosition_20200101_1200.csv");
                File.WriteAllText(oldFile, "test");
                File.SetCreationTime(oldFile, DateTime.Now.AddDays(-35));

                _worker.CleanupOldFiles(TimeSpan.FromDays(30));

                _loggerMock.Verify(
                    x => x.Log(
                        LogLevel.Information,
                        It.IsAny<EventId>(),
                        It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("File cleanup completed")),
                        It.IsAny<Exception>(),
                        It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                    Times.Once);
            }
            finally
            {
                if (Directory.Exists(testDir))
                    Directory.Delete(testDir, true);
            }
        }

        [Theory]
        [InlineData(22, 59, "2024-01-15")]
        [InlineData(23, 0, "2024-01-16")]
        [InlineData(23, 30, "2024-01-16")]
        [InlineData(0, 0, "2024-01-15")]
        [InlineData(10, 0, "2024-01-15")]
        public void GetTradeDate_ShouldHandleLondonTradingDay(int hour, int minute, string expectedDate)
        {
            var currentTime = new DateTime(2024, 1, 15, hour, minute, 0);
            var result = _worker.GetTradeDate(currentTime);
            Assert.Equal(expectedDate, result.ToString("yyyy-MM-dd"));
        }

        [Fact]
        public void AggregateVolumes_ShouldSumVolumes_FromMultipleTrades()
        {
            var trades = new List<PowerTrade>
            {
                CreateTrade(new double[] { 100, 200, 150 }),
                CreateTrade(new double[] { 50, 75, 100 })
            };

            var result = _worker.AggregateVolumes(trades);

            Assert.Equal(3, result.Count);
            Assert.Equal(150, result["23:00"]);
            Assert.Equal(275, result["00:00"]);
            Assert.Equal(250, result["01:00"]);
        }

        [Fact]
        public void AggregateVolumes_ShouldReturnEmpty_ForNoTrades()
        {
            var result = _worker.AggregateVolumes(Enumerable.Empty<PowerTrade>());
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void AggregateVolumes_ShouldSortTimes_With23First()
        {
            var trades = new List<PowerTrade>
            {
                CreateTrade(new double[] { 100, 200, 300 })
            };

            var result = _worker.AggregateVolumes(trades);
            var keys = result.Keys.ToList();

            Assert.Equal("23:00", keys[0]);
            Assert.Equal("00:00", keys[1]);
            Assert.Equal("01:00", keys[2]);
        }

        [Theory]
        [InlineData(1, "23:00")]
        [InlineData(2, "00:00")]
        [InlineData(3, "01:00")]
        [InlineData(12, "10:00")]
        [InlineData(24, "22:00")]
        public void PeriodToTimeString_ShouldConvertPeriods_ToCorrectTimeStrings(int period, string expectedTime)
        {
            var result = _worker.PeriodToTimeString(period);
            Assert.Equal(expectedTime, result);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(25)]
        [InlineData(-1)]
        public void PeriodToTimeString_ShouldHandle_InvalidPeriods(int invalidPeriod)
        {
            var result = _worker.PeriodToTimeString(invalidPeriod);
            Assert.NotNull(result);
        }

        private PowerTrade CreateTrade(double[] volumes)
        {
            var trade = PowerTrade.Create(DateTime.Today, volumes.Length);
            for (int i = 0; i < volumes.Length; i++)
                trade.Periods[i].Volume = volumes[i];
            return trade;
        }
    }
}
