using Factorraria.Common.Cosmic;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;

namespace Factorraria.Common.Net
{
    // One event waiting for the end of the tick (what CosmicEventRouter collects and CosmicWire writes).
    public struct CosmicPendingEvent
    {
        public byte Kind;
        public byte Color;
        public byte Tier;
        public ushort Life;
        public float AX, AY, BX, BY;
        public int TileStart, TileCount;   // x, y pairs in the router's tile buffer
    }

    // The sink the cosmic simulation sends its events to (roadmap section 4). It is the ONLY place that differs between the sides:
    //   - single-player, and a host that also plays: the events feed the local CosmicParticleSystem directly (no packet);
    //   - dedicated server, and the remote players of a host: the events are collected during the tick and sent at the end of it
    //     (EndTick) as ONE CosmicRays and ONE CosmicEvent packet per player, filtered to the players that are near them.
    // The simulation never calls draw code and never waits for the network. Everything here is cosmetic: a lost or late packet changes
    // nothing but what a player sees. Authority stays on the server.
    public sealed class CosmicEventRouter : ICosmicEventSink
    {
        // What /fcosmic stats prints.
        public long PacketsSent, RaysSent, EventsSent, RaysDropped, EventsDropped, LocalRays, LocalEvents;

        readonly List<CosmicRayEvent> rays = new List<CosmicRayEvent>();
        readonly List<CosmicPendingEvent> events = new List<CosmicPendingEvent>();
        readonly List<short> tileBuf = new List<short>();
        readonly List<int> pickedRays = new List<int>();
        readonly List<int> pickedEvents = new List<int>();

        // Set at the end of each tick: is there anybody to send to next tick? An empty server collects nothing.
        bool remoteActive;

        // Cosmetic limits on what one tick may collect (the per-player limits are the NetMax... tunables).
        public int MaxPendingRays = 4096;
        public int MaxPendingEvents = 2048;
        public int MaxTilesPerEvent = 256;

        static bool Local => !Main.dedServ && Main.netMode != NetmodeID.MultiplayerClient;

        public string StatsText()
        {
            return "sent " + PacketsSent + " packets (" + RaysSent + " rays, " + EventsSent + " events), dropped by the per-tick limits " + RaysDropped + " rays and " + EventsDropped
                + " events, fed to the local screen " + LocalRays + " rays and " + LocalEvents + " events; players to send to: " + (remoteActive ? "yes" : "none");
        }

        public void Clear()
        {
            rays.Clear();
            events.Clear();
            tileBuf.Clear();
            remoteActive = false;
        }

        // ------------------------------------------------------------ the sink
        public void Ray(in CosmicRayEvent e)
        {
            if (Local)
            {
                LocalRays++;
                CosmicParticleSystem.Instance.AddRay(e.Color, (byte)e.End, e.StartX, e.StartY, e.DirX, e.DirY, e.Length, CosmicRadiationSystem.Instance.ParticleSpeed);
            }

            if (!remoteActive)
                return;

            if (rays.Count < MaxPendingRays)
                rays.Add(e);
            else
                RaysDropped++;
        }

        public void Absorb(in CosmicAbsorbEvent e)
        {
            if (Local)
            {
                LocalEvents++;
                CosmicParticleSystem.Instance.AddAbsorb(e.Color, e.TileX, e.TileY);
            }

            Queue(new CosmicPendingEvent { Kind = CosmicEventKind.Absorb, Color = (byte)e.Color, AX = e.TileX, AY = e.TileY });
        }

        public void Ionize(in CosmicIonizeEvent e)
        {
            if (Local)
            {
                LocalEvents++;
                CosmicParticleSystem.Instance.AddIonize(e.Color, e.LifeTicks, e.Tiles);
            }

            if (!remoteActive || e.Tiles == null || e.Tiles.Count == 0)
                return;

            CosmicPendingEvent p = new CosmicPendingEvent { Kind = CosmicEventKind.Ionize, Color = (byte)e.Color, Life = (ushort)Math.Max(1, Math.Min(ushort.MaxValue, e.LifeTicks)), AX = e.Tiles[0].x, AY = e.Tiles[0].y };
            AddTiles(ref p, e.Tiles);
            Queue(p);
        }

        public void Discharge(in CosmicDischargeEvent e)
        {
            if (Local)
            {
                LocalEvents++;
                CosmicParticleSystem.Instance.AddDischarge(e.Tiles);
            }

            if (!remoteActive || e.Tiles == null)
                return;

            CosmicPendingEvent p = new CosmicPendingEvent { Kind = CosmicEventKind.Discharge, Tier = CosmicWire.EnergyTier(e.Energy), AX = e.CenterX, AY = e.CenterY };
            AddTiles(ref p, e.Tiles);
            Queue(p);
        }

        public void Strike(in CosmicStrikeEvent e)
        {
            byte tier = CosmicWire.EnergyTier(e.Energy);

            if (Local)
            {
                LocalEvents++;
                CosmicParticleSystem.Instance.AddStrike(e.FromX, e.FromY, e.ToX, e.ToY, tier);
            }

            Queue(new CosmicPendingEvent { Kind = CosmicEventKind.Strike, Tier = tier, AX = e.FromX, AY = e.FromY, BX = e.ToX, BY = e.ToY });
        }

        public void Spit(in CosmicSpitEvent e)
        {
            byte tier = CosmicWire.EnergyTier(e.Energy);

            if (Local)
            {
                LocalEvents++;
                CosmicParticleSystem.Instance.AddSpit(e.FromX, e.FromY, e.DirX, e.DirY, tier);
            }

            Queue(new CosmicPendingEvent { Kind = CosmicEventKind.Spit, Tier = tier, AX = e.FromX, AY = e.FromY, BX = e.DirX, BY = e.DirY });
        }

        void Queue(CosmicPendingEvent p)
        {
            if (!remoteActive)
                return;

            if (events.Count < MaxPendingEvents)
                events.Add(p);
            else
                EventsDropped++;
        }

        void AddTiles(ref CosmicPendingEvent p, IReadOnlyList<(int x, int y)> tiles)
        {
            p.TileStart = tileBuf.Count;
            int count = Math.Min(tiles.Count, Math.Max(1, MaxTilesPerEvent));
            for (int i = 0; i < count; i++)
            {
                tileBuf.Add((short)tiles[i].x);
                tileBuf.Add((short)tiles[i].y);
            }
            p.TileCount = count;
        }

        // ------------------------------------------------------------ the end of the tick (server side only)
        public void EndTick()
        {
            if (remoteActive && (rays.Count > 0 || events.Count > 0))
                Flush();

            rays.Clear();
            events.Clear();
            tileBuf.Clear();

            // Decided here for the NEXT tick, so one loop over the players per tick is all it costs.
            remoteActive = false;
            if (Main.netMode == NetmodeID.Server)
            {
                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    if (IsRemoteTarget(i))
                    {
                        remoteActive = true;
                        break;
                    }
                }
            }
        }

        // A connected player that gets packets. A host that plays is fed locally, never by packet.
        static bool IsRemoteTarget(int i)
        {
            return Main.player[i].active && (Main.dedServ || i != Main.myPlayer);
        }

        void Flush()
        {
            CosmicRadiationSystem system = CosmicRadiationSystem.Instance;
            float radius = Math.Max(10f, system.NetInterestRadiusTiles);
            float speed = system.ParticleSpeed;
            int rayCap = Math.Max(1, system.NetMaxRaysPerTick);
            int eventCap = Math.Max(1, system.NetMaxEventsPerTick);

            for (int player = 0; player < Main.maxPlayers; player++)
            {
                if (!IsRemoteTarget(player))
                    continue;

                float cx = Main.player[player].Center.X / 16f;
                float cy = Main.player[player].Center.Y / 16f;

                pickedRays.Clear();
                for (int i = 0; i < rays.Count; i++)
                {
                    CosmicRayEvent r = rays[i];
                    if (!SegmentNear(r.StartX, r.StartY, r.StartX + r.DirX * r.Length, r.StartY + r.DirY * r.Length, cx, cy, radius))
                        continue;

                    if (pickedRays.Count >= rayCap)
                        RaysDropped++;
                    else
                        pickedRays.Add(i);
                }

                pickedEvents.Clear();
                for (int i = 0; i < events.Count; i++)
                {
                    CosmicPendingEvent p = events[i];
                    bool near = p.Kind == CosmicEventKind.Strike
                        ? SegmentNear(p.AX, p.AY, p.BX, p.BY, cx, cy, radius)
                        : SegmentNear(p.AX, p.AY, p.AX, p.AY, cx, cy, radius);

                    if (!near)
                        continue;

                    if (pickedEvents.Count >= eventCap)
                        EventsDropped++;
                    else
                        pickedEvents.Add(i);
                }

                if (pickedRays.Count > 0)
                {
                    RaysSent += pickedRays.Count;
                    PacketsSent++;
                    FactorrariaNet.SendToClient(player, FactorrariaPacket.CosmicRays, writer => CosmicWire.WriteRays(writer, speed, rays, pickedRays));
                }

                if (pickedEvents.Count > 0)
                {
                    EventsSent += pickedEvents.Count;
                    PacketsSent++;
                    FactorrariaNet.SendToClient(player, FactorrariaPacket.CosmicEvent, writer => CosmicWire.WriteEvents(writer, events, tileBuf, pickedEvents));
                }
            }
        }

        // Is the segment (ax, ay) to (bx, by) within `radius` tiles of the point (cx, cy)?
        static bool SegmentNear(float ax, float ay, float bx, float by, float cx, float cy, float radius)
        {
            float vx = bx - ax;
            float vy = by - ay;
            float lengthSquared = vx * vx + vy * vy;
            float t = lengthSquared < 1e-6f ? 0f : ((cx - ax) * vx + (cy - ay) * vy) / lengthSquared;
            t = Math.Max(0f, Math.Min(1f, t));

            float px = ax + vx * t - cx;
            float py = ay + vy * t - cy;
            return px * px + py * py <= radius * radius;
        }
    }

    // The two packets, byte by byte (everything is cosmetic; both sides must agree, so a change here bumps nothing saved):
    //   CosmicRays (55):  float speed (tiles/tick), ushort count, then per ray:
    //                     byte (color | end << 3), float startX, float startY, short angle (radians / pi * 32767), ushort length (1/16 tile)
    //   CosmicEvent (56): ushort count, then per event a kind byte and
    //                     Absorb:    byte color, short tileX, short tileY
    //                     Ionize:    byte color, ushort lifeTicks, ushort n, n x (short x, short y)
    //                     Discharge: byte tier, float centerX, float centerY, ushort n, n x (short x, short y)
    //                     Strike:    byte tier, float fromX, float fromY, float toX, float toY
    //                     Spit:      byte tier, float fromX, float fromY, float dirX, float dirY
    // tier = log10(energy) x 4, 0 to 255 (how thick the bolt is drawn).
    public static class CosmicWire
    {
        public const int MaxRaysInPacket = 4096;
        public const int MaxEventsInPacket = 4096;
        public const int MaxTilesInEvent = 512;

        public static byte EnergyTier(double energy)
        {
            if (!(energy > 1.0))
                return 0;

            double tier = Math.Log10(energy) * 4.0;
            return (byte)Math.Max(0.0, Math.Min(255.0, tier));
        }

        public static void WriteRays(BinaryWriter writer, float speed, List<CosmicRayEvent> rays, List<int> picked)
        {
            writer.Write(speed);
            writer.Write((ushort)picked.Count);

            for (int i = 0; i < picked.Count; i++)
            {
                CosmicRayEvent r = rays[picked[i]];
                writer.Write((byte)((byte)r.Color | ((byte)r.End << 3)));
                writer.Write(r.StartX);
                writer.Write(r.StartY);
                writer.Write((short)Math.Round(Math.Atan2(r.DirY, r.DirX) / Math.PI * 32767.0));
                writer.Write((ushort)Math.Max(0.0, Math.Min(65535.0, Math.Round(r.Length * 16.0))));
            }
        }

        public static void ReadRays(BinaryReader reader, CosmicParticleSystem target)
        {
            float speed = reader.ReadSingle();
            int count = reader.ReadUInt16();
            if (count > MaxRaysInPacket || !(speed > 0f) || speed > 100f)
                return;

            for (int i = 0; i < count; i++)
            {
                byte packed = reader.ReadByte();
                float x = reader.ReadSingle();
                float y = reader.ReadSingle();
                double angle = reader.ReadInt16() / 32767.0 * Math.PI;
                float length = reader.ReadUInt16() / 16f;

                if (float.IsNaN(x) || float.IsNaN(y))
                    continue;

                target.IncomingRays.Enqueue(new CosmicClientRay
                {
                    Color = (byte)(packed & 7),
                    End = (byte)((packed >> 3) & 3),
                    X = x,
                    Y = y,
                    DirX = (float)Math.Cos(angle),
                    DirY = (float)Math.Sin(angle),
                    Length = length,
                    Speed = speed
                });
            }
        }

        public static void WriteEvents(BinaryWriter writer, List<CosmicPendingEvent> events, List<short> tiles, List<int> picked)
        {
            writer.Write((ushort)picked.Count);

            for (int i = 0; i < picked.Count; i++)
            {
                CosmicPendingEvent p = events[picked[i]];
                writer.Write(p.Kind);

                switch (p.Kind)
                {
                    case CosmicEventKind.Absorb:
                        writer.Write(p.Color);
                        writer.Write((short)p.AX);
                        writer.Write((short)p.AY);
                        break;

                    case CosmicEventKind.Ionize:
                        writer.Write(p.Color);
                        writer.Write(p.Life);
                        WriteTiles(writer, tiles, p);
                        break;

                    case CosmicEventKind.Discharge:
                        writer.Write(p.Tier);
                        writer.Write(p.AX);
                        writer.Write(p.AY);
                        WriteTiles(writer, tiles, p);
                        break;

                    default:   // Strike and Spit
                        writer.Write(p.Tier);
                        writer.Write(p.AX);
                        writer.Write(p.AY);
                        writer.Write(p.BX);
                        writer.Write(p.BY);
                        break;
                }
            }
        }

        static void WriteTiles(BinaryWriter writer, List<short> tiles, CosmicPendingEvent p)
        {
            writer.Write((ushort)p.TileCount);
            for (int k = 0; k < p.TileCount * 2; k++)
                writer.Write(tiles[p.TileStart + k]);
        }

        public static void ReadEvents(BinaryReader reader, CosmicParticleSystem target)
        {
            int count = reader.ReadUInt16();
            if (count > MaxEventsInPacket)
                return;

            for (int i = 0; i < count; i++)
            {
                CosmicClientEvent e = default;
                e.Kind = reader.ReadByte();

                switch (e.Kind)
                {
                    case CosmicEventKind.Absorb:
                        e.Color = reader.ReadByte();
                        e.AX = reader.ReadInt16();
                        e.AY = reader.ReadInt16();
                        break;

                    case CosmicEventKind.Ionize:
                        e.Color = reader.ReadByte();
                        e.Life = reader.ReadUInt16();
                        e.Tiles = ReadTiles(reader);
                        break;

                    case CosmicEventKind.Discharge:
                        e.Tier = reader.ReadByte();
                        e.AX = reader.ReadSingle();
                        e.AY = reader.ReadSingle();
                        e.Tiles = ReadTiles(reader);
                        break;

                    case CosmicEventKind.Strike:
                    case CosmicEventKind.Spit:
                        e.Tier = reader.ReadByte();
                        e.AX = reader.ReadSingle();
                        e.AY = reader.ReadSingle();
                        e.BX = reader.ReadSingle();
                        e.BY = reader.ReadSingle();
                        break;

                    default:
                        return;   // an unknown kind: the rest of this packet cannot be read, drop it
                }

                if (e.Color > 6)
                    e.Color = 6;

                target.IncomingEvents.Enqueue(e);
            }
        }

        static short[] ReadTiles(BinaryReader reader)
        {
            int n = reader.ReadUInt16();
            if (n > MaxTilesInEvent)
                throw new InvalidDataException("cosmic event with " + n + " tiles");

            short[] tiles = new short[n * 2];
            for (int k = 0; k < tiles.Length; k++)
                tiles[k] = reader.ReadInt16();
            return tiles;
        }
    }
}
