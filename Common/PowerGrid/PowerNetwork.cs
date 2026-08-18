using Factorraria.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Factorraria.Common.PowerGrid
{
    public class PowerNetwork
    {
        public List<IElectricConsumer> electricConsumers = new List<IElectricConsumer>();
        public List<IElectricProducer> electricProducers = new List<IElectricProducer>();

        public void Tick()
        {
            float totalSupply = 0f;
            float totalDemand = 0f;

            foreach (var consumer in electricConsumers)
            {
                if (consumer.isWorking)
                {
                    totalDemand += consumer.PowerDemand;
                }
            }

            foreach (var producer in electricProducers)
            {
                if (producer.isWorking)
                {
                    totalSupply += producer.PowerSupply;
                }
            }

            bool isGridStable = true;
            if (totalSupply < totalDemand) 
            {
                isGridStable = false;
            }

            foreach (var producer in electricProducers)
            {
                producer.isGenerating = isGridStable;
            }

            foreach (var consumer in electricConsumers)
            {
                consumer.isPowered = isGridStable;
            }
        }

    }
}
