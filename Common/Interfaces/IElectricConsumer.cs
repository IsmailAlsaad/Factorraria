using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Factorraria.Common.Interfaces
{
    public interface IElectricConsumer
    {
        float PowerDemand { get; }

        bool isPowered { get; set; }
        bool isWorking { get; set; }
    }
}
