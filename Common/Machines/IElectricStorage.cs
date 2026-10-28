namespace Factorraria.Common.Machines
{
    // A battery. It is also an IElectricProducer so it joins power grids, wire toggling, cosmic surges and the light sync for free,
    // but its PowerSupply is 0: PowerNetwork.Tick settles storages AFTER the normal supply / demand, so a battery only ever
    // charges from real surplus and discharges to cover a real deficit (never charges another battery, never feeds itself).
    // Powers are in grid units per second; energy is in "units" (a full discharge at 60/s empties 3000 units in 50 s).
    public interface IElectricStorage : IElectricProducer
    {
        float StoredEnergy { get; }
        float EnergyCapacity { get; }

        // The most power this battery can take from / give to the grid right now (already limited by its rate and its charge / free room).
        float AvailableCharge();
        float AvailableDischarge();

        // Called by PowerNetwork.Tick once per tick, server / single-player only. power > 0 = charging at that rate,
        // power < 0 = discharging at that rate, 0 = idle. The battery converts it to energy and updates its visual state.
        void ApplyFlow(float power);
    }
}