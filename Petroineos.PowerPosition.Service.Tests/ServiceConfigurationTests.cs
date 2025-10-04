namespace Petroineos.PowerPosition.Service.Tests
{
    public class ServiceConfigurationTests
    {
        [Fact]
        public void ServiceConfiguration_ShouldHaveReasonableDefaults()
        {
            // Arrange & Act
            var config = new ServiceConfiguration();

            // Assert
            Assert.NotNull(config.OutputDirectory);
            Assert.False(string.IsNullOrEmpty(config.OutputDirectory));
            Assert.True(config.IntervalMinutes > 0);
            Assert.True(config.RetryCount > 0);
            Assert.True(config.RetryDelayMilliseconds > 0);
        }

        [Fact]
        public void ServiceConfiguration_DefaultValues_ShouldMatchExpected()
        {
            // Arrange & Act
            var config = new ServiceConfiguration();

            // Assert
            Assert.Equal(@"C:\PowerPositionReports", config.OutputDirectory);
            Assert.Equal(5, config.IntervalMinutes);
            Assert.Equal(3, config.RetryCount);
            Assert.Equal(1000, config.RetryDelayMilliseconds);
        }
    }
}