using Factorraria.Common.Systems;
using Factorraria.Content.Items.Materials;
using Factorraria.Content.Items.Wires;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // Custom wires (roadmap patch 3.1, finding I1). The server owns CustomWireSystem.WireGrid because the power grid is built there.
    //  - the owner's wire / cutter item sends WireEditRequest(add or remove, x, y, one wire type)
    //  - the server checks the player, the tile, the reach and the current state, changes WireGrid (which flags the power grid for a
    //    rebuild), spawns the returned wire item when a wire was cut, and sends WireChanged(x, y, flags now) to everyone else
    //  - a request the server does not accept (out of reach, wire already there or already gone) is answered with WireChanged to the
    //    sender alone, so the owner's predicted change is undone and the client matches the server again
    //  - WireChanged carries the tile's complete flags, not an add/remove, so a repeated or out-of-order packet cannot drift
    //  - a joining client gets the whole grid from the "custom-wires" join provider
    // On a multiplayer client the owner also applies its own edit at once (prediction). In single-player the request runs the server
    // handler directly, so nothing is predicted and nothing is sent.
    public class CustomWireNetSystem : ModSystem
    {
        public override void Load()
        {
            FactorrariaNet.Register(FactorrariaPacket.WireEditRequest, PacketDirection.ToServer, CustomWireNet.HandleEdit);
            FactorrariaNet.Register(FactorrariaPacket.WireChanged, PacketDirection.ToClient, CustomWireNet.HandleChanged);
            JoinSync.Register("custom-wires", CustomWireNet.WriteAll, CustomWireNet.ReadAll);
        }
    }

    public static class CustomWireNet
    {
        // Sanity limit on how many wire tiles a join packet may claim (a bad packet must not make the client allocate without end).
        public static int MaxJoinWires = 5000000;

        public static bool IsSingleKnownType(CustomWireType type)
        {
            foreach (CustomWireType known in CustomWireSystem.AllWireTypes)
            {
                if (type == known)
                    return true;
            }

            return false;
        }

        public static int WireItemType(CustomWireType type)
        {
            if (type == CustomWireType.Gold)
                return ModContent.ItemType<GoldWireItem>();

            return type == CustomWireType.Tin ? ModContent.ItemType<TinWireItem>() : ModContent.ItemType<CopperWireItem>();
        }

        /// <summary>Owner side: place (add = true) or cut (add = false) one wire type at a tile.</summary>
        public static void RequestEdit(bool add, int x, int y, CustomWireType type)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                // Prediction: show the change at once. The server confirms by saying nothing (everyone else is told), or corrects us.
                if (add)
                    CustomWireSystem.AddWire(x, y, type);
                else
                    CustomWireSystem.RemoveWire(x, y, type);
            }

            FactorrariaNet.SendRequest(FactorrariaPacket.WireEditRequest, writer =>
            {
                writer.Write(add);
                writer.Write((short)x);
                writer.Write((short)y);
                writer.Write((byte)type);
            });
        }

        // Server (or single-player, where sender is the local player).
        public static void HandleEdit(BinaryReader reader, int sender)
        {
            bool add = reader.ReadBoolean();
            int x = reader.ReadInt16();
            int y = reader.ReadInt16();
            CustomWireType type = (CustomWireType)reader.ReadByte();

            if (sender < 0 || sender >= Main.maxPlayers)
                return;

            Player player = Main.player[sender];
            if (!player.active || player.dead || !IsSingleKnownType(type) || !WorldGen.InWorld(x, y, 1))
                return;

            bool changed = false;

            if (FactorrariaNet.InReach(player, x, y))
            {
                if (add)
                {
                    if (!CustomWireSystem.HasWire(x, y, type))
                    {
                        CustomWireSystem.AddWire(x, y, type);
                        changed = true;
                    }
                }
                else if (CustomWireSystem.RemoveWire(x, y, type))
                {
                    changed = true;

                    // The returned wire is spawned here, once. NewItem on a server tells every client about the item itself.
                    Item.NewItem(player.GetSource_Misc("FactorrariaWireCut"), x * 16, y * 16, 16, 16, WireItemType(type));
                }
            }

            CustomWireType now = CustomWireSystem.WireAt(x, y);

            if (changed)
            {
                FactorrariaNet.Log("wire " + (add ? "added" : "cut") + " " + type + " at (" + x + ", " + y + ") by " + player.name + ", wire tiles now " + CustomWireSystem.WireGrid.Count);
                SendChanged(x, y, now, -1, sender);
            }
            else
            {
                // Not accepted (or nothing to do): tell the sender what the tile really holds so its prediction is undone.
                SendChanged(x, y, now, sender, -1);
            }
        }

        /// <summary>Server only: tells one client (toClient) or everyone except ignoreClient what a tile holds.</summary>
        static void SendChanged(int x, int y, CustomWireType flags, int toClient, int ignoreClient)
        {
            void Write(BinaryWriter writer)
            {
                writer.Write((short)x);
                writer.Write((short)y);
                writer.Write((byte)flags);
            }

            if (toClient >= 0)
                FactorrariaNet.SendToClient(toClient, FactorrariaPacket.WireChanged, Write);
            else
                FactorrariaNet.Broadcast(FactorrariaPacket.WireChanged, Write, ignoreClient);
        }

        // Client.
        public static void HandleChanged(BinaryReader reader, int sender)
        {
            int x = reader.ReadInt16();
            int y = reader.ReadInt16();
            CustomWireType flags = (CustomWireType)reader.ReadByte();

            if (!WorldGen.InWorld(x, y, 1))
                return;

            CustomWireSystem.SetWireAt(x, y, flags);
        }

        // ---------------------------------------------------------------- join provider ("custom-wires")

        public static void WriteAll(BinaryWriter writer)
        {
            writer.Write(CustomWireSystem.WireGrid.Count);

            foreach (var pair in CustomWireSystem.WireGrid)
            {
                writer.Write(pair.Key.X);
                writer.Write(pair.Key.Y);
                writer.Write((byte)pair.Value);
            }
        }

        public static void ReadAll(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > MaxJoinWires)
                throw new InvalidDataException("custom wire count " + count);

            var wires = new List<KeyValuePair<Point16, CustomWireType>>(count);
            for (int i = 0; i < count; i++)
            {
                short x = reader.ReadInt16();
                short y = reader.ReadInt16();
                wires.Add(new KeyValuePair<Point16, CustomWireType>(new Point16(x, y), (CustomWireType)reader.ReadByte()));
            }

            CustomWireSystem.ReplaceAll(wires);
            FactorrariaNet.Log("join provider custom-wires: " + count + " wire tiles");
        }
    }

    // Multiplayer test command: /fwires prints how many wire tiles THIS process knows (compare the client with the server log line).
    public class WireCountCommand : ModCommand
    {
        public override string Command => "fwires";
        public override CommandType Type => CommandType.Chat;
        public override string Description => "Multiplayer test: shows how many custom wire tiles this client knows.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            caller.Reply("fwires: " + CustomWireSystem.WireGrid.Count + " wire tile(s) known here", Color.LightGreen);
        }
    }
}