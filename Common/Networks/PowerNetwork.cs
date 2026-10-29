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

            // What the grid itself makes and asks for, before any battery. The hover readout shows these as "demand/supply".
            float baseDemand = totalDemand;
            float baseSupply = totalSupply;

            // The most the working batteries could give right now. A battery only discharges when the whole group can cover the whole
            // shortfall: a rescue that cannot finish would drain them without ever switching the grid on (a brownout freezes machines).
            // So a group that is too small waits and keeps its charge, and once one battery runs dry and the group falls below the load,
            // every battery stops again. Stateless on purpose: it is simply redone every tick.
            float dischargeCapacity = 0f;
            foreach (var producer in electricProducers)
            {
                if (producer is IElectricStorage capacityStorage && capacityStorage.isWorking)
                    dischargeCapacity += capacityStorage.AvailableDischarge();
            }

            bool batteriesCanLift = shortfall > storageEpsilon && dischargeCapacity + storageEpsilon >= shortfall;

            foreach (var producer in electricProducers)
            {
                if (producer is not IElectricStorage storage)
                    continue;

                float flow = 0f;
                if (storage.isWorking)
                {
                    if (batteriesCanLift)
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
                PublishReadout(producer, baseDemand, baseSupply, storageCharged, dischargeCapacity, linked);
            }

            foreach (var consumer in electricConsumers)
            {
                consumer.isOn = isGridStable;
                consumer.useFraction = isGridStable ? 1f : 0f;
                PublishReadout(consumer, baseDemand, baseSupply, storageCharged, dischargeCapacity, linked);
            }
        }

        // The surge power this member adds to the grid this tick; also counts its window down (Factorraria.Common.Cosmic.CosmicStrikeMath.Tick).
        static float TakeSurge(object member, bool countedAsConsumer)
        {
            if (countedAsConsumer || member is not BaseMachine machine || machine.SurgeTicksLeft <= 0)
                return 0f;

            return CosmicStrikeMath.Tick(ref machine.SurgePower, ref machine.SurgeTicksLeft);
        }

        // demand / supply are the grid's own numbers (no battery in them). charge = what the batteries take right now (only while some
        // battery really charges), dischargeCapacity = the most the working batteries could give (0 when there is none).
        static void PublishReadout(object member, float demand, float supply, float charge, float dischargeCapacity, bool linked)
        {
            if (member is BaseMachine machine)
            {
                machine.PowerNetDemand = demand;
                machine.PowerNetSupply = supply;
                machine.PowerNetCharge = charge;
                machine.PowerNetDischarge = dischargeCapacity;
                machine.PowerNetLinked = linked;
            }
        }

    }
}
