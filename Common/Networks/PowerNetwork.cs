using Factorraria.Common.Cosmic;
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

            // Cosmic radiation (patch C5): a lightning surge on any member adds to the supply for its few ticks (A8). The grid rule below is unchanged.
            // A machine that is both a consumer and a producer sits in both lists, so only the consumer pass counts it.
            foreach (var consumer in electricConsumers)
            {
                totalSupply += TakeSurge(consumer, false);
            }

            foreach (var producer in electricProducers)
            {
                totalSupply += TakeSurge(producer, producer is IElectricConsumer);
            }

            // Batteries (IElectricStorage) are settled after the normal supply / demand. Deficit: they discharge to cover it (up to their rate).
            // Surplus: they charge from it (up to their rate). A battery switched off by a wire (isWorking false) does neither.
            // The totals below then include the battery flow, so the stability check, the use fraction and the UI readout all see it.
            float storageEpsilon = 0.0001f;
            float shortfall = totalDemand - totalSupply;
            float surplus = totalSupply - totalDemand;
            float storageCharged = 0f;
            float storageDischarged = 0f;

            foreach (var producer in electricProducers)
            {
                if (producer is not IElectricStorage storage)
                    continue;

                float flow = 0f;
                if (storage.isWorking)
                {
                    if (shortfall > storageEpsilon)
                    {
                        flow = -Math.Min(storage.AvailableDischarge(), shortfall);
                        shortfall += flow;
                        storageDischarged -= flow;
                    }
                    else if (surplus > storageEpsilon)
                    {
                        flow = Math.Min(storage.AvailableCharge(), surplus);
                        surplus -= flow;
                        storageCharged += flow;
                    }
                }

                storage.ApplyFlow(flow);
            }

            totalSupply += storageDischarged;
            if (shortfall <= storageEpsilon && storageDischarged > 0f)
                totalSupply = Math.Max(totalSupply, totalDemand);   // the battery covered the whole deficit: absorb float rounding

            if (storageCharged > 0f)
                totalDemand = Math.Min(totalDemand + storageCharged, totalSupply);   // charge never exceeds the surplus: absorb float rounding

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

        // The surge power this member adds to the grid this tick; also counts its window down (Factorraria.Common.Cosmic.CosmicStrikeMath.Tick).
        static float TakeSurge(object member, bool countedAsConsumer)
        {
            if (countedAsConsumer || member is not BaseMachine machine || machine.SurgeTicksLeft <= 0)
                return 0f;

            return CosmicStrikeMath.Tick(ref machine.SurgePower, ref machine.SurgeTicksLeft);
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
