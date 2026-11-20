using Factorraria.Common.Mounts;
using Factorraria.Content.Items.Mounts;
using Microsoft.Xna.Framework;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // Mech Suit build tool, patch M3 (docs/mech_suit_builder/ROADMAP.md). Three packets:
    //   MechBuildRequest   client -> server  "build the region (x, y, w, h) at destination origin (x, y)" (one packet, no blueprint travels:
    //                      the server reads the source region from the world itself). Single-player runs the handler directly.
    //   MechBuildArmState  server -> nearby clients (and the owner)  the builder arms of one player: building on / off, and per arm the target
    //                      tile (or none), the strike length when a strike just started, the held item type. Sent on change and as a refresh.
    //   MechBuildConsumed  server -> owner  "the server took these items from your inventory": vanilla ignores a server's inventory writes for
    //                      the player's own character, so the owner's client removes the same items itself and re-syncs those slots.
    // Packet handlers only parse and queue; PostUpdateEverything applies the queues on the main thread.
    public class MechSuitBuildNetSystem : ModSystem
    {
        public float NearbyTiles = 75f;

        public struct ArmState
        {
            public bool HasTarget;
            public short TargetX;
            public short TargetY;
            public byte StrikeTicks;
            public ushort HeldType;
        }

        public struct StateMessage
        {
            public byte Player;
            public bool Building;
            public ArmState[] Arms;
        }

        public struct ConsumedEntry
        {
            public byte Slot;
            public ushort Type;
            public ushort Count;
        }

        readonly ConcurrentQueue<StateMessage> states = new();
        readonly ConcurrentQueue<ConsumedEntry> consumed = new();

        public override void Load()
        {
            FactorrariaNet.Register(FactorrariaPacket.MechBuildRequest, PacketDirection.ToServer, MechSuitBuildNet.HandleRequest, 4);
            FactorrariaNet.Register(FactorrariaPacket.MechBuildCancel, PacketDirection.ToServer, MechSuitBuildNet.HandleCancel);
            FactorrariaNet.Register(FactorrariaPacket.MechBuildArmState, PacketDirection.ToClient, MechSuitBuildNet.HandleArmState);
            FactorrariaNet.Register(FactorrariaPacket.MechBuildConsumed, PacketDirection.ToClient, MechSuitBuildNet.HandleConsumed);
        }

        public override void Unload()
        {
            while (states.TryDequeue(out _)) { }
            while (consumed.TryDequeue(out _)) { }
        }

        public void Enqueue(StateMessage message) => states.Enqueue(message);
        public void Enqueue(ConsumedEntry entry) => consumed.Enqueue(entry);

        // Multiplayer client, main thread.
        public override void PostUpdateEverything()
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            MechSuitBuilderArms arms = ModContent.GetInstance<MechSuitBuilderArms>();
            while (states.TryDequeue(out StateMessage message))
            {
                if (message.Player >= Main.maxPlayers)
                    continue;

                Player player = Main.player[message.Player];
                if (player == null || !player.active)
                    continue;

                if (message.Player == Main.myPlayer)
                    player.GetModPlayer<MechSuitPlayer>().SetBuilding(message.Building);

                if (!message.Building)
                {
                    arms.MarkEnded(message.Player);
                    continue;
                }

                MechSuitBuilderArmSet set = arms.GetOrCreate(message.Player);
                set.Building = true;
                for (int i = 0; i < set.Arms.Length && i < message.Arms.Length; i++)
                {
                    ArmState state = message.Arms[i];
                    MechSuitBuilderArm arm = set.Arms[i];
                    arm.SetHeld(state.HeldType);

                    if (!state.HasTarget)
                    {
                        arm.Motion.SetTarget(null);
                        continue;
                    }

                    arm.Motion.SetTarget(new Vector2(state.TargetX * 16f + 8f, state.TargetY * 16f + 8f));
                    if (state.StrikeTicks > 0)
                        arm.Motion.StartStrike(state.StrikeTicks); // a refresh never restarts a strike (StrikeTicks is 0 then)
                }
            }

            while (consumed.TryDequeue(out ConsumedEntry entry))
                RemoveFromOwnInventory(entry);
        }

        // The server already took these items from its copy of the inventory; do the same on the real one and tell the server the new stacks.
        static void RemoveFromOwnInventory(ConsumedEntry entry)
        {
            Player me = Main.LocalPlayer;
            int left = entry.Count;

            // The slot the server used first, then any other slot with that item.
            for (int pass = 0; pass < 2 && left > 0; pass++)
            {
                for (int s = pass == 0 ? entry.Slot : 0; s < (pass == 0 ? entry.Slot + 1 : 58) && left > 0; s++)
                {
                    Item item = me.inventory[s];
                    if (item.IsAir || item.type != entry.Type)
                        continue;

                    int take = System.Math.Min(left, item.stack);
                    item.stack -= take;
                    left -= take;
                    if (item.stack <= 0)
                        item.TurnToAir();

                    NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, me.whoAmI, s, item.prefix);
                }
            }
        }
    }

    public static class MechSuitBuildNet
    {
        /// <summary>Owner side: ask the server to build the copy of source at origin (the destination's top-left tile).</summary>
        public static void RequestBuild(Rectangle source, Point origin)
        {
            FactorrariaNet.SendRequest(FactorrariaPacket.MechBuildRequest, writer =>
            {
                writer.Write((short)source.X);
                writer.Write((short)source.Y);
                writer.Write((short)source.Width);
                writer.Write((short)source.Height);
                writer.Write((short)origin.X);
                writer.Write((short)origin.Y);
            });
        }

        /// <summary>Owner side: ask the server to end the build job of this player (single-player runs the handler directly).</summary>
        public static void RequestCancel()
        {
            FactorrariaNet.SendRequest(FactorrariaPacket.MechBuildCancel);
        }

        // Server (or single-player). A job only ever ends for the sender, so a client cannot cancel someone else's build.
        public static void HandleCancel(BinaryReader reader, int sender)
        {
            if (sender < 0 || sender >= Main.maxPlayers)
                return;

            ModContent.GetInstance<MechSuitBuildSystem>().EndJob(sender, "cancelled by the player");
        }

        // Server (or single-player, where sender is the local player).
        public static void HandleRequest(BinaryReader reader, int sender)
        {
            Rectangle source = new Rectangle(reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16());
            Point origin = new Point(reader.ReadInt16(), reader.ReadInt16());
            ModContent.GetInstance<MechSuitBuildSystem>().StartJob(sender, source, origin);
        }

        /// <summary>Server: tells the players near the owner (the owner included) what its builder arms do. building = false ends them.</summary>
        public static void SendArmState(Player owner, bool building, MechSuitBuildNetSystem.ArmState[] arms)
        {
            if (Main.netMode != NetmodeID.Server)
                return;

            void Write(BinaryWriter writer)
            {
                writer.Write((byte)owner.whoAmI);
                writer.Write(building);
                for (int i = 0; i < arms.Length; i++)
                {
                    writer.Write(arms[i].HasTarget);
                    writer.Write(arms[i].TargetX);
                    writer.Write(arms[i].TargetY);
                    writer.Write(arms[i].StrikeTicks);
                    writer.Write(arms[i].HeldType);
                }
            }

            FactorrariaNet.SendToNearby(owner.Center, ModContent.GetInstance<MechSuitBuildNetSystem>().NearbyTiles, FactorrariaPacket.MechBuildArmState, Write);
        }

        /// <summary>Server: tells the owner which inventory items the build job consumed (slot, item type, count).</summary>
        public static void SendConsumed(Player owner, List<MechSuitBuildNetSystem.ConsumedEntry> entries)
        {
            if (Main.netMode != NetmodeID.Server || entries.Count == 0)
                return;

            FactorrariaNet.SendToClient(owner.whoAmI, FactorrariaPacket.MechBuildConsumed, writer =>
            {
                writer.Write((byte)entries.Count);
                foreach (MechSuitBuildNetSystem.ConsumedEntry entry in entries)
                {
                    writer.Write(entry.Slot);
                    writer.Write(entry.Type);
                    writer.Write(entry.Count);
                }
            });
        }

        // Client.
        public static void HandleArmState(BinaryReader reader, int sender)
        {
            MechSuitBuildNetSystem.StateMessage message = new MechSuitBuildNetSystem.StateMessage
            {
                Player = reader.ReadByte(),
                Building = reader.ReadBoolean(),
                Arms = new MechSuitBuildNetSystem.ArmState[2]
            };

            for (int i = 0; i < message.Arms.Length; i++)
            {
                message.Arms[i] = new MechSuitBuildNetSystem.ArmState
                {
                    HasTarget = reader.ReadBoolean(),
                    TargetX = reader.ReadInt16(),
                    TargetY = reader.ReadInt16(),
                    StrikeTicks = reader.ReadByte(),
                    HeldType = reader.ReadUInt16()
                };
            }

            ModContent.GetInstance<MechSuitBuildNetSystem>().Enqueue(message);
        }

        // Client.
        public static void HandleConsumed(BinaryReader reader, int sender)
        {
            MechSuitBuildNetSystem system = ModContent.GetInstance<MechSuitBuildNetSystem>();
            int count = reader.ReadByte();
            for (int i = 0; i < count; i++)
            {
                system.Enqueue(new MechSuitBuildNetSystem.ConsumedEntry
                {
                    Slot = reader.ReadByte(),
                    Type = reader.ReadUInt16(),
                    Count = reader.ReadUInt16()
                });
            }
        }
    }
}