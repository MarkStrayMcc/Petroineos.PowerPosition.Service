using Microsoft.Extensions.Logging;

namespace Petroineos.PowerPosition.Service
{
    public class PowerPositionWorker
    {
        private readonly ILogger<PowerPositionWorker> _logger;

        public PowerPositionWorker(ILogger<PowerPositionWorker> logger)
        {
            _logger = logger;
        }

        public Task GeneratePowerPositionAsync()
        {
            _logger.LogInformation("Power position generation - TODO: Implement business logic");
            return Task.CompletedTask;
        }
    }
}