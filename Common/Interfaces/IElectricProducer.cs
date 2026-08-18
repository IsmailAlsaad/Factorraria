using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Factorraria.Common.Interfaces
{
    public interface IElectricProducer
    {
        float PowerSupply { get; }

        bool isGenerating { get; set; }
        bool isWorking { get; set; }
    }
}
