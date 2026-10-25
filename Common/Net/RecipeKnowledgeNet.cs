using Factorraria.Common.Knowledge;
using Factorraria.Common.Machines;
using Factorraria.Common.Systems;
using Factorraria.Content.Items.Discovery;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // Recipe knowledge (roadmap patch 3.2, findings R2, I2, I3, M10, U12). The WORLD knows three sets: unlocked recipes (read from
    // parchments), crafted recipes (a machine finished them) and seen ingredient item types. The server owns them.
    //  - join: provider "recipe-knowledge" sends all three sets, the client replaces its copy and both version counters are bumped
    //    (so the recipe book, the machine browsers and RecipeVisibility rebuild)
    //  - crafted: BaseMachine.FinishRecipe runs on the server and calls ServerMarkCrafted, which broadcasts KnowledgeDelta
    //  - seen: RecipeVisibility scans the local inventory; every newly seen ingredient type is marked here at once and queued,
    //    and a few times a second the queue goes to the server as ONE SeenReport; the server keeps what is new and broadcasts
    //    SeenDelta to everyone else (one player holding an ingredient reveals it for everybody)
    //  - parchment: the client sends ReadParchmentRequest(pool). The server checks that the sender holds such a parchment, rolls
    //    with its own RNG against its own sets, unlocks, broadcasts KnowledgeDelta, then answers the reader with
    //    ReadParchmentResult. Only a successful answer consumes the parchment on the reader's client; a failed read keeps it.
    // Single-player: SendRequest runs the server handler right here and the "answer" is applied directly, no packets.
    public class RecipeKnowledgeNetSystem : ModSystem
    {
        public override void Load()
        {
            FactorrariaNet.Register(FactorrariaPacket.KnowledgeDelta, PacketDirection.ToClient, RecipeKnowledgeNet.HandleKnowledgeDelta);
            FactorrariaNet.Register(FactorrariaPacket.SeenDelta, PacketDirection.ToClient, RecipeKnowledgeNet.HandleSeenDelta);
            FactorrariaNet.Register(FactorrariaPacket.SeenReport, PacketDirection.ToServer, RecipeKnowledgeNet.HandleSeenReport, 2);
            FactorrariaNet.Register(FactorrariaPacket.ReadParchmentRequest, PacketDirection.ToServer, RecipeKnowledgeNet.HandleReadRequest, 3);
            FactorrariaNet.Register(FactorrariaPacket.ReadParchmentResult, PacketDirection.ToClient, RecipeKnowledgeNet.HandleReadResult);
            JoinSync.Register("recipe-knowledge", RecipeKnowledgeNet.WriteAll, RecipeKnowledgeNet.ReadAll);
        }

        public override void PostUpdateEverything()
        {
            RecipeKnowledgeNet.Update();
        }

        public override void OnWorldUnload()
        {
            RecipeKnowledgeNet.ClearPending();
        }
    }

    /// <summary>What a parchment read came to, as seen by RecipeParchmentItem.RightClick.</summary>
    public enum ParchmentReadResult
    {
        Pending,   // multiplayer client: the request was sent, the answer comes later
        Read,      // done, the parchment is used up (single-player, answered at once)
        Failed     // nothing was unlocked, keep the parchment
    }

    public enum ParchmentFailure : byte
    {
        None = 0,
        NoParchment = 1,   // the server's copy of the player's inventory holds no such parchment (or the pool name is bad)
        Exhausted = 2,     // nothing left in the whole book to unlock
        Unresolved = 3,    // the rolled key is not in the catalog (should not happen)
        CannotRead = 4     // the player is dead or not in the game
    }

    public static class RecipeKnowledgeNet
    {
        // Tunables (not const, so hot reload can change them).
        public static int SeenBatchTicks = 30;       // client: how often queued seen item types are sent
        public static int MaxSeenPerPacket = 64;     // at most 255 (the count is one byte)
        public static int ReadTimeoutTicks = 300;    // client: a read request with no answer after this long is forgotten
        public static int MaxPoolNameLength = 64;
        public static int MaxJoinEntries = 200000;

        enum KnowledgeKind : byte
        {
            Unlocked = 0,
            Crafted = 1
        }

        // Client state.
        static readonly HashSet<int> pendingSeen = new();
        static bool readPending;
        static uint readPendingTick;
        static ParchmentReadResult immediateResult;

        /// <summary>Client: true while a ReadParchmentRequest waits for its answer (the parchment cannot be read again until it comes).</summary>
        public static bool ReadPending => readPending && Main.GameUpdateCount - readPendingTick < (uint)System.Math.Max(1, ReadTimeoutTicks);

        public static void ClearPending()
        {
            pendingSeen.Clear();
            readPending = false;
        }

        // ---------------------------------------------------------------- crafted (server)

        /// <summary>
        /// Server or single-player: marks a recipe as crafted for the whole world and tells every client. A multiplayer client does
        /// nothing (it learns it from the server's KnowledgeDelta).
        /// </summary>
        public static void ServerMarkCrafted(string catalogKey)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            if (!RecipeKnowledgeSystem.MarkCrafted(catalogKey))
                return;

            if (Main.netMode == NetmodeID.Server)
                FactorrariaNet.Log("[Knowledge] recipe crafted: " + catalogKey + " (crafted now " + RecipeKnowledgeSystem.CraftedKeys.Count + ")");

            SendKnowledgeDelta(KnowledgeKind.Crafted, catalogKey);
        }

        static void SendKnowledgeDelta(KnowledgeKind kind, string catalogKey)
        {
            FactorrariaNet.Broadcast(FactorrariaPacket.KnowledgeDelta, writer =>
            {
                writer.Write((byte)kind);
                writer.Write(catalogKey);
            });
        }

        // Client.
        public static void HandleKnowledgeDelta(BinaryReader reader, int sender)
        {
            KnowledgeKind kind = (KnowledgeKind)reader.ReadByte();
            string key = reader.ReadString();

            if (string.IsNullOrEmpty(key))
                return;

            if (kind == KnowledgeKind.Unlocked)
                RecipeKnowledgeSystem.Unlock(key);
            else if (kind == KnowledgeKind.Crafted)
                RecipeKnowledgeSystem.MarkCrafted(key);
        }

        // ---------------------------------------------------------------- seen items

        /// <summary>
        /// Called by RecipeVisibility for every recipe input the local player holds. Marks it seen here at once (so the UI reacts
        /// without waiting) and, on a multiplayer client, queues it for the next SeenReport. Only types that are NEW to this
        /// process are queued, so a held item is reported once.
        /// </summary>
        public static void ReportSeen(int itemType)
        {
            if (!RecipeKnowledgeSystem.MarkSeen(itemType))
                return;

            if (Main.netMode == NetmodeID.MultiplayerClient)
                pendingSeen.Add(itemType);
        }

        /// <summary>Per tick (ModSystem.PostUpdateEverything): a multiplayer client sends its queued seen types, a few times a second.</summary>
        public static void Update()
        {
            if (Main.netMode != NetmodeID.MultiplayerClient || pendingSeen.Count == 0)
                return;

            if (Main.GameUpdateCount % (uint)System.Math.Max(1, SeenBatchTicks) != 0)
                return;

            int perPacket = System.Math.Clamp(MaxSeenPerPacket, 1, 255);
            var batch = new List<int>(perPacket);

            foreach (int type in pendingSeen)
            {
                batch.Add(type);
                if (batch.Count >= perPacket)
                {
                    SendSeenReport(batch);
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
                SendSeenReport(batch);

            pendingSeen.Clear();
        }

        static void SendSeenReport(List<int> types)
        {
            FactorrariaNet.SendRequest(FactorrariaPacket.SeenReport, writer =>
            {
                writer.Write((byte)types.Count);
                foreach (int type in types)
                    writer.Write(type);
            });
        }

        // Server.
        public static void HandleSeenReport(BinaryReader reader, int sender)
        {
            int count = reader.ReadByte();

            if (sender < 0 || sender >= Main.maxPlayers || count > MaxSeenPerPacket)
                return;

            var added = new List<int>();
            for (int i = 0; i < count; i++)
            {
                int type = reader.ReadInt32();

                // Only real item types that some recipe takes as an input. (The server cannot see what the player really holds
                // between two inventory syncs, so it trusts the report, like vanilla trusts a client's item use.)
                if (type <= 0 || type >= ItemLoader.ItemCount || !RecipeVisibility.IsIngredient(type))
                    continue;

                if (RecipeKnowledgeSystem.MarkSeen(type))
                    added.Add(type);
            }

            if (added.Count == 0)
                return;

            FactorrariaNet.Log("[Knowledge] " + Main.player[sender].name + " revealed " + added.Count + " ingredient item type(s) (seen now " + RecipeKnowledgeSystem.SeenItems.Count + ")");

            FactorrariaNet.Broadcast(FactorrariaPacket.SeenDelta, writer =>
            {
                writer.Write((byte)added.Count);
                foreach (int type in added)
                    writer.Write(type);
            }, sender);
        }

        // Client.
        public static void HandleSeenDelta(BinaryReader reader, int sender)
        {
            int count = reader.ReadByte();
            for (int i = 0; i < count; i++)
            {
                int type = reader.ReadInt32();
                RecipeKnowledgeSystem.MarkSeen(type);   // not ReportSeen: it would echo straight back to the server
            }
        }

        // ---------------------------------------------------------------- parchment reading

        /// <summary>
        /// Owner side, called by RecipeParchmentItem.RightClick. Single-player: runs the server logic now and returns Read or
        /// Failed. Multiplayer client: sends the request and returns Pending; the answer arrives in HandleReadResult.
        /// </summary>
        public static ParchmentReadResult RequestReadParchment(string poolName)
        {
            string pool = poolName ?? "";
            bool multiplayerClient = Main.netMode == NetmodeID.MultiplayerClient;

            immediateResult = ParchmentReadResult.Pending;
            if (multiplayerClient)
            {
                readPending = true;
                readPendingTick = Main.GameUpdateCount;
            }

            FactorrariaNet.SendRequest(FactorrariaPacket.ReadParchmentRequest, writer => writer.Write(pool));

            if (multiplayerClient)
                return ParchmentReadResult.Pending;

            // Single-player: the handler already ran. If it threw, nothing was unlocked.
            return immediateResult == ParchmentReadResult.Read ? ParchmentReadResult.Read : ParchmentReadResult.Failed;
        }

        static bool HoldsParchment(Player player, string pool)
        {
            foreach (Item item in player.inventory)
            {
                if (item != null && !item.IsAir && item.ModItem is RecipeParchmentItem parchment && (parchment.PoolName ?? "") == pool)
                    return true;
            }

            return false;
        }

        // Server (or single-player, where sender is the local player).
        public static void HandleReadRequest(BinaryReader reader, int sender)
        {
            string pool = reader.ReadString();

            if (sender < 0 || sender >= Main.maxPlayers)
                return;

            Player player = Main.player[sender];

            if (!player.active || player.dead)
            {
                ReplyRead(sender, false, null, ParchmentFailure.CannotRead, pool);
                return;
            }

            if (pool.Length > MaxPoolNameLength || !HoldsParchment(player, pool))
            {
                ReplyRead(sender, false, null, ParchmentFailure.NoParchment, pool);
                return;
            }

            // The roll uses the server's RNG and the server's sets. An unknown pool name falls back to the whole book (TryRoll).
            if (!ScrollPools.TryRoll(pool.Length == 0 ? null : pool, Main.rand, out string key))
            {
                ReplyRead(sender, false, null, ParchmentFailure.Exhausted, pool);
                return;
            }

            if (!RecipeCatalog.Exists(key))
            {
                ReplyRead(sender, false, null, ParchmentFailure.Unresolved, pool);
                return;
            }

            if (RecipeKnowledgeSystem.Unlock(key))
            {
                if (Main.netMode == NetmodeID.Server)
                    FactorrariaNet.Log("[Knowledge] " + player.name + " read a parchment (pool " + (pool.Length == 0 ? "none" : pool) + "): unlocked " + key + " (unlocked now " + RecipeKnowledgeSystem.UnlockedKeys.Count + ")");

                // Everyone, the reader included, so every client's UI rebuilds. Sent before the answer, so the unlock is already
                // there when the reader's book opens on that page.
                SendKnowledgeDelta(KnowledgeKind.Unlocked, key);
            }

            ReplyRead(sender, true, key, ParchmentFailure.None, pool);
        }

        static void ReplyRead(int sender, bool success, string key, ParchmentFailure failure, string pool)
        {
            if (Main.netMode == NetmodeID.Server)
            {
                FactorrariaNet.SendToClient(sender, FactorrariaPacket.ReadParchmentResult, writer =>
                {
                    writer.Write(success);
                    writer.Write(key ?? "");
                    writer.Write((byte)failure);
                    writer.Write(pool ?? "");
                });
            }
            else
            {
                // Single-player: no network, apply the answer here.
                ApplyReadResult(success, key, failure, pool);
            }
        }

        // Client.
        public static void HandleReadResult(BinaryReader reader, int sender)
        {
            bool success = reader.ReadBoolean();
            string key = reader.ReadString();
            ParchmentFailure failure = (ParchmentFailure)reader.ReadByte();
            string pool = reader.ReadString();

            ApplyReadResult(success, key.Length == 0 ? null : key, failure, pool);
        }

        static void ApplyReadResult(bool success, string key, ParchmentFailure failure, string pool)
        {
            readPending = false;

            if (Main.dedServ)
                return;

            if (!success)
            {
                immediateResult = ParchmentReadResult.Failed;

                switch (failure)
                {
                    case ParchmentFailure.Exhausted:
                        Main.NewText("The ink fades, but there is nothing left in the world for this parchment to teach.", 200, 200, 200);
                        break;
                    case ParchmentFailure.Unresolved:
                        Main.NewText("The writing on this parchment has faded beyond reading.", 200, 200, 200);
                        break;
                    case ParchmentFailure.NoParchment:
                        Main.NewText("The server could not find that parchment in your inventory. Try again in a moment.", 200, 200, 200);
                        break;
                    default:
                        Main.NewText("You cannot read this right now.", 200, 200, 200);
                        break;
                }

                return;
            }

            immediateResult = ParchmentReadResult.Read;

            // Multiplayer: the parchment is used up now that the server confirmed (single-player: vanilla consumes it after RightClick).
            if (Main.netMode == NetmodeID.MultiplayerClient)
                ConsumeOneParchment(Main.LocalPlayer, pool);

            // Open the recipe book on the page that was just unlocked.
            if (key != null)
                RecipeBookSystem.OpenAt(key);
        }

        static void ConsumeOneParchment(Player player, string pool)
        {
            for (int i = 0; i < player.inventory.Length; i++)
            {
                Item item = player.inventory[i];
                if (item.IsAir || item.ModItem is not RecipeParchmentItem parchment || (parchment.PoolName ?? "") != pool)
                    continue;

                item.stack--;
                if (item.stack <= 0)
                    item.TurnToAir();

                // Vanilla would send the changed slot on its own; do it now so the server's copy cannot still hold the parchment.
                FactorrariaNet.SyncInventoryItem(player, item);
                return;
            }

            // Not in the inventory any more (dropped or moved during the round trip): nothing to consume. The unlock stands.
        }

        // ---------------------------------------------------------------- join provider ("recipe-knowledge")

        public static void WriteAll(BinaryWriter writer)
        {
            writer.Write(RecipeKnowledgeSystem.UnlockedKeys.Count);
            foreach (string key in RecipeKnowledgeSystem.UnlockedKeys)
                writer.Write(key);

            writer.Write(RecipeKnowledgeSystem.CraftedKeys.Count);
            foreach (string key in RecipeKnowledgeSystem.CraftedKeys)
                writer.Write(key);

            writer.Write(RecipeKnowledgeSystem.SeenItems.Count);
            foreach (int type in RecipeKnowledgeSystem.SeenItems)
                writer.Write(type);

            FactorrariaNet.Log("join provider recipe-knowledge: sent " + RecipeKnowledgeSystem.UnlockedKeys.Count + " unlocked, " + RecipeKnowledgeSystem.CraftedKeys.Count + " crafted, " + RecipeKnowledgeSystem.SeenItems.Count + " seen");
        }

        public static void ReadAll(BinaryReader reader)
        {
            var unlockedKeys = ReadKeys(reader, "unlocked");
            var craftedKeys = ReadKeys(reader, "crafted");

            int seenCount = reader.ReadInt32();
            if (seenCount < 0 || seenCount > MaxJoinEntries)
                throw new InvalidDataException("seen item count " + seenCount);

            var seen = new List<int>(seenCount);
            for (int i = 0; i < seenCount; i++)
                seen.Add(reader.ReadInt32());

            RecipeKnowledgeSystem.ReplaceAll(unlockedKeys, craftedKeys, seen);
            FactorrariaNet.Log("join provider recipe-knowledge: " + unlockedKeys.Count + " unlocked, " + craftedKeys.Count + " crafted, " + seenCount + " seen");
        }

        static List<string> ReadKeys(BinaryReader reader, string what)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > MaxJoinEntries)
                throw new InvalidDataException(what + " recipe count " + count);

            var keys = new List<string>(count);
            for (int i = 0; i < count; i++)
                keys.Add(reader.ReadString());

            return keys;
        }
    }
}