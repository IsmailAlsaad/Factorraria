using Factorraria.Common.Net;
using Microsoft.Xna.Framework;
using System.Collections.Concurrent;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.RoboticArm
{
    // Patch 7b.4 (finding A5b): the arm's animation on multiplayer clients.
    //
    // Authority: the server runs the worker and the hand's motion model (RoboticArmMotion). A client never runs the worker; its replica
    // of the arm only needs to MOVE LIKE the server's, so the server tells the nearby players what the hand is doing and each client
    // replays it in its own RoboticArmMotion (ticked by DynamicUpdate on a client, then drawn by RoboticArmVisual, both unchanged).
    //
    // One packet type, ArmState (server -> client, sent only to players within NearbyTiles of the arm):
    //   arm position (the entity's tile), flags (has target), target tile, strike ticks (0 = no new strike), held item type (0 = empty).
    // The server sends one when something CHANGED since the last one: a target appeared, moved to another tile or vanished (that covers
    // power loss, which clears the target), a strike started, or the held item type changed; and a refresh while it is idle or working
    // so a player who walks into range (or a client that missed one) catches up within a second or five. A strike packet carries the
    // strike length, so the client's swing lines up with the server's impact moment (the tile square of patch 7b.2 is sent at that moment).
    // A refresh never restarts a strike. Nothing is sent when no player is near.
    //
    // The held item: slot contents are not replicated for an arm nobody has opened (5.3), so the held item type travels in the packet
    // and the visual draws from it (RoboticArmTileEntity.HeldForVisual). Until the first packet arrives the replicated slot is used.
    //
    // Threads: packet handlers parse into a queue; PostUpdateEverything applies it on the main thread.
    public class RoboticArmNet : ModSystem
    {
        public const float NearbyTiles = 75f;
        const int RefreshWorking = 60;
        const int RefreshIdle = 300;

        // TEMPORARY test counters (remove in Phase 10), printed by /farmsend status.
        public static int Sent;
        public static int Received;
        public static int Missed;

        struct State
        {
            public short X;
            public short Y;
            public bool HasTarget;
            public short TargetX;
            public short TargetY;
            public int StrikeTicks;
            public ushort HeldType;
        }

        static readonly ConcurrentQueue<State> incoming = new ConcurrentQueue<State>();

        public static string Describe() => "armNet: sent=" + Sent + " received=" + Received + " missed=" + Missed;

        public override void Load()
        {
            FactorrariaNet.Register(FactorrariaPacket.ArmState, PacketDirection.ToClient, HandleState);
        }

        public override void Unload()
        {
            while (incoming.TryDequeue(out _))
            {
            }
        }

        static void HandleState(System.IO.BinaryReader reader, int sender)
        {
            State state = new State();
            state.X = reader.ReadInt16();
            state.Y = reader.ReadInt16();
            state.HasTarget = reader.ReadBoolean();
            state.TargetX = reader.ReadInt16();
            state.TargetY = reader.ReadInt16();
            state.StrikeTicks = reader.ReadByte();
            state.HeldType = reader.ReadUInt16();
            incoming.Enqueue(state);
        }

        // Client, main thread.
        public override void PostUpdateEverything()
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            while (incoming.TryDequeue(out State state))
            {
                Received++;
                if (TileEntity.ByPosition.TryGetValue(new Point16(state.X, state.Y), out TileEntity entity) && entity is RoboticArmTileEntity arm)
                    arm.ApplyNetState(state.HasTarget, state.TargetX, state.TargetY, state.StrikeTicks, state.HeldType);
                else
                    Missed++; // no replica yet (its section has not arrived): the next refresh catches up
            }
        }

        // One per arm entity; remembers what the nearby players were last told. Server only.
        public class Tracker
        {
            bool lastHasTarget;
            Point16 lastTile;
            ushort lastHeld = ushort.MaxValue;
            bool lastStriking;
            int refresh;

            static bool AnyPlayerNear(Vector2 center)
            {
                float radius = NearbyTiles * 16f;
                float radiusSquared = radius * radius;
                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    Player player = Main.player[i];
                    if (player != null && player.active && Vector2.DistanceSquared(player.Center, center) <= radiusSquared)
                        return true;
                }

                return false;
            }

            public void Tick(RoboticArmTileEntity arm)
            {
                if (Main.netMode != NetmodeID.Server)
                    return;

                RoboticArmMotion motion = arm.Motion;
                Item held = arm.InputSlots[0];
                ushort heldType = held.IsAir ? (ushort)0 : (ushort)held.type;
                bool hasTarget = motion.HasTarget;
                Point16 tile = default;
                if (hasTarget)
                    tile = new Point16((int)(motion.Target.Value.X / 16f), (int)(motion.Target.Value.Y / 16f));

                bool striking = motion.Striking;
                bool strikeStarted = striking && !lastStriking;
                lastStriking = striking;

                // Nobody within range: send nothing, and make the next tick after someone arrives send the current state at once.
                if (!AnyPlayerNear(arm.Shoulder))
                {
                    refresh = 0;
                    lastHeld = ushort.MaxValue;
                    return;
                }

                refresh--;
                bool changed = hasTarget != lastHasTarget || (hasTarget && tile != lastTile) || heldType != lastHeld || strikeStarted;
                if (!changed && refresh > 0)
                    return;

                refresh = hasTarget ? RefreshWorking : RefreshIdle;
                lastHasTarget = hasTarget;
                lastTile = tile;
                lastHeld = heldType;

                int strikeTicks = strikeStarted ? System.Math.Min(motion.StrikeDuration, 255) : 0;
                short armX = (short)arm.Position.X;
                short armY = (short)arm.Position.Y;
                Point16 sentTile = tile;

                FactorrariaNet.SendToNearby(arm.Shoulder, NearbyTiles, FactorrariaPacket.ArmState, writer =>
                {
                    writer.Write(armX);
                    writer.Write(armY);
                    writer.Write(hasTarget);
                    writer.Write((short)sentTile.X);
                    writer.Write((short)sentTile.Y);
                    writer.Write((byte)strikeTicks);
                    writer.Write(heldType);
                });
                Sent++;
            }
        }
    }
}