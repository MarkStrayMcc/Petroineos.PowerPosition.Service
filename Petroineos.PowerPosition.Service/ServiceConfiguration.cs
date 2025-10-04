using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Petroineos.PowerPosition.Service
{
    public class ServiceConfiguration
    {
        public string OutputDirectory { get; set; } = @"C:\PowerPositionReports";
        public int IntervalMinutes { get; set; } = 5;
        public int RetryCount { get; set; } = 3;
        public int RetryDelayMilliseconds { get; set; } = 1000;
    }
}
