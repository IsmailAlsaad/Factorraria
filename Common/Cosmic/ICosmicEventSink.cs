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

    // C6: how a ray's path ends, for the picture only (the server already decided what happens).
    public enum CosmicRayEndKind : byte
    {
        None = 0,     // it flew its whole range or hit a lead brick
        Absorb = 1,   // a gemspark of its own color
        Ionize = 2    // a cloud block
    }

    // A ray was traced. Length = tiles it flies (to the tile it enters, or the full range). Flying at ParticleSpeed from Tick on.
    public struct CosmicRayEvent
    {
        public long Tick;
        public CosmicColor Color;
        public float StartX, StartY, DirX, DirY, Length;
        public CosmicRayEndKind End;
        public float SpeedMul;   // this ray flies at ParticleSpeed x SpeedMul (the simulation always sets it; 0 = treat as 1)
    }

    // A gemspark absorbed a particle (the two child rays have their own Ray events).
    public struct CosmicAbsorbEvent
    {
        public long Tick;
        public CosmicColor Color;
        public int TileX, TileY;
    }

    // A hit ionized these cloud blocks for LifeTicks. Tiles is only valid during the call (the sink copies what it needs).
    public struct CosmicIonizeEvent
    {
        public long Tick;
        public CosmicColor Color;
        public int LifeTicks;
        public System.Collections.Generic.IReadOnlyList<(int x, int y)> Tiles;
    }

    // A whole connected cluster discharged. Tiles is only valid during the call. A Strike or Spit event follows it.
    public struct CosmicDischargeEvent
    {
        public long Tick;
        public float CenterX, CenterY;
        public double Energy;
        public System.Collections.Generic.IReadOnlyList<(int x, int y)> Tiles;
    }

    public interface ICosmicEventSink
    {
        void Ray(in CosmicRayEvent e);
        void Absorb(in CosmicAbsorbEvent e);
        void Ionize(in CosmicIonizeEvent e);
        void Discharge(in CosmicDischargeEvent e);
        void Strike(in CosmicStrikeEvent e);
        void Spit(in CosmicSpitEvent e);

        // Called once at the end of every simulation tick: a network sink sends what it collected during the tick.
        void EndTick();
    }

    // The sink until C6: events are dropped (the counters in CosmicSimulation still count them).
    public sealed class CosmicNullSink : ICosmicEventSink
    {
        public void Ray(in CosmicRayEvent e) { }
        public void Absorb(in CosmicAbsorbEvent e) { }
        public void Ionize(in CosmicIonizeEvent e) { }
        public void Discharge(in CosmicDischargeEvent e) { }
        public void Strike(in CosmicStrikeEvent e) { }
        public void Spit(in CosmicSpitEvent e) { }
        public void EndTick() { }
    }
}