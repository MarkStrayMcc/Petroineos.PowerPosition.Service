using Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Petroineos.PowerPosition.Service.Interfaces
{
    public interface IPowerPositionWorker
    {
        Task<int> GeneratePowerPositionAsync();
        void CleanupOldFiles(TimeSpan retentionPeriod);

        // Optional: expose these for testing if needed
        DateTime GetTradeDate(DateTime currentTime);
        Dictionary<string, double> AggregateVolumes(IEnumerable<PowerTrade> trades);
        string PeriodToTimeString(int period);
    }
}
