using Factorraria.Common.Knowledge;
using Factorraria.Common.Machines;
using System;
using System.Collections.Concurrent;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // Recipe selection (roadmap patch 6.3, finding U4).
    //
    // Authority: the server (or single-player) owns ManualGroup, the recipe group the player picked in the recipe browser (null = auto).
    // The pick travels as the group's STABLE catalog key (RecipeCatalog.KeyFor: machine name + output item name or liquid name, "" = auto),
    // never as a list index, because every side builds its recipe registries separately in PostAddRecipes.
    //
    // Client (multiplayer): the browser calls Select. The pick is applied to the local replica at once (the browser shows it without
    // waiting) and sent as MachineSelectRecipeRequest. The server validates it (MachineRequest.Validate with the keep-reach extra, the
    // sender must have the machine open, the machine must have a recipe book, the key must name a group of THIS machine's book), applies
    // SetManualGroup, calls MachineDetailSystem.Push (so every subscriber's replica follows, through the manual group and running recipe
    // the detail channel of 5.3 already carries) and answers with MachineSelectRecipeResult, which always holds the server's real key.
    // On a reject the client puts that key back, so a refused pick never stays on screen.
    //
    // While a pick is waiting for its answer, an arriving detail packet (it still carries the old pick) does not undo it
    // (ReapplyPending). A newer pick replaces the waiting one; answers for older picks are ignored, the last answer holds the truth.
    // No answer after two seconds (the request was dropped): the client asks the server for a fresh detail (an open request again).
    //
    // Single-player and the host do not send anything: the pick edits the real machine directly.
    public static class MachineRecipeSelect
    {
        sealed class Pending
        {
            public int Seq;
            public Point16 Corner;
            public RecipeOutputGroup Group;
            public uint Tick;
        }

        sealed class Result
        {
            public Point16 Corner;
            public int Seq;
            public bool Accepted;
            public bool HasKey;
            public string Key;
        }

        static readonly object pendingLock = new object();
        static Pending pending;
        static int nextSeq;
        static readonly ConcurrentQueue<Result> results = new ConcurrentQueue<Result>();

        // Test aid (temporary, set by /fslot recipe, remove in Phase 10): the next pick is sent with a key that names no recipe group, so
        // the server rejects it and the rollback can be tested on demand.
        public static bool CorruptNextRequest;

        // Temporary test counters (remove in Phase 10).
        public static int ServerAccepted;
        public static int ServerRejected;
        public static int ClientSent;
        public static int ClientRolledBack;

        // ---------------------------------------------------------------- shared

        /// <summary>The group of this machine's recipe book that has this catalog key, or null (also for "" = auto).</summary>
        public static RecipeOutputGroup ResolveGroup(BaseMachine machine, string key)
        {
            RecipeBook book = machine?.Recipes;
            string name = RecipeCatalog.NameOf(book);
            if (name == null || string.IsNullOrEmpty(key))
                return null;

            foreach (RecipeOutputGroup group in book.Groups)
            {
                if (RecipeCatalog.KeyFor(name, group) == key)
                    return group;
            }
            return null;
        }

        static string KeyOf(BaseMachine machine, RecipeOutputGroup group)
        {
            string name = RecipeCatalog.NameOf(machine.Recipes);
            return group == null || name == null ? "" : RecipeCatalog.KeyFor(name, group);
        }

        // ---------------------------------------------------------------- client: the browser's pick

        /// <summary>What the recipe browser calls instead of SetManualGroup. null = back to auto-detect.</summary>
        public static void Select(BaseMachine machine, RecipeOutputGroup group)
        {
            if (machine == null)
                return;

            machine.SetManualGroup(group);   // single-player and the host: this is the whole change; a client: the prediction

            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            if (RecipeCatalog.NameOf(machine.Recipes) == null)
                return;   // a machine whose book is not in the catalog has no stable key (the server could not resolve it either)

            nextSeq = (nextSeq + 1) & 0xFFFFFF;
            var p = new Pending { Seq = nextSeq, Corner = machine.Position, Group = group, Tick = Main.GameUpdateCount };
            lock (pendingLock)
                pending = p;

            string key = KeyOf(machine, group);
            if (CorruptNextRequest)
            {
                CorruptNextRequest = false;
                key = "i|__no_such_recipe__";
                FactorrariaNet.Log("[SelectRecipe] test: pick " + p.Seq + " is sent with a key that names no recipe on purpose");
            }

            ClientSent++;
            Point16 corner = p.Corner;
            int seq = p.Seq;
            FactorrariaNet.SendRequest(FactorrariaPacket.MachineSelectRecipeRequest, w =>
            {
                w.Write(corner.X);
                w.Write(corner.Y);
                w.Write7BitEncodedInt(seq);
                w.Write(key);
            });
        }

        // ---------------------------------------------------------------- server

        public static void HandleRequest(BinaryReader reader, int sender)
        {
            short x = reader.ReadInt16();
            short y = reader.ReadInt16();
            int seq = reader.Read7BitEncodedInt();
            string key = reader.ReadString();

            if (Main.netMode != NetmodeID.Server)
                return;   // single-player and the host: the browser already changed the real machine

            if (key.Length > 200)
            {
                Reject(sender, new Point16(x, y), seq, null, "key too long");
                return;
            }

            float keepReach = ModContent.GetInstance<MachineDetailSystem>().KeepReachExtraTiles;
            RequestCheck check = MachineRequest.Validate(sender, x, y, keepReach, out BaseMachine machine, out Point16 corner);
            if (check == RequestCheck.Ignored)
                return;

            if (check == RequestCheck.Refused)
            {
                Reject(sender, corner, seq, machine, machine == null ? "no machine there" : "out of reach");
                return;
            }

            if (!ModContent.GetInstance<MachineDetailSystem>().IsSubscribed(sender, machine))
            {
                Reject(sender, corner, seq, machine, "the player does not have this machine open");
                return;
            }

            if (machine.Recipes == null || RecipeCatalog.NameOf(machine.Recipes) == null)
            {
                Reject(sender, corner, seq, machine, "this machine has no recipe list");
                return;
            }

            RecipeOutputGroup group = ResolveGroup(machine, key);
            if (key.Length > 0 && group == null)
            {
                Reject(sender, corner, seq, machine, "no recipe group with key '" + key + "' in this machine's book");
                return;
            }

            if (!ReferenceEquals(machine.ManualGroup, group))
            {
                machine.SetManualGroup(group);
                ServerAccepted++;
                FactorrariaNet.Log("[SelectRecipe] " + Main.player[sender].name + " picked '" + (key.Length == 0 ? "auto" : key) + "' on " + machine.GetType().Name + " at (" + corner.X + ", " + corner.Y + ")");
                ModContent.GetInstance<MachineDetailSystem>().Push(machine);   // detail first, then the answer
            }

            SendResult(sender, corner, seq, true, machine);
        }

        static void Reject(int player, Point16 corner, int seq, BaseMachine machine, string why)
        {
            ServerRejected++;
            FactorrariaNet.Log("[SelectRecipe] rejected pick " + seq + " from " + Main.player[player].name + " at (" + corner.X + ", " + corner.Y + "): " + why);
            SendResult(player, corner, seq, false, machine);
        }

        static void SendResult(int player, Point16 corner, int seq, bool accepted, BaseMachine machine)
        {
            bool hasKey = machine != null && machine.Recipes != null && RecipeCatalog.NameOf(machine.Recipes) != null;
            string key = hasKey ? KeyOf(machine, machine.ManualGroup) : "";
            FactorrariaNet.SendToClient(player, FactorrariaPacket.MachineSelectRecipeResult, w =>
            {
                w.Write(corner.X);
                w.Write(corner.Y);
                w.Write7BitEncodedInt(seq);
                w.Write(accepted);
                w.Write(hasKey);
                w.Write(key);
            });
        }

        // ---------------------------------------------------------------- client: the answer

        // May run on the network thread: it only queues. Update (main thread) does the work.
        public static void HandleResult(BinaryReader reader, int sender)
        {
            short x = reader.ReadInt16();
            short y = reader.ReadInt16();
            int seq = reader.Read7BitEncodedInt();
            bool accepted = reader.ReadBoolean();
            bool hasKey = reader.ReadBoolean();
            string key = reader.ReadString();
            results.Enqueue(new Result { Corner = new Point16(x, y), Seq = seq, Accepted = accepted, HasKey = hasKey, Key = key });
        }

        /// <summary>Client main thread, every tick.</summary>
        public static void Update(int timeoutTicks)
        {
            while (results.TryDequeue(out Result result))
                ProcessResult(result);

            Pending p;
            lock (pendingLock)
                p = pending;

            if (p != null && Main.GameUpdateCount - p.Tick > (uint)Math.Max(1, timeoutTicks))
            {
                lock (pendingLock)
                {
                    if (ReferenceEquals(pending, p))
                        pending = null;
                }
                FactorrariaNet.Log("[SelectRecipe] no answer for pick " + p.Seq + " after " + timeoutTicks + " ticks: asking the server for a fresh detail");
                MachineDetail.SendSubscribe(p.Corner, true);
            }
        }

        static void ProcessResult(Result r)
        {
            Pending p;
            lock (pendingLock)
            {
                if (pending == null || pending.Seq != r.Seq)
                    return;   // an answer for a pick that a newer pick already replaced

                p = pending;
                pending = null;
            }

            if (r.Accepted || !r.HasKey)
                return;

            if (TileEntity.ByPosition.TryGetValue(p.Corner, out TileEntity entity) && entity is BaseMachine machine)
            {
                ClientRolledBack++;
                RecipeOutputGroup real = ResolveGroup(machine, r.Key);
                if (!ReferenceEquals(machine.ManualGroup, real))
                    machine.SetManualGroup(real);
                FactorrariaNet.Log("[SelectRecipe] pick " + p.Seq + " was rejected: the browser shows the server's pick again ('" + (r.Key.Length == 0 ? "auto" : r.Key) + "')");
            }
        }

        /// <summary>Network thread. A detail packet has just written the server's (older) pick into the replica; a pick still waiting for its answer is put back.</summary>
        public static void ReapplyPending(BaseMachine machine)
        {
            Pending p;
            lock (pendingLock)
                p = pending;

            if (p == null || machine == null || p.Corner != machine.Position)
                return;

            if (!ReferenceEquals(machine.ManualGroup, p.Group))
                machine.SetManualGroup(p.Group);
        }

        public static void Reset()
        {
            lock (pendingLock)
                pending = null;

            while (results.TryDequeue(out _)) { }
            CorruptNextRequest = false;
        }

        // Test aid (temporary, used by /fslot).
        public static string Describe()
        {
            return Main.netMode == NetmodeID.Server
                ? "recipe picks: accepted " + ServerAccepted + ", rejected " + ServerRejected
                : "recipe picks: sent " + ClientSent + ", rolled back " + ClientRolledBack;
        }
    }

    public class MachineRecipeSelectSystem : ModSystem
    {
        // Tuning knob (instance field on purpose, so hot reload can change it): how long a client waits for the answer to a pick.
        public int AnswerTimeoutTicks = 120;

        public override void Load()
        {
            FactorrariaNet.Register(FactorrariaPacket.MachineSelectRecipeRequest, PacketDirection.ToServer, MachineRecipeSelect.HandleRequest);
            FactorrariaNet.Register(FactorrariaPacket.MachineSelectRecipeResult, PacketDirection.ToClient, MachineRecipeSelect.HandleResult);
        }

        public override void PostUpdateEverything()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                MachineRecipeSelect.Update(AnswerTimeoutTicks);
        }

        public override void OnWorldUnload()
        {
            MachineRecipeSelect.Reset();
        }

        public override void Unload()
        {
            MachineRecipeSelect.Reset();
        }
    }
}