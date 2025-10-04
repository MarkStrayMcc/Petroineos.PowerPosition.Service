using Microsoft.Extensions.Logging;
using Moq;
using Services;

namespace Petroineos.PowerPosition.Service.Tests
{
    public class PowerServiceIntegrationTests
    {
        private readonly Mock<ILogger<PowerPositionWorker>> _loggerMock;
        private readonly ServiceConfiguration _config;
        private readonly PowerPositionWorker _worker;

        public PowerServiceIntegrationTests()
        {
            _loggerMock = new Mock<ILogger<PowerPositionWorker>>();
            _config = new ServiceConfiguration { OutputDirectory = "C:\\Test" };

            // Use the real PowerService for integration tests
            var powerService = new PowerService();
            _worker = new PowerPositionWorker(powerService, _loggerMock.Object, _config);
        }

        [Fact]
        public async Task GeneratePowerPositionAsync_WithRealPowerService_ShouldNotThrow()
        {
            // Arrange & Act
            var exception = await Record.ExceptionAsync(() => _worker.GeneratePowerPositionAsync());

            // Assert - should not throw for basic functionality
            Assert.Null(exception);
        }

        [Fact]
        public void PowerService_GetTrades_ShouldReturnData()
        {
            // Arrange
            var powerService = new PowerService();
            var testDate = DateTime.Today;

            // Act
            var trades = powerService.GetTrades(testDate);

            // Assert
            Assert.NotNull(trades);
            // Note: PowerService may throw exceptions randomly, so we can't assert much more
        }
    }
}