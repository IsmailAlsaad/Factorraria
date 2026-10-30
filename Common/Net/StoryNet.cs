using Factorraria.Common.Story;
using Factorraria.Content.Items.Story;
using Factorraria.Content.NPCs.HerBackpack;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // NPC story flags (NPC roadmap patch N1.1). The server owns the flags (StorySystem.ServerSet). Two ways a client learns them:
    //  - join: provider "story-flags" sends the complete set, the client replaces its copy
    //  - change: StoryFlagChanged (server -> every client) carries one key and its new value
    // Single-player has no packets: ServerSet already changed the only copy there is.
    public class StoryNetSystem : ModSystem
    {
        public override void Load()
        {
            FactorrariaNet.Register(FactorrariaPacket.StoryFlagChanged, PacketDirection.ToClient, StoryNet.HandleFlagChanged);
            FactorrariaNet.Register(FactorrariaPacket.StoryActionRequest, PacketDirection.ToServer, StoryNet.HandleActionRequest, 3);
            FactorrariaNet.Register(FactorrariaPacket.StoryActionResult, PacketDirection.ToClient, StoryNet.HandleActionResult);
            JoinSync.Register("story-flags", StoryNet.WriteAll, StoryNet.ReadAll);
        }
    }

    /// <summary>What a story action request asks for (NPC story patch N4.2). More kinds arrive with N5.1 (turn in the Bag) and N6 (the scrolls).</summary>
    public enum StoryActionKind : byte
    {
        TakeBag = 0
    }

    public enum StoryActionFailure : byte
    {
        None = 0,
        CannotAct = 1,    // the player is dead or not in the game
        NoNpc = 2,        // the NPC is gone (someone else took the Backpack, or it despawned) or is not the right kind
        TooFar = 3,
        Unavailable = 4   // the rule says no right now (the Bag was already taken, the shop is already unlocked, unknown kind)
    }

    public static class StoryNet
    {
        // Tunables (not const, so hot reload can change them).
        public static int MaxJoinFlags = 4096;
        public static int InteractReachTiles = 12;     // server: how far (in tiles) a player may be from the NPC and still act on it
        public static int ActionTimeoutTicks = 300;    // client: a request with no answer after this long is forgotten

        /// <summary>Server only (no-op elsewhere): tells every client that one flag changed.</summary>
        public static void BroadcastFlag(string key, bool value)
        {
            if (Main.netMode != NetmodeID.Server)
                return;

            FactorrariaNet.Broadcast(FactorrariaPacket.StoryFlagChanged, writer =>
            {
                writer.Write(key);
                writer.Write(value);
            });
        }

        // Client.
        public static void HandleFlagChanged(BinaryReader reader, int sender)
        {
            string key = reader.ReadString();
            bool value = reader.ReadBoolean();
            StorySystem.ApplyFromServer(key, value);
        }

        // ---------------------------------------------------------------- story actions (NPC story patch N4.2)
        // The client sends StoryActionRequest(kind, npc index). The server checks everything with its own state (the NPC exists and is the right
        // kind, the player is close, the story rule still allows it), does the work, and answers the sender with StoryActionResult. The Backpack
        // disappears for everybody through the normal NPC sync, the flag change through the story flag sync. Single-player: the handler runs
        // right in SendRequest and the "answer" is applied directly, no packets.

        static bool actionPending;
        static uint actionPendingTick;

        /// <summary>Client: true while a request waits for its answer, so a double click cannot send two.</summary>
        public static bool ActionPending => actionPending && Main.GameUpdateCount - actionPendingTick < (uint)System.Math.Max(1, ActionTimeoutTicks);

        /// <summary>Client (or single-player): asks the server to give the player the Bag from this Her Backpack NPC.</summary>
        public static void RequestTakeBag(int npcIndex)
        {
            if (ActionPending)
                return;

            actionPending = true;
            actionPendingTick = Main.GameUpdateCount;

            FactorrariaNet.SendRequest(FactorrariaPacket.StoryActionRequest, writer =>
            {
                writer.Write((byte)StoryActionKind.TakeBag);
                writer.Write((short)npcIndex);
            });
        }

        // Server (or single-player, where sender is the local player).
        public static void HandleActionRequest(BinaryReader reader, int sender)
        {
            StoryActionKind kind = (StoryActionKind)reader.ReadByte();
            int npcIndex = reader.ReadInt16();

            if (sender < 0 || sender >= Main.maxPlayers)
                return;

            Player player = Main.player[sender];
            if (!player.active || player.dead)
            {
                ReplyAction(sender, kind, false, StoryActionFailure.CannotAct);
                return;
            }

            switch (kind)
            {
                case StoryActionKind.TakeBag:
                    HandleTakeBag(player, sender, npcIndex);
                    break;

                default:
                    ReplyAction(sender, kind, false, StoryActionFailure.Unavailable);
                    break;
            }
        }

        static void HandleTakeBag(Player player, int sender, int npcIndex)
        {
            if (npcIndex < 0 || npcIndex >= Main.maxNPCs)
            {
                ReplyAction(sender, StoryActionKind.TakeBag, false, StoryActionFailure.NoNpc);
                return;
            }

            NPC npc = Main.npc[npcIndex];
            if (!npc.active || npc.type != ModContent.NPCType<HerBackpackNPC>())
            {
                ReplyAction(sender, StoryActionKind.TakeBag, false, StoryActionFailure.NoNpc);
                return;
            }

            if (player.Distance(npc.Center) > System.Math.Max(1, InteractReachTiles) * 16f)
            {
                ReplyAction(sender, StoryActionKind.TakeBag, false, StoryActionFailure.TooFar);
                return;
            }

            // The Bag exists once: the first player to get here takes it, a second request in the same moment finds the flag already set.
            if (StorySystem.Get(StoryFlags.BackpackTaken) || StorySystem.Get(StoryFlags.MechanicShopUnlocked))
            {
                ReplyAction(sender, StoryActionKind.TakeBag, false, StoryActionFailure.Unavailable);
                return;
            }

            StorySystem.ServerSet(StoryFlags.BackpackTaken, true);
            player.QuickSpawnItem(player.GetSource_Misc("FactorrariaStoryBag"), ModContent.ItemType<MechanicsBagItem>(), 1);

            npc.active = false;
            npc.netUpdate = true;
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npcIndex);

            FactorrariaNet.Log("[Story] " + player.name + " took the Mechanic's Bag from Her Backpack (NPC #" + npcIndex + ")");
            ReplyAction(sender, StoryActionKind.TakeBag, true, StoryActionFailure.None);
        }

        static void ReplyAction(int sender, StoryActionKind kind, bool accepted, StoryActionFailure failure)
        {
            if (Main.netMode == NetmodeID.Server)
            {
                FactorrariaNet.SendToClient(sender, FactorrariaPacket.StoryActionResult, writer =>
                {
                    writer.Write((byte)kind);
                    writer.Write(accepted);
                    writer.Write((byte)failure);
                });
            }
            else
            {
                // Single-player: no network, apply the answer here.
                ApplyActionResult(kind, accepted, failure);
            }
        }

        // Client.
        public static void HandleActionResult(BinaryReader reader, int sender)
        {
            StoryActionKind kind = (StoryActionKind)reader.ReadByte();
            bool accepted = reader.ReadBoolean();
            StoryActionFailure failure = (StoryActionFailure)reader.ReadByte();
            ApplyActionResult(kind, accepted, failure);
        }

        static void ApplyActionResult(StoryActionKind kind, bool accepted, StoryActionFailure failure)
        {
            actionPending = false;

            if (Main.dedServ)
                return;

            if (accepted)
            {
                // Placeholder texts until the localization pass (N9).
                if (kind == StoryActionKind.TakeBag)
                    Main.NewText("You take the Mechanic's Bag.", 255, 240, 20);

                return;
            }

            switch (failure)
            {
                case StoryActionFailure.TooFar:
                    Main.NewText("You are too far away.", 200, 200, 200);
                    break;
                case StoryActionFailure.NoNpc:
                    Main.NewText("It is gone.", 200, 200, 200);
                    break;
                case StoryActionFailure.Unavailable:
                    Main.NewText("Someone already took it.", 200, 200, 200);
                    break;
                default:
                    Main.NewText("You cannot do that right now.", 200, 200, 200);
                    break;
            }
        }

        // Join provider, server side: the complete set.
        public static void WriteAll(BinaryWriter writer)
        {
            writer.Write(StorySystem.Flags.Count);
            foreach (string key in StorySystem.Flags)
                writer.Write(key);
        }

        // Join provider, client side: replaces the local set.
        public static void ReadAll(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > MaxJoinFlags)
                count = 0;

            List<string> keys = new List<string>(count);
            for (int i = 0; i < count; i++)
                keys.Add(reader.ReadString());

            StorySystem.ReplaceAll(keys);
        }
    }
}