using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Petroineos.PowerPosition.Service.Health;
using Petroineos.PowerPosition.Service.Interfaces;
using Petroineos.PowerPosition.Service.Metrics;

namespace Petroineos.PowerPosition.Service.Tests
{
    public class PowerPositionBackgroundServiceTests
    {
        private readonly Mock<IPowerPositionWorker> _workerMock;
        private readonly ServiceConfiguration _config;
        private readonly Mock<ILogger<PowerPositionBackgroundService>> _loggerMock;
        private readonly Mock<IHealthMonitor> _healthMonitorMock;
        private readonly Mock<IMetricsService> _metricsMock;
        private readonly PowerPositionBackgroundService _service;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public PowerPositionBackgroundServiceTests()
        {
            _workerMock = new Mock<IPowerPositionWorker>();
            _config = new ServiceConfiguration
            {
                IntervalMinutes = 1, // Normal interval for most tests
                EnableFileCleanup = true,
                FileRetentionDays = 30,
                CleanupIntervalHours = 24
            };
            _loggerMock = new Mock<ILogger<PowerPositionBackgroundService>>();
            _healthMonitorMock = new Mock<IHealthMonitor>();
            _metricsMock = new Mock<IMetricsService>();
            _cancellationTokenSource = new CancellationTokenSource();

            _service = new PowerPositionBackgroundService(
                _workerMock.Object,
                _config,
                _loggerMock.Object,
                _healthMonitorMock.Object,
                _metricsMock.Object);
        }

        [Fact]
        public async Task StartAsync_ShouldRunInitialExtract()
        {
            // Arrange
            _workerMock.Setup(w => w.GeneratePowerPositionAsync())
                      .ReturnsAsync(5);

            // Act
            await _service.StartAsync(_cancellationTokenSource.Token);
            await Task.Delay(100);
            await _service.StopAsync(_cancellationTokenSource.Token);

            // Assert
            _workerMock.Verify(w => w.GeneratePowerPositionAsync(), Times.AtLeastOnce);
            _healthMonitorMock.Verify(h => h.RecordSuccessfulRun(), Times.AtLeastOnce);
            _metricsMock.Verify(m => m.RecordSuccessfulRun(5), Times.AtLeastOnce);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldRunInitialExtract_AndSetupTimers()
        {
            // Arrange
            _workerMock.Setup(w => w.GeneratePowerPositionAsync())
                      .ReturnsAsync(3);

            // Act
            await _service.StartAsync(_cancellationTokenSource.Token);
            await Task.Delay(100);
            await _service.StopAsync(_cancellationTokenSource.Token);

            // Assert - Should run initial extract and setup timers
            _workerMock.Verify(w => w.GeneratePowerPositionAsync(), Times.AtLeastOnce);

            // Verify timers were created (indirectly through service behavior)
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Scheduling extracts every")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task RunExtractAsync_ShouldRecordSuccess_WhenWorkerSucceeds()
        {
            // Arrange
            var tradesProcessed = 10;
            _workerMock.Setup(w => w.GeneratePowerPositionAsync())
                      .ReturnsAsync(tradesProcessed);

            // Act - Directly call the internal method
            await _service.RunExtractAsync();

            // Assert
            _workerMock.Verify(w => w.GeneratePowerPositionAsync(), Times.Once);
            _healthMonitorMock.Verify(h => h.RecordSuccessfulRun(), Times.Once);
            _metricsMock.Verify(m => m.RecordSuccessfulRun(tradesProcessed), Times.Once);
        }

        [Fact]
        public async Task RunExtractAsync_ShouldRecordFailure_WhenWorkerThrows()
        {
            // Arrange
            _workerMock.Setup(w => w.GeneratePowerPositionAsync())
                      .ThrowsAsync(new Exception("Worker failed"));

            // Act - Directly call the internal method
            await _service.RunExtractAsync();

            // Assert
            _metricsMock.Verify(m => m.RecordFailedRun(), Times.Once);
            _healthMonitorMock.Verify(h => h.RecordSuccessfulRun(), Times.Never);
        }

        [Fact]
        public async Task RunExtractAsync_ShouldNotThrow_WhenWorkerFails()
        {
            // Arrange
            _workerMock.Setup(w => w.GeneratePowerPositionAsync())
                      .ThrowsAsync(new Exception("Worker failed"));

            // Act & Assert - Should not throw (exceptions are caught internally)
            await _service.RunExtractAsync();

            // Verify the exception was logged
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error during scheduled power position extraction")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task RunCleanupIfDueAsync_ShouldCallWorkerCleanup_WhenFirstCalled()
        {
            // Arrange
            _workerMock.Setup(w => w.CleanupOldFiles(It.IsAny<TimeSpan>()));

            // Act - First call (should run cleanup)
            await _service.RunCleanupIfDueAsync();

            // Assert
            _workerMock.Verify(w => w.CleanupOldFiles(It.IsAny<TimeSpan>()), Times.Once);
        }

        [Fact]
        public async Task RunCleanupIfDueAsync_ShouldNotCallWorkerCleanup_WhenCalledShortlyAfter()
        {
            // Arrange
            _workerMock.Setup(w => w.CleanupOldFiles(It.IsAny<TimeSpan>()));

            // Act - First call (runs cleanup)
            await _service.RunCleanupIfDueAsync();

            // Second call shortly after (should NOT run cleanup)
            await _service.RunCleanupIfDueAsync();

            // Assert
            _workerMock.Verify(w => w.CleanupOldFiles(It.IsAny<TimeSpan>()), Times.Once);
        }

        [Fact]
        public async Task RunCleanupIfDueAsync_ShouldNotRun_WhenCleanupDisabled()
        {
            // Arrange
            var disabledConfig = new ServiceConfiguration
            {
                IntervalMinutes = 1,
                EnableFileCleanup = false, // Cleanup disabled
                FileRetentionDays = 30,
                CleanupIntervalHours = 24
            };

            var service = new PowerPositionBackgroundService(
                _workerMock.Object,
                disabledConfig,
                _loggerMock.Object,
                _healthMonitorMock.Object,
                _metricsMock.Object);

            _workerMock.Setup(w => w.CleanupOldFiles(It.IsAny<TimeSpan>()));

            // Act
            await service.RunCleanupIfDueAsync();

            // Assert
            _workerMock.Verify(w => w.CleanupOldFiles(It.IsAny<TimeSpan>()), Times.Never);
        }

        [Fact]
        public async Task RunCleanupIfDueAsync_ShouldHandleWorkerExceptions_Gracefully()
        {
            // Arrange
            _workerMock.Setup(w => w.CleanupOldFiles(It.IsAny<TimeSpan>()))
                      .Throws(new Exception("Cleanup failed"));

            // Act & Assert - Should not throw
            await _service.RunCleanupIfDueAsync();

            // Verify the exception was logged
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error during scheduled file cleanup")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task StopAsync_ShouldLogMetricsSummary()
        {
            // Arrange
            _workerMock.Setup(w => w.GeneratePowerPositionAsync())
                      .ReturnsAsync(4);

            // Act
            await _service.StartAsync(_cancellationTokenSource.Token);
            await Task.Delay(100);
            await _service.StopAsync(_cancellationTokenSource.Token);

            // Assert
            _metricsMock.Verify(m => m.LogMetricsSummary(), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldRespectCancellationToken()
        {
            // Arrange
            var quickCancelToken = new CancellationTokenSource();
            _workerMock.Setup(w => w.GeneratePowerPositionAsync())
                      .ReturnsAsync(1);

            // Act
            var task = _service.StartAsync(quickCancelToken.Token);
            quickCancelToken.CancelAfter(50);
            await Task.Delay(200);

            // Assert
            Assert.True(task.IsCompleted);
        }

        [Fact]
        public void Dispose_ShouldNotThrow_WhenCalledMultipleTimes()
        {
            // Arrange & Act & Assert
            var exception = Record.Exception(() =>
            {
                _service.Dispose();
                _service.Dispose(); // Second call should not throw
            });

            Assert.Null(exception);
        }

        [Fact]
        public async Task Service_Integration_StartStopCycle()
        {
            // Arrange
            var extractCount = 0;
            var cleanupCount = 0;

            _workerMock.Setup(w => w.GeneratePowerPositionAsync())
                      .ReturnsAsync(2)
                      .Callback(() => extractCount++);

            _workerMock.Setup(w => w.CleanupOldFiles(It.IsAny<TimeSpan>()))
                      .Callback(() => cleanupCount++);

            // Act - Full start/stop cycle
            await _service.StartAsync(_cancellationTokenSource.Token);
            await Task.Delay(300); // Let it run briefly
            await _service.StopAsync(_cancellationTokenSource.Token);

            // Assert - Extract should have run at least once
            Assert.True(extractCount >= 1, "Extract should have run at least once");
            // Cleanup might not run due to timing, so we don't assert on it
        }

        private void Dispose()
        {
            _cancellationTokenSource?.Dispose();
            _service?.Dispose();
        }
    }
}