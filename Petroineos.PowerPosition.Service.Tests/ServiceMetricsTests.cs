using Microsoft.Extensions.Logging;
using Moq;
using Petroineos.PowerPosition.Service.Metrics;

namespace Petroineos.PowerPosition.Service.Tests
{
    public class ServiceMetricsTests
    {
        private readonly Mock<ILogger<ServiceMetrics>> _loggerMock;
        private readonly ServiceConfiguration _config;
        private readonly ServiceMetrics _metrics;

        public ServiceMetricsTests()
        {
            _loggerMock = new Mock<ILogger<ServiceMetrics>>();
            _config = new ServiceConfiguration { EnableDetailedLogging = true };
            _metrics = new ServiceMetrics(_loggerMock.Object, _config);
        }

        [Fact]
        public void RecordSuccessfulRun_ShouldIncrementCounter()
        {
            // Act
            _metrics.RecordSuccessfulRun(5);
            _metrics.RecordSuccessfulRun(3);

            // Assert - Verify through logging (check for numbers without exact formatting)
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) =>
                        v.ToString()!.Contains("Successful runs: 2") &&
                        v.ToString()!.Contains("Total trades: 8")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public void RecordFailedRun_ShouldIncrementCounter()
        {
            // Act
            _metrics.RecordSuccessfulRun(2);
            _metrics.RecordFailedRun();
            _metrics.RecordFailedRun();

            // Assert - Check for numbers without worrying about percentage formatting
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) =>
                        v.ToString()!.Contains("Failed runs: 2") &&
                        v.ToString()!.Contains("Success rate: 33.33")), // Check for the number, not exact formatting
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public void LogMetricsSummary_ShouldLogAllMetrics()
        {
            // Arrange
            _metrics.RecordSuccessfulRun(10);
            _metrics.RecordFailedRun();

            // Act
            _metrics.LogMetricsSummary();

            // Assert - Check for key metrics without exact formatting
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) =>
                        v.ToString()!.Contains("Service Metrics Summary") &&
                        v.ToString()!.Contains("Successful runs: 1") &&
                        v.ToString()!.Contains("Failed runs: 1") &&
                        v.ToString()!.Contains("Total trades processed: 10") &&
                        v.ToString()!.Contains("Success rate:") &&
                        v.ToString()!.Contains("50.00")), // Check for the number
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void GetSuccessRate_WithNoRuns_ShouldReturn100Percent()
        {
            // Act
            _metrics.LogMetricsSummary();

            // Assert - Check for 100% success rate with no runs
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) =>
                        v.ToString()!.Contains("Service Metrics Summary") &&
                        v.ToString()!.Contains("Successful runs: 0") &&
                        v.ToString()!.Contains("Failed runs: 0") &&
                        v.ToString()!.Contains("Total trades processed: 0") &&
                        v.ToString()!.Contains("Success rate:") &&
                        v.ToString()!.Contains("100.00")), // Check for the number
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void RecordFailedRun_WithNoSuccessfulRuns_ShouldLogZeroSuccessRate()
        {
            // Act
            _metrics.RecordFailedRun();
            _metrics.RecordFailedRun();

            // Assert - 0% success rate when only failures
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) =>
                        v.ToString()!.Contains("Failed runs: 2") &&
                        v.ToString()!.Contains("Success rate: 0.00")), // 0% success rate
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }
    }
}