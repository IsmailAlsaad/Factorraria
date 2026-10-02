using Factorraria.Common.Systems;

namespace Factorraria.Common.Machines
{
    public abstract class ElectricConsumerMachine : BaseMachine, IElectricConsumer
    {
        public abstract float PowerDemand { get; }
        public float useFraction { get; set; }
        public float GetUseFraction() => useFraction;
    }
    public abstract class ElectricProducerMachine : BaseMachine, IElectricProducer
    {
        public abstract float PowerSupply { get; }
        public float useFraction { get; set; }

        public float GetUseFraction() => useFraction;
    }
}
