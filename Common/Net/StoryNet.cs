using Factorraria.Common.Story;
using Factorraria.Content.Items.Story;
using Factorraria.Content.NPCs.HerBackpack;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
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

    /// <summary>What a story action request asks for (NPC story patches N4.2, N5.1 and N7.1).</summary>
    public enum StoryActionKind : byte
    {
        TakeBag = 0,
        TurnInBag = 1,
        ClaimScrolls = 2,
        TalkDryad = 3   // N8.2: fire-and-forget, the server never answers it (no result packet, no chat line)
    }

    public enum StoryActionFailure : byte
    {
        None = 0,
        CannotAct = 1,    // the player is dead or not in the game
        NoNpc = 2,        // the NPC is gone (someone else took the Backpack, or it despawned) or is not the right kind
        TooFar = 3,
        Unavailable = 4,  // the rule says no right now (the Bag was already taken, the shop is already unlocked, unknown kind)
        NoItem = 5        // the server's copy of the player's inventory holds no Bag (turn-in)
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

        /// <summary>Client (or single-player): asks the server to take one Mechanic's Bag from this player and unlock the Mechanic's shop. npcIndex = the town Mechanic.</summary>
        public static void RequestTurnInBag(int npcIndex)
        {
            if (ActionPending || !HasBag(Main.LocalPlayer))
                return;

            actionPending = true;
            actionPendingTick = Main.GameUpdateCount;

            FactorrariaNet.SendRequest(FactorrariaPacket.StoryActionRequest, writer =>
            {
                writer.Write((byte)StoryActionKind.TurnInBag);
                writer.Write((short)npcIndex);
            });
        }

        /// <summary>
        /// Client (or single-player): asks the server to hand the three super weapon scrolls to this player (patch N7.1). npcIndex = the town Mechanic.
        /// Sent from the talk-start of the Mechanic.SuperWeaponIntro dialogue, so the first conversation after her rescue gives them.
        /// </summary>
        public static void RequestClaimScrolls(int npcIndex)
        {
            if (ActionPending || StorySystem.Get(StoryFlags.ScrollsGiven))
                return;

            actionPending = true;
            actionPendingTick = Main.GameUpdateCount;

            FactorrariaNet.SendRequest(FactorrariaPacket.StoryActionRequest, writer =>
            {
                writer.Write((byte)StoryActionKind.ClaimScrolls);
                writer.Write((short)npcIndex);
            });
        }

        /// <summary>
        /// Client (or single-player): tells the server this player just started a conversation with a Dryad (patch N8.2, rule D20). Fire and forget:
        /// it does not use ActionPending (no answer comes back), and the flag it sets reaches everybody through the story flag sync. Sent from the
        /// talk-start edge in ImportantDialoguePlayer, also when the Dryad has no active important dialogue.
        /// </summary>
        public static void RequestDryadTalk(int npcIndex)
        {
            if (StorySystem.Get(StoryFlags.DryadTalked))
                return;

            FactorrariaNet.SendRequest(FactorrariaPacket.StoryActionRequest, writer =>
            {
                writer.Write((byte)StoryActionKind.TalkDryad);
                writer.Write((short)npcIndex);
            });
        }

        /// <summary>True when the player's main inventory (the 59 slots, the same ones the turn-in consumes from) holds a Mechanic's Bag. Used by the dialogue condition and by the server check.</summary>
        public static bool HasBag(Player player)
        {
            if (player == null)
                return false;

            int bag = ModContent.ItemType<MechanicsBagItem>();
            for (int i = 0; i < player.inventory.Length; i++)
            {
                Item item = player.inventory[i];
                if (item != null && !item.IsAir && item.type == bag)
                    return true;
            }

            return false;
        }

        // Takes exactly ONE Bag out of the player's main inventory (this copy only). Returns the slot's item (for SyncInventoryItem) or null when there is none.
        static Item ConsumeOneBag(Player player)
        {
            int bag = ModContent.ItemType<MechanicsBagItem>();
            for (int i = 0; i < player.inventory.Length; i++)
            {
                Item item = player.inventory[i];
                if (item == null || item.IsAir || item.type != bag)
                    continue;

                item.stack--;
                if (item.stack <= 0)
                    item.TurnToAir();

                return item;
            }

            return null;
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
                if (kind != StoryActionKind.TalkDryad)
                    ReplyAction(sender, kind, false, StoryActionFailure.CannotAct);
                return;
            }

            switch (kind)
            {
                case StoryActionKind.TakeBag:
                    HandleTakeBag(player, sender, npcIndex);
                    break;

                case StoryActionKind.TurnInBag:
                    HandleTurnInBag(player, sender, npcIndex);
                    break;

                case StoryActionKind.ClaimScrolls:
                    HandleClaimScrolls(player, sender, npcIndex);
                    break;

                case StoryActionKind.TalkDryad:
                    HandleTalkDryad(player, npcIndex);
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

        // Server / single-player. The server decides: the Mechanic is a real town NPC, the player is close, the shop is still locked, the server's
        // copy of the inventory really holds a Bag. Then it takes exactly one Bag (its own copy; a multiplayer client removes its copy after the
        // answer, see ApplyActionResult) and sets MechanicShopUnlocked, which reaches every client through the story flag sync.
        static void HandleTurnInBag(Player player, int sender, int npcIndex)
        {
            if (npcIndex < 0 || npcIndex >= Main.maxNPCs)
            {
                ReplyAction(sender, StoryActionKind.TurnInBag, false, StoryActionFailure.NoNpc);
                return;
            }

            NPC npc = Main.npc[npcIndex];
            if (!npc.active || npc.type != NPCID.Mechanic || !npc.townNPC)
            {
                ReplyAction(sender, StoryActionKind.TurnInBag, false, StoryActionFailure.NoNpc);
                return;
            }

            if (player.Distance(npc.Center) > System.Math.Max(1, InteractReachTiles) * 16f)
            {
                ReplyAction(sender, StoryActionKind.TurnInBag, false, StoryActionFailure.TooFar);
                return;
            }

            // The shop unlocks once: a second player's request in the same moment finds the flag already set and keeps their Bag.
            if (StorySystem.Get(StoryFlags.MechanicShopUnlocked))
            {
                ReplyAction(sender, StoryActionKind.TurnInBag, false, StoryActionFailure.Unavailable);
                return;
            }

            if (ConsumeOneBag(player) == null)
            {
                ReplyAction(sender, StoryActionKind.TurnInBag, false, StoryActionFailure.NoItem);
                return;
            }

            StorySystem.ServerSet(StoryFlags.MechanicShopUnlocked, true);

            FactorrariaNet.Log("[Story] " + player.name + " gave the Mechanic's Bag to the Mechanic (NPC #" + npcIndex + "): the shop is unlocked");
            ReplyAction(sender, StoryActionKind.TurnInBag, true, StoryActionFailure.None);
        }

        // Server / single-player (patch N7.1). The server decides: the Mechanic is a real town NPC, the player is close, she was rescued, and the
        // scrolls were not handed out yet (once per world, to the first player who asks; D9). ScrollsGiven is set BEFORE the items are spawned, so a
        // second player's request in the same moment is refused. The flag reaches every client through the story flag sync (so the "!" goes away on
        // the Mechanic and on the Dryad); the scrolls are real items spawned next to the player (QuickSpawnItem broadcasts by itself).
        static void HandleClaimScrolls(Player player, int sender, int npcIndex)
        {
            if (npcIndex < 0 || npcIndex >= Main.maxNPCs)
            {
                ReplyAction(sender, StoryActionKind.ClaimScrolls, false, StoryActionFailure.NoNpc);
                return;
            }

            NPC npc = Main.npc[npcIndex];
            if (!npc.active || npc.type != NPCID.Mechanic || !npc.townNPC)
            {
                ReplyAction(sender, StoryActionKind.ClaimScrolls, false, StoryActionFailure.NoNpc);
                return;
            }

            if (player.Distance(npc.Center) > System.Math.Max(1, InteractReachTiles) * 16f)
            {
                ReplyAction(sender, StoryActionKind.ClaimScrolls, false, StoryActionFailure.TooFar);
                return;
            }

            // D20 (N8.2): the Mechanic hands the scrolls out only after a real conversation with a Dryad (DryadTalked). A refused request changes nothing; the next talk asks again.
            if (!StorySystem.Get(StoryFlags.MechanicRescued) || StorySystem.Get(StoryFlags.ScrollsGiven) || !StorySystem.Get(StoryFlags.DryadTalked))
            {
                ReplyAction(sender, StoryActionKind.ClaimScrolls, false, StoryActionFailure.Unavailable);
                return;
            }

            StorySystem.ServerSet(StoryFlags.ScrollsGiven, true);
            StoryScrolls.Give(player, "FactorrariaStoryScrolls");

            FactorrariaNet.Log("[Story] " + player.name + " received the three super weapon scrolls from the Mechanic (NPC #" + npcIndex + ")");
            ReplyAction(sender, StoryActionKind.ClaimScrolls, true, StoryActionFailure.None);
        }

        // Server (or single-player). Patch N8.2 (rule D20): the first conversation with a live Dryad, by any player, sets DryadTalked. The NPC must be an
        // active town Dryad and the player within InteractReachTiles; anything else is ignored silently (the next conversation sends the request again).
        // No answer is sent. The flag travels to every client with the story flag sync, so the Mechanic's intro starts for everybody.
        static void HandleTalkDryad(Player player, int npcIndex)
        {
            if (StorySystem.Get(StoryFlags.DryadTalked))
                return;

            if (npcIndex < 0 || npcIndex >= Main.maxNPCs)
                return;

            NPC npc = Main.npc[npcIndex];
            if (!npc.active || npc.type != NPCID.Dryad || !npc.townNPC)
                return;

            if (player.Distance(npc.Center) > System.Math.Max(1, InteractReachTiles) * 16f)
                return;

            StorySystem.ServerSet(StoryFlags.DryadTalked, true);
            FactorrariaNet.Log("[Story] " + player.name + " talked to the Dryad (NPC #" + npcIndex + "): DryadTalked is on");
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
                else if (kind == StoryActionKind.ClaimScrolls)
                {
                    SoundEngine.PlaySound(SoundID.Grab);
                    Main.NewText("The Mechanic gives you three Sealed Scrolls.", 255, 240, 20);
                }
                else if (kind == StoryActionKind.TurnInBag)
                {
                    SoundEngine.PlaySound(SoundID.Grab);
                    Main.NewText("You give the Mechanic's Bag back. Her shop is open.", 255, 240, 20);

                    // Multiplayer: the server took the Bag out of its copy already; take it out of this copy too (single-player has one copy, the handler already did it).
                    if (Main.netMode == NetmodeID.MultiplayerClient)
                    {
                        Item slot = ConsumeOneBag(Main.LocalPlayer);
                        if (slot != null)
                            FactorrariaNet.SyncInventoryItem(Main.LocalPlayer, slot);
                    }

                    // The chat window is still open: replace the question with a thank-you (placeholder text).
                    if (Main.LocalPlayer.talkNPC >= 0)
                        Main.npcChatText = "My bag! Thank you, {PlayerName}. Take a look at what I have.".Replace("{PlayerName}", Main.LocalPlayer.name);
                }

                return;
            }

            bool turnIn = kind == StoryActionKind.TurnInBag;
            bool scrolls = kind == StoryActionKind.ClaimScrolls;
            switch (failure)
            {
                case StoryActionFailure.TooFar:
                    Main.NewText("You are too far away.", 200, 200, 200);
                    break;
                case StoryActionFailure.NoNpc:
                    Main.NewText(turnIn || scrolls ? "She is not here any more." : "It is gone.", 200, 200, 200);
                    break;
                case StoryActionFailure.Unavailable:
                    Main.NewText(scrolls ? "She has nothing more to give you." : turnIn ? "Her shop is already open." : "Someone already took it.", 200, 200, 200);
                    break;
                case StoryActionFailure.NoItem:
                    Main.NewText("You do not have the Mechanic's Bag.", 200, 200, 200);
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