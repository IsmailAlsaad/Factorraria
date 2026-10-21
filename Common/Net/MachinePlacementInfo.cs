using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Factorraria.Common.Systems;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // Machine placement info (roadmap patch 4.3, finding M5). The biome a machine is placed in decides which biome water it makes
    // (Snow Water, Jungle Water, ...) and whether the Ice Machine works, but it is read from the PLACER's Zone flags, and a dedicated
    // server has no local player, so it always got Unknown.
    //
    // Authority: the placing client detects the biome (BaseMachine.DetermineBiomeAt, unchanged) and sends one small follow-up packet,
    // MachineBiomeReport(x, y, biome), right after its placement request. The server owns BaseMachine.InsideBiome: it applies the value
    // to the new entity and keeps saving it in SaveData. Nothing else about the machine travels in this packet.
    // Single-player: no packet, OnPlaced detects the biome locally as before.
    //
    // Ordering: the report normally arrives after the entity exists (same connection, sent after the placement request), but the server
    // handles both orders. An entity created on the server waits in 'awaiting' until its report arrives; a report that arrives first is
    // kept in 'early' and applied by OnServerPlaced. Both expire after WaitTicks, so a machine placed by server code (the Robotic Arm)
    // simply keeps Unknown (known limit, see the roadmap).
    public class MachinePlacementInfoSystem : ModSystem
    {
        public override void Load()
        {
            FactorrariaNet.Register(FactorrariaPacket.MachineBiomeReport, PacketDirection.ToServer, MachinePlacementInfo.HandleBiomeReport);
        }

        public override void PostUpdateEverything()
        {
            if (Main.netMode == NetmodeID.Server && Main.GameUpdateCount % 60 == 0)
                MachinePlacementInfo.Prune();
        }

        public override void OnWorldUnload()
        {
            MachinePlacementInfo.Clear();
            MachinePlacement.RejectNextEntityRequest = false; // patch 7b.6 test aid
        }

        public override void Unload()
        {
            MachinePlacementInfo.Clear();
        }
    }

    public static class MachinePlacementInfo
    {
        public static int WaitTicks = 600;
        public static int MaxEntries = 256;

        // Machine created on the server, report not received yet: corner -> tick created.
        static readonly Dictionary<Point16, uint> awaiting = new();

        // Report received, machine not created yet: corner -> (biome, tick received).
        static readonly Dictionary<Point16, KeyValuePair<MachineBiome, uint>> early = new();

        // ---------------------------------------------------------------- client

        /// <summary>Multiplayer client only: reports the local player's biome for the machine just placed at this tile.</summary>
        public static void SendBiome(int x, int y)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            MachineBiome biome = BaseMachine.DetermineBiomeAt(x, y);

            FactorrariaNet.SendRequest(FactorrariaPacket.MachineBiomeReport, writer =>
            {
                writer.Write((short)x);
                writer.Write((short)y);
                writer.Write((byte)biome);
            });
        }

        /// <summary>Same, for a placement of a vanilla tile: only sends when that tile type is a registered machine.</summary>
        public static void SendBiomeForVanillaTile(int x, int y, int tileType)
        {
            if (tileType >= TileID.Count || !MachineVisualRegistry.Definitions.ContainsKey(tileType))
                return;

            SendBiome(x, y);
        }

        // ---------------------------------------------------------------- server

        /// <summary>Called at the end of BaseMachine.OnPlaced. On a dedicated server: applies a report that already arrived, else waits for it.</summary>
        public static void OnServerPlaced(BaseMachine machine)
        {
            if (Main.netMode != NetmodeID.Server || machine == null)
                return;

            Point16 corner = machine.Position;

            if (early.TryGetValue(corner, out KeyValuePair<MachineBiome, uint> report))
            {
                early.Remove(corner);
                if (!Expired(report.Value))
                {
                    Apply(machine, report.Key, "report arrived first");
                    return;
                }
            }

            if (awaiting.Count < MaxEntries)
                awaiting[corner] = Main.GameUpdateCount;
        }

        // Server: a client reports the biome of the machine it just placed.
        public static void HandleBiomeReport(BinaryReader reader, int sender)
        {
            int x = reader.ReadInt16();
            int y = reader.ReadInt16();
            int raw = reader.ReadByte();

            if (Main.netMode != NetmodeID.Server || sender < 0 || sender >= Main.maxPlayers)
                return;

            Player player = Main.player[sender];
            string who = player.name + " (" + sender + ")";

            if (!player.active || !WorldGen.InWorld(x, y, 1) || !System.Enum.IsDefined(typeof(MachineBiome), raw))
            {
                FactorrariaNet.Log("[Machines] biome report from " + who + " rejected: bad position or biome (" + x + ", " + y + ", " + raw + ")");
                return;
            }

            if (!Main.tile[x, y].HasTile)
            {
                FactorrariaNet.Log("[Machines] biome report from " + who + " rejected: no tile at (" + x + ", " + y + ")");
                return;
            }

            if (!FactorrariaNet.InReach(player, x, y))
            {
                FactorrariaNet.Log("[Machines] biome report from " + who + " rejected: (" + x + ", " + y + ") is out of reach");
                return;
            }

            var biome = (MachineBiome)raw;
            TileEntityHelper.TryGetEntityFromTile(x, y, out TileEntity entity, out Point16 corner);

            if (entity is BaseMachine machine)
            {
                if (awaiting.Remove(corner))
                    Apply(machine, biome, "reported by " + who);
                else
                    FactorrariaNet.Log("[Machines] biome report from " + who + " ignored: " + machine.GetType().Name + " at (" + corner.X + ", " + corner.Y + ") is not waiting for one");
                return;
            }

            if (early.Count < MaxEntries)
                early[corner] = new KeyValuePair<MachineBiome, uint>(biome, Main.GameUpdateCount);
        }

        static void Apply(BaseMachine machine, MachineBiome biome, string why)
        {
            machine.InsideBiome = biome;
            MachinePlacement.Share(machine);   // patch 5.1: the placement share went out before the biome was known; send the updated snapshot
            FactorrariaNet.Log("[Machines] biome " + biome + " applied to " + machine.GetType().Name + " at (" + machine.Position.X + ", " + machine.Position.Y + ") (" + why + ")");
        }

        static bool Expired(uint tick) => Main.GameUpdateCount - tick > (uint)WaitTicks;

        /// <summary>Server: drops waiting entries that never got their partner (arm-placed machines, lost or rejected reports).</summary>
        public static void Prune()
        {
            if (awaiting.Count > 0)
            {
                var old = new List<Point16>();
                foreach (KeyValuePair<Point16, uint> pair in awaiting)
                    if (Expired(pair.Value)) old.Add(pair.Key);
                foreach (Point16 key in old) awaiting.Remove(key);
            }

            if (early.Count > 0)
            {
                var old = new List<Point16>();
                foreach (KeyValuePair<Point16, KeyValuePair<MachineBiome, uint>> pair in early)
                    if (Expired(pair.Value.Value)) old.Add(pair.Key);
                foreach (Point16 key in old) early.Remove(key);
            }
        }

        public static void Clear()
        {
            awaiting.Clear();
            early.Clear();
        }
    }
}