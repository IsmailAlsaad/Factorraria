using Factorraria.Common.Machines;
using System;
using System.Collections.Generic;
using Terraria;

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
            float UseFraction = totalSupply > 0f ? Math.Clamp(1f - totalDemand / totalSupply, 0f, 1f) : 0f;
            if (totalSupply < totalDemand || totalSupply == 0) 
            {
                isGridStable = false;
            }

            // Readout for the machine UI hover text: the grid totals, and whether this machine shares a grid with any other machine.
            bool linked = electricConsumers.Count + electricProducers.Count > 1;

            foreach (var producer in electricProducers)
            {
                producer.isOn = isGridStable;
                producer.useFraction = UseFraction;
                PublishReadout(producer, totalDemand, totalSupply, linked);
            }

            foreach (var consumer in electricConsumers)
            {
                consumer.isOn = isGridStable;
                consumer.useFraction = isGridStable ? 1f : 0f;
                PublishReadout(consumer, totalDemand, totalSupply, linked);
            }
        }

        static void PublishReadout(object member, float demand, float supply, bool linked)
        {
            if (member is BaseMachine machine)
            {
                machine.PowerNetDemand = demand;
                machine.PowerNetSupply = supply;
                machine.PowerNetLinked = linked;
            }
        }

    }
}
