using Factorraria.Common.Machines;
using System.Collections.Generic;

namespace Factorraria.Common.PowerGrid
{
    public class PowerNetwork
    {
        public List<ElectricConsumerMachine> electricConsumers = new List<ElectricConsumerMachine>();
        public List<ElectricProducerMachine> electricProducers = new List<ElectricProducerMachine>();

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
                producer.isOn = isGridStable;
            }

            foreach (var consumer in electricConsumers)
            {
                consumer.isOn = isGridStable;
            }
        }

    }
}
