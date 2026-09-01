using Factorraria.Common.Systems;

namespace Factorraria.Common.Machines
{
    public abstract class ElectricProducerMachine : BaseMachine
    {
        public abstract float PowerSupply { get; }

        public override void OnKill()
        {
            PowerGridSystem.AllMachines.Remove(this);
            PowerGridSystem.gridNeedsRebuilding = true;
        }
    }
}
