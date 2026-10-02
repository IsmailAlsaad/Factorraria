using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Factorraria.Common.Machines
{
    public interface IElectricConsumer 
    { 
        float PowerDemand { get; } 
        bool isWorking { get; } 
        bool isOn { get; set; } 
        float useFraction { get; set; }
        float GetUseFraction();
    }
    public interface IElectricProducer 
    { 
        float PowerSupply { get; } 
        bool isWorking { get; } 
        bool isOn { get; set; } 
        float useFraction { get; set; } 
        float GetUseFraction();
    }
}
