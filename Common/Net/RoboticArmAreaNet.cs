using Factorraria.Common.Machines;
using Factorraria.Content.Tiles.Machines.ElectricalConsumers.RoboticArm;
using System;
using System.Collections.Concurrent;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // Robotic Arm working area as a request (roadmap patch 7b.5, finding A5).
    //
    // Authority: the server (or single-player) owns AreaX/Y/W/H of the arm; the worker reads it and the world save stores it.
    // The area editor (RoboticArmAreaEditorSystem) still previews a drag on the local replica every frame (no packet while dragging).
    // On mouse release it calls Commit, which sends ONE ArmAreaRequest with the final rectangle. The server validates it
    // (MachineRequest.Validate with the keep-reach extra: the arm exists and the player is alive and in reach; the sender must have the
    // arm's UI open (MachineDetailSystem.IsSubscribed); sane size; a minimum spacing per player), runs the same SetArea clamp, and
    // answers:
    //   - to the sender: ArmAreaChanged with the sender's sequence number and the arm's REAL area, accepted or not. The client always
    //     writes that area into its replica, so a rejected request puts the overlay back to the real area.
    //   - to the other players within RoboticArmNet.NearbyTiles of the arm (only when the area really changed): ArmAreaChanged with
    //     sequence 0 and the new area. Players further away keep an old replica until they open the arm's UI (RequestRefresh asks for the
    //     real area then) or a snapshot (join, message 86) arrives; the area is only visible through that UI.
    // A query request (RequestRefresh) changes nothing and only gets the real area back to the asker.
    //
    // Client rules: while this client drags the same arm, an incoming area is kept aside and applied when the drag ends (the preview is
    // never yanked); a broadcast of someone else's change that arrives while this client's own request is still waiting is ignored (the
    // answer to the waiting request follows and is the last word); an answer for an older request is ignored. No answer after two
    // seconds: the client sends a query once, and gives up if that is not answered either.
    //
    // Single-player and the host never send anything: the drag edited the real arm directly.
    public static class RoboticArmAreaNet
    {
        sealed class Pending
        {
            public int Seq;
            public Point16 Corner;
            public uint Tick;
            public bool IsQuery;
        }

        sealed class Result
        {
            public Point16 Corner;
            public int Seq;
            public bool Accepted;
            public bool HasArea;
            public int X, Y, W, H;
        }

        static readonly object pendingLock = new object();
        static Pending pending;
        static int nextSeq;
        static Result deferred;   // main thread only
        static readonly ConcurrentQueue<Result> results = new ConcurrentQueue<Result>();
        static readonly uint[] lastAcceptedTick = new uint[Main.maxPlayers];

        // Test aid (TEMPORARY, set by /farmsend areareject in the server console, remove in Phase 10): the server rejects the next request.
        public static bool RejectNext;

        // Temporary test counters (remove in Phase 10), printed by /farmsend status.
        public static int ServerAccepted;
        public static int ServerRejected;
        public static int ServerQueries;
        public static int ServerBroadcasts;
        public static int ClientSent;
        public static int ClientQueries;
        public static int ClientApplied;
        public static int ClientRestored;

        // ---------------------------------------------------------------- client: sending

        /// <summary>Called by the area editor when a drag ends. Sends one request if the area really changed. No-op outside a multiplayer client.</summary>
        public static void Commit(RoboticArmTileEntity arm, Microsoft.Xna.Framework.Rectangle startArea)
        {
            if (arm == null || Main.netMode != NetmodeID.MultiplayerClient)
                return;

            if (arm.AreaX == startArea.X && arm.AreaY == startArea.Y && arm.AreaW == startArea.Width && arm.AreaH == startArea.Height)
                return;

            ClientSent++;
            SendRequest(arm.Position, false, arm.AreaX, arm.AreaY, arm.AreaW, arm.AreaH);
        }

        /// <summary>Asks the server for the arm's real area (opening the arm's UI, a cancelled drag). No-op outside a multiplayer client.</summary>
        public static void RequestRefresh(RoboticArmTileEntity arm)
        {
            if (arm == null || Main.netMode != NetmodeID.MultiplayerClient)
                return;

            ClientQueries++;
            SendRequest(arm.Position, true, 0, 0, 0, 0);
        }

        static void SendRequest(Point16 corner, bool query, int ax, int ay, int aw, int ah)
        {
            nextSeq = (nextSeq & 0xFFFFFF) + 1;   // never 0: 0 marks a broadcast
            int seq = nextSeq;
            deferred = null;   // whatever was waiting is older than this request
            lock (pendingLock)
                pending = new Pending { Seq = seq, Corner = corner, Tick = Main.GameUpdateCount, IsQuery = query };

            FactorrariaNet.SendRequest(FactorrariaPacket.ArmAreaRequest, w =>
            {
                w.Write(corner.X);
                w.Write(corner.Y);
                w.Write7BitEncodedInt(seq);
                w.Write(query);
                if (!query)
                {
                    w.Write((short)ax);
                    w.Write((short)ay);
                    w.Write((short)aw);
                    w.Write((short)ah);
                }
            });
        }

        // ---------------------------------------------------------------- server

        public static void HandleRequest(BinaryReader reader, int sender)
        {
            short x = reader.ReadInt16();
            short y = reader.ReadInt16();
            int seq = reader.Read7BitEncodedInt();
            bool query = reader.ReadBoolean();
            int ax = 0, ay = 0, aw = 0, ah = 0;
            if (!query)
            {
                ax = reader.ReadInt16();
                ay = reader.ReadInt16();
                aw = reader.ReadInt16();
                ah = reader.ReadInt16();
            }

            if (Main.netMode != NetmodeID.Server)
                return;   // single-player and the host: the drag already changed the real arm

            float keepReach = ModContent.GetInstance<MachineDetailSystem>().KeepReachExtraTiles;
            RequestCheck check = MachineRequest.Validate(sender, x, y, keepReach, out BaseMachine machine, out Point16 corner);
            if (check == RequestCheck.Ignored)
                return;

            RoboticArmTileEntity arm = machine as RoboticArmTileEntity;
            if (check == RequestCheck.Refused)
            {
                Reject(sender, corner, seq, arm, machine == null ? "no machine there" : "out of reach");
                return;
            }

            if (arm == null)
            {
                Reject(sender, corner, seq, null, "the machine there is not a Robotic Arm");
                return;
            }

            if (query)
            {
                ServerQueries++;
                SendResult(sender, corner, seq, true, arm);
                return;
            }

            if (!ModContent.GetInstance<MachineDetailSystem>().IsSubscribed(sender, arm))
            {
                Reject(sender, corner, seq, arm, "the player does not have this arm open");
                return;
            }

            int maxSize = 2 * arm.AreaLimit + 1;
            if (aw < 1 || ah < 1 || aw > maxSize || ah > maxSize)
            {
                Reject(sender, corner, seq, arm, "bad size " + aw + "x" + ah);
                return;
            }

            uint now = Main.GameUpdateCount;
            int spacing = Math.Max(0, ModContent.GetInstance<RoboticArmAreaNetSystem>().MinRequestSpacingTicks);
            if (lastAcceptedTick[sender] != 0 && now - lastAcceptedTick[sender] < (uint)spacing)
            {
                Reject(sender, corner, seq, arm, "too fast (" + (now - lastAcceptedTick[sender]) + " ticks after the last accepted change)");
                return;
            }

            if (RejectNext)
            {
                RejectNext = false;
                Reject(sender, corner, seq, arm, "test: /farmsend areareject");
                return;
            }

            int oldX = arm.AreaX, oldY = arm.AreaY, oldW = arm.AreaW, oldH = arm.AreaH;
            arm.SetArea(ax, ay, aw, ah);   // the same clamp the editor used for the preview
            bool changed = oldX != arm.AreaX || oldY != arm.AreaY || oldW != arm.AreaW || oldH != arm.AreaH;

            lastAcceptedTick[sender] = now;
            ServerAccepted++;
            FactorrariaNet.Log("[ArmArea] " + Main.player[sender].name + " set the area of the arm at (" + corner.X + ", " + corner.Y + ") to ("
                + arm.AreaX + ", " + arm.AreaY + ") " + arm.AreaW + "x" + arm.AreaH + (changed ? "" : " (unchanged)"));

            SendResult(sender, corner, seq, true, arm);

            if (changed)
            {
                ServerBroadcasts++;
                int bx = arm.AreaX, by = arm.AreaY, bw = arm.AreaW, bh = arm.AreaH;
                FactorrariaNet.SendToNearby(arm.Shoulder, RoboticArmNet.NearbyTiles, FactorrariaPacket.ArmAreaChanged, w =>
                {
                    WriteResult(w, corner, 0, true, true, bx, by, bw, bh);
                }, sender);
            }
        }

        static void Reject(int player, Point16 corner, int seq, RoboticArmTileEntity arm, string why)
        {
            ServerRejected++;
            FactorrariaNet.Log("[ArmArea] rejected request " + seq + " from " + Main.player[player].name + " for the arm at (" + corner.X + ", " + corner.Y + "): " + why);
            SendResult(player, corner, seq, false, arm);
        }

        static void SendResult(int player, Point16 corner, int seq, bool accepted, RoboticArmTileEntity arm)
        {
            bool hasArea = arm != null;
            int ax = hasArea ? arm.AreaX : 0;
            int ay = hasArea ? arm.AreaY : 0;
            int aw = hasArea ? arm.AreaW : 0;
            int ah = hasArea ? arm.AreaH : 0;
            FactorrariaNet.SendToClient(player, FactorrariaPacket.ArmAreaChanged, w =>
            {
                WriteResult(w, corner, seq, accepted, hasArea, ax, ay, aw, ah);
            });
        }

        static void WriteResult(BinaryWriter w, Point16 corner, int seq, bool accepted, bool hasArea, int ax, int ay, int aw, int ah)
        {
            w.Write(corner.X);
            w.Write(corner.Y);
            w.Write7BitEncodedInt(seq);
            w.Write(accepted);
            w.Write(hasArea);
            w.Write((short)ax);
            w.Write((short)ay);
            w.Write((short)aw);
            w.Write((short)ah);
        }

        // ---------------------------------------------------------------- client: the answer

        // May run on the network thread: it only queues. Update (main thread) does the work.
        public static void HandleChanged(BinaryReader reader, int sender)
        {
            var r = new Result();
            short x = reader.ReadInt16();
            short y = reader.ReadInt16();
            r.Corner = new Point16(x, y);
            r.Seq = reader.Read7BitEncodedInt();
            r.Accepted = reader.ReadBoolean();
            r.HasArea = reader.ReadBoolean();
            r.X = reader.ReadInt16();
            r.Y = reader.ReadInt16();
            r.W = reader.ReadInt16();
            r.H = reader.ReadInt16();
            results.Enqueue(r);
        }

        /// <summary>Client main thread, every tick.</summary>
        public static void Update(int timeoutTicks)
        {
            while (results.TryDequeue(out Result result))
                ProcessResult(result);

            if (deferred != null)
            {
                Result d = deferred;
                deferred = null;
                Apply(d);   // puts itself back aside while the editor still drags this arm
            }

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

                if (p.IsQuery)
                {
                    FactorrariaNet.Log("[ArmArea] no answer to the query " + p.Seq + " either: giving up");
                    return;
                }

                FactorrariaNet.Log("[ArmArea] no answer for request " + p.Seq + " after " + timeoutTicks + " ticks: asking the server for the real area");
                ClientQueries++;
                SendRequest(p.Corner, true, 0, 0, 0, 0);
            }
        }

        static void ProcessResult(Result r)
        {
            lock (pendingLock)
            {
                if (r.Seq != 0)
                {
                    if (pending == null || pending.Seq != r.Seq)
                        return;   // an answer for a request that a newer one already replaced

                    pending = null;
                }
                else if (pending != null && pending.Corner == r.Corner)
                {
                    return;   // someone else's change while ours is waiting: our answer follows and is the last word
                }
            }

            Apply(r);
        }

        static void Apply(Result r)
        {
            if (!r.HasArea)
            {
                FactorrariaNet.Log("[ArmArea] the server has no arm at (" + r.Corner.X + ", " + r.Corner.Y + "): nothing to restore");
                return;
            }

            if (!TileEntity.ByPosition.TryGetValue(r.Corner, out TileEntity entity) || !(entity is RoboticArmTileEntity arm))
                return;

            if (ModContent.GetInstance<RoboticArmAreaEditorSystem>().IsDraggingArm(arm))
            {
                deferred = r;
                return;
            }

            arm.SetArea(r.X, r.Y, r.W, r.H);
            ClientApplied++;
            if (!r.Accepted)
            {
                ClientRestored++;
                FactorrariaNet.Log("[ArmArea] request " + r.Seq + " was rejected: the overlay shows the real area again (" + arm.AreaX + ", " + arm.AreaY + ") " + arm.AreaW + "x" + arm.AreaH);
            }
        }

        public static void Reset()
        {
            lock (pendingLock)
                pending = null;

            deferred = null;
            while (results.TryDequeue(out _)) { }
            RejectNext = false;
            Array.Clear(lastAcceptedTick, 0, lastAcceptedTick.Length);
        }

        // Test aid (temporary, used by /farmsend status).
        public static string Describe()
        {
            return Main.netMode == NetmodeID.Server
                ? "armArea: accepted " + ServerAccepted + ", rejected " + ServerRejected + ", queries " + ServerQueries + ", broadcasts " + ServerBroadcasts
                : "armArea: sent " + ClientSent + ", queries " + ClientQueries + ", applied " + ClientApplied + ", restored " + ClientRestored;
        }
    }

    public class RoboticArmAreaNetSystem : ModSystem
    {
        // Tuning knobs (instance fields on purpose, so hot reload can change them).
        public int AnswerTimeoutTicks = 120;      // how long a client waits for the answer to an area request
        public int MinRequestSpacingTicks = 5;    // a player's area changes closer together than this are rejected (one request per drag release)

        public override void Load()
        {
            FactorrariaNet.Register(FactorrariaPacket.ArmAreaRequest, PacketDirection.ToServer, RoboticArmAreaNet.HandleRequest, 2);
            FactorrariaNet.Register(FactorrariaPacket.ArmAreaChanged, PacketDirection.ToClient, RoboticArmAreaNet.HandleChanged);
        }

        public override void PostUpdateEverything()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                RoboticArmAreaNet.Update(AnswerTimeoutTicks);
        }

        public override void OnWorldUnload()
        {
            RoboticArmAreaNet.Reset();
        }

        public override void Unload()
        {
            RoboticArmAreaNet.Reset();
        }
    }
}
