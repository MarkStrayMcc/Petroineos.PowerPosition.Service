using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Petroineos.PowerPosition.Service.Interfaces
{
    public interface IHealthMonitor
    {
        void RecordSuccessfulRun();
    }
}
