namespace Factorraria.Common.Cosmic
{
    // Roadmap section 4: server logic never calls draw code. It hands finished events to a sink, and the sink is the only part that
    // differs between single-player (a local sink that feeds the client draw) and multiplayer (a network sink). Positions are in tile units.
    // C5 has the Strike and Spit events and a sink that drops them; C6 adds the rest (rays, absorb, ionize, discharge) and the real sinks.

    // Lightning hit a machine. From = the thunder's center, To = the machine's center.
    public struct CosmicStrikeEvent
    {
        public long Tick;
        public float FromX, FromY, ToX, ToY;
        public double Energy;
        public float SurgePower;   // what was added to the machine after clamping
    }

    // Thunder with no machine in range: it goes off in a random direction (Dir is a unit vector) and gives no power.
    public struct CosmicSpitEvent
    {
        public long Tick;
        public float FromX, FromY, DirX, DirY;
        public double Energy;
    }

    public interface ICosmicEventSink
    {
        void Strike(in CosmicStrikeEvent e);
        void Spit(in CosmicSpitEvent e);
    }

    // The sink until C6: events are dropped (the counters in CosmicSimulation still count them).
    public sealed class CosmicNullSink : ICosmicEventSink
    {
        public void Strike(in CosmicStrikeEvent e) { }
        public void Spit(in CosmicSpitEvent e) { }
    }
}