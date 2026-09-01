using Factorraria.Common.Systems;

namespace Factorraria.Common.Machines
{
    public abstract class ElectricConsumerMachine : BaseMachine
    {
        public abstract float PowerDemand { get; }

        public override void OnKill()
        {
            PowerGridSystem.AllMachines.Remove(this);
            PowerGridSystem.gridNeedsRebuilding = true;
        }
    }
}
