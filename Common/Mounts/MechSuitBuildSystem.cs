using Factorraria.Common.Net;
using Factorraria.Common.Systems;
using Factorraria.Content.Items.Mounts;
using Factorraria.Content.Tiles.Machines.ElectricalConsumers.RoboticArm;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Mounts
{
    // Mech Suit build tool, patch M3 (docs/mech_suit_builder/ROADMAP.md, sections 3 and 4): the build job. Runs where the world is simulated
    // (dedicated server or single-player); a multiplayer client never runs it.
    //
    // Authority and sync (rule 9): the owner's client sends ONE request (source rectangle + destination origin, MechSuitBuildNet). The server
    // checks it (player alive, in the suit, sane rectangle), reads the source region from its own world (MechSuitBlueprint.Scan), keeps the
    // job, moves the two builder arms (MechSuitBuilderArms: RoboticArmMotion model, player shoulder as pivot), places tiles and walls with
    // WorldPlacement, takes the material from the owner's inventory and broadcasts the arm state, a tile square and the place sound.
    // Single-player runs the same code; there are just no packets.
    //
    // Rules (roadmap section 4): no build order. Each idle arm picks the closest tile of the job that is inside its reach, has material in the
    // owner's inventory, is not blocked by another block and passes the placement check (a tile that fails only the support check stays pending
    // and is retried, because the arms place other tiles around it). Switching material: the arm folds back to the player, "grabs" the next
    // item, then reaches out again. The job ends when nothing left can be placed (all done, or blocked / out of material / unsupported),
    // when the player leaves the suit, dies or disconnects, or after IdleTimeoutTicks without a placement.
    // Patch M5: the job places objects too (one-tile and multi-tile, chests, machines). One pending entry per placeable unit (MechSuitBlueprint.Block.Head):
    // a plain block, a wall or a whole object; an object is placed by WorldPlacement.PlaceObject (the route a player's item takes, so chests get their
    // empty Main.chest entry and machines their TileEntity), costs one item, and counts as built when its whole footprint holds the same type and style.
    public class MechSuitBuildSystem : ModSystem
    {
        // --- TUNABLES (plain fields so hot reload can change them) ---
        public float BuilderReachTiles = 12f;      // a builder arm reaches this far from the player's centre (the anchoring arms reach 12.5: 2 x 100 px)
        public bool KeepWaiting = true;            // true: a job waits (for material, reach, a cleared tile) until it finishes, is cancelled or the player leaves the suit
        public int StrikeTicks = 14;               // one placement strike
        public int RescanTicks = 15;               // an idle arm that found nothing looks again after this long
        public int BlockedCooldownTicks = 180;     // a tile PlaceTile refused (a player or NPC stands there) is skipped this long
        public int ReachTimeoutTicks = 120;        // give up on a target the hand cannot settle on
        public int GrabTimeoutTicks = 90;          // the fold-back to grab the next item gives up after this long
        public int SettleTicks = 60;               // nothing placeable for this long (both arms idle) ends the job
        public int IdleTimeoutTicks = 1800;        // no placement for this long ends the job even if tiles are still out of reach
        public int StatusRefreshTicks = 60;        // the arm state is re-sent this often while nothing changes
        public int MaxPendingBlocks = 30000;       // a new copy is refused when the running job would hold more blocks than this
        public int ConsumedFlushTicks = 10;        // taken items are reported to the owner at most this often
        public bool LogJobs = true;                // TEMPORARY [MechBuild] log lines (remove in Phase 10)

        enum Phase : byte { Idle, Retracting, Reaching, Striking }

        class Arm
        {
            public MechSuitBuilderArm View;
            public Phase Phase;
            public MechSuitBlueprint.Block Target;
            public Point Tile;
            public int Timer;
            public int Wait;
            public bool ImpactDone;
            public int NextType;
            public bool LastHasTarget;
            public Point16 LastTile;
            public bool LastStriking;
            public ushort LastHeld = ushort.MaxValue;
        }

        class Job
        {
            public int Owner;
            public Rectangle Source;
            public Point Origin;
            public readonly List<MechSuitBlueprint.Block> Pending = new();
            public readonly Arm[] Arms = new Arm[2];
            public readonly HashSet<(int, int, bool)> Claimed = new();
            public readonly Dictionary<(int, int, bool), uint> Cooldowns = new();
            public readonly List<MechSuitBuildNetSystem.ConsumedEntry> Consumed = new();
            public int Total, Placed;
            public int IdleTicks, StatusTimer, ConsumeTimer;
            // The newest scan of the pending list (what is left, and why).
            public int Placeable, WaitingForReach, Blocked, NoMaterial, Unsupported;
        }

        readonly Dictionary<int, Job> jobs = new();
        readonly Dictionary<int, Item> items = new();
        readonly Dictionary<int, int> inventoryCounts = new();
        readonly List<int> scratchKeys = new();

        public override void OnWorldUnload()
        {
            jobs.Clear();
            items.Clear();
        }

        public override void Unload()
        {
            jobs.Clear();
            items.Clear();
        }

        public bool HasJob(int owner) => jobs.ContainsKey(owner);

        void Log(string message)
        {
            if (LogJobs)
                Mod.Logger.Info("[MechBuild] " + message);
        }

        Item GetItem(int type)
        {
            if (!items.TryGetValue(type, out Item item))
            {
                item = new Item();
                item.SetDefaults(type);
                items[type] = item;
            }

            return item;
        }

        static int CountObjects(Job job)
        {
            int count = 0;
            foreach (MechSuitBlueprint.Block block in job.Pending)
            {
                if (block.ObjW > 0)
                    count++;
            }

            return count;
        }

        // ---------------------------------------------------------------- start / end

        /// <summary>Server or single-player: starts the build job of one player, or adds one more copy to the job that already runs. Everything the client sent is checked here.</summary>
        public void StartJob(int sender, Rectangle source, Point origin)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient || sender < 0 || sender >= Main.maxPlayers)
                return;

            Player owner = Main.player[sender];
            if (!owner.active || owner.dead || !owner.GetModPlayer<MechSuitPlayer>().IsActive)
            {
                Log("request from " + sender + " refused: not alive or not in the suit");
                return;
            }

            MechSuitBuilderSystem limits = ModContent.GetInstance<MechSuitBuilderSystem>();
            bool sane = source.Width >= 1 && source.Height >= 1
                && source.Width <= limits.MaxSelectionWidth && source.Height <= limits.MaxSelectionHeight
                && source.X >= 0 && source.Y >= 0 && source.Right <= Main.maxTilesX && source.Bottom <= Main.maxTilesY
                && origin.X > -source.Width && origin.Y > -source.Height && origin.X < Main.maxTilesX && origin.Y < Main.maxTilesY;
            if (!sane)
            {
                Log("request from " + sender + " refused: bad rectangle " + source + " origin " + origin);
                return;
            }

            List<MechSuitBlueprint.Block> blocks = new List<MechSuitBlueprint.Block>();
            ModContent.GetInstance<MechSuitBlueprint>().Scan(source, blocks);
            MechSuitBlueprint.Shift(blocks, origin);

            // The blocks of a job hold absolute world tiles (the destination origin is added by Shift), so one job can hold several copies
            // placed at different spots. job.Origin stays at zero for the same reason. Only the head of a unit is kept: the job places a
            // whole object from its footprint and does not need the other cells (the client's placed hologram does, to draw them).
            Job job = new Job { Owner = sender, Source = source, Origin = Point.Zero };
            foreach (MechSuitBlueprint.Block block in blocks)
            {
                if (block.Head)
                    job.Pending.Add(block);
            }

            job.Total = job.Pending.Count;

            if (job.Pending.Count == 0)
            {
                Log("request from " + sender + " refused: nothing to build in " + source);
                return;
            }

            if (jobs.TryGetValue(sender, out Job running))
            {
                // The new copy wins where it overlaps older pending copies: those blocks are dropped (same tile, same layer), never counted twice.
                HashSet<(int, int, bool)> newCells = MechSuitBlueprint.CellsOf(job.Pending);
                List<MechSuitBlueprint.Block> kept = new List<MechSuitBlueprint.Block>(running.Pending.Count);
                foreach (MechSuitBlueprint.Block block in running.Pending)
                {
                    if (!MechSuitBlueprint.Overlaps(newCells, block))
                        kept.Add(block);
                }

                if (kept.Count + job.Pending.Count > MaxPendingBlocks)
                {
                    Log("request from " + sender + " refused: the running job would hold more than " + MaxPendingBlocks + " blocks");
                    return;
                }

                running.Pending.Clear();
                running.Pending.AddRange(kept);
                running.Pending.AddRange(job.Pending);
                running.Total += job.Total;
                running.IdleTicks = 0;
                Log("player " + sender + " added a copy to the running build: source " + source + " -> origin " + origin + ", +" + job.Pending.Count + " blocks, " + running.Pending.Count + " pending now");
                return;
            }

            MechSuitBuilderArmSet set = ModContent.GetInstance<MechSuitBuilderArms>().GetOrCreate(sender);
            set.Building = true;
            for (int i = 0; i < job.Arms.Length; i++)
            {
                job.Arms[i] = new Arm { View = set.Arms[i] };
                job.Arms[i].View.SetHeld(DisplayItem(job));
                job.Arms[i].View.Motion.SetTarget(null);
            }

            jobs[sender] = job;
            if (Main.netMode == NetmodeID.SinglePlayer)
                owner.GetModPlayer<MechSuitPlayer>().SetBuilding(true);

            SendState(job, owner, true, true);
            Log("player " + sender + " started a build: source " + source + " -> origin " + origin + ", " + job.Pending.Count + " units (" + CountObjects(job) + " objects)");
        }

        /// <summary>Ends the job of a player (no-op without one). Called by the job itself, by a new request, and when the suit goes off.</summary>
        public void EndJob(int owner, string reason)
        {
            if (!jobs.TryGetValue(owner, out Job job))
                return;

            jobs.Remove(owner);
            Player player = Main.player[owner];
            FlushConsumed(job, player);

            ModContent.GetInstance<MechSuitBuilderArms>().MarkEnded(owner);
            if (Main.netMode == NetmodeID.SinglePlayer)
                player.GetModPlayer<MechSuitPlayer>().SetBuilding(false);

            SendState(job, player, false, true);
            Log("player " + owner + "'s build ended (" + reason + "): placed " + job.Placed + " of " + (job.Placed + job.Pending.Count) + ", left " + job.Pending.Count
                + " (blocked " + job.Blocked + ", out of material " + job.NoMaterial + ", unsupported " + job.Unsupported + ", out of reach " + job.WaitingForReach + ")");
        }

        // ---------------------------------------------------------------- tick

        public override void PostUpdateEverything()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient || jobs.Count == 0)
                return;

            scratchKeys.Clear();
            scratchKeys.AddRange(jobs.Keys);
            foreach (int key in scratchKeys)
            {
                if (jobs.TryGetValue(key, out Job job))
                    TickJob(job);
            }
        }

        void TickJob(Job job)
        {
            Player owner = Main.player[job.Owner];
            if (!owner.active || owner.dead)
            {
                EndJob(job.Owner, "the player is gone");
                return;
            }

            if (!owner.GetModPlayer<MechSuitPlayer>().IsActive)
            {
                EndJob(job.Owner, "the player left the suit");
                return;
            }

            MechSuitBuilderArms arms = ModContent.GetInstance<MechSuitBuilderArms>();
            bool busy = false;
            for (int i = 0; i < job.Arms.Length; i++)
            {
                TickArm(job, owner, i, arms.Shoulder(owner, i));
                if (job.Arms[i].Phase != Phase.Idle)
                    busy = true;
            }

            // The hand model moves after the arm logic, like the Robotic Arm entity does.
            for (int i = 0; i < job.Arms.Length; i++)
            {
                MechSuitBuilderArm view = job.Arms[i].View;
                view.Motion.Update(arms.Shoulder(owner, i), arms.UpperArmLength, arms.ForearmLength, view.Held, false);
            }

            if (busy)
                job.IdleTicks = 0;
            else
                job.IdleTicks++;

            if (++job.ConsumeTimer >= ConsumedFlushTicks)
                FlushConsumed(job, owner);

            SendState(job, owner, true, false);

            if (!busy && job.Pending.Count == 0)
                EndJob(job.Owner, "finished");
            else if (!KeepWaiting && !busy && job.IdleTicks >= SettleTicks && job.Placeable == 0 && job.WaitingForReach == 0)
                EndJob(job.Owner, "nothing left can be placed");
            else if (!KeepWaiting && job.IdleTicks >= IdleTimeoutTicks)
                EndJob(job.Owner, "timed out waiting");
        }

        void TickArm(Job job, Player owner, int index, Vector2 shoulder)
        {
            Arm arm = job.Arms[index];
            RoboticArmMotion motion = arm.View.Motion;

            switch (arm.Phase)
            {
                case Phase.Idle:
                    if (arm.Wait > 0)
                    {
                        arm.Wait--;
                        break;
                    }

                    if (!FindTarget(job, owner, arm))
                    {
                        motion.SetTarget(null);
                        KeepHolding(job, arm);
                        arm.Wait = RescanTicks;
                        break;
                    }

                    if (arm.View.HeldType != arm.Target.Item)
                    {
                        // Different material: fold back to the player to grab it first.
                        arm.NextType = arm.Target.Item;
                        arm.Phase = Phase.Retracting;
                        arm.Timer = 0;
                        motion.SetTarget(shoulder);
                    }
                    else
                    {
                        BeginReach(arm);
                    }

                    break;

                case Phase.Retracting:
                    arm.Timer++;
                    if (motion.InPosition || arm.Timer >= GrabTimeoutTicks)
                    {
                        arm.View.SetHeld(arm.NextType);
                        BeginReach(arm);
                    }

                    break;

                case Phase.Reaching:
                    arm.Timer++;
                    if (!StillWanted(job, owner, arm))
                    {
                        Release(job, arm);
                        break;
                    }

                    if (motion.InPosition)
                    {
                        motion.StartStrike(StrikeTicks);
                        arm.Phase = Phase.Striking;
                        arm.Timer = 0;
                        arm.ImpactDone = false;
                    }
                    else if (arm.Timer >= ReachTimeoutTicks)
                    {
                        job.Cooldowns[Key(arm)] = Main.GameUpdateCount + (uint)BlockedCooldownTicks;
                        Release(job, arm);
                    }

                    break;

                case Phase.Striking:
                    arm.Timer++;
                    if (!arm.ImpactDone && arm.Timer >= (int)(StrikeTicks * motion.ImpactPoint))
                    {
                        arm.ImpactDone = true;
                        ApplyPlacement(job, owner, arm);
                    }

                    if (arm.Timer >= StrikeTicks)
                        Release(job, arm);

                    break;
            }
        }

        // An arm that holds nothing (job start, or its last item was spent) takes the item of the first pending block it could place, or of
        // the first pending block at all, so the claws always show something while a job runs.
        int DisplayItem(Job job)
        {
            int fallback = 0;
            for (int i = 0; i < job.Pending.Count; i++)
            {
                int item = job.Pending[i].Item;
                if (fallback == 0)
                    fallback = item;

                if (inventoryCounts.TryGetValue(item, out int count) && count > 0)
                    return item;
            }

            return fallback;
        }

        void KeepHolding(Job job, Arm arm)
        {
            if (arm.View.HeldType == 0)
                arm.View.SetHeld(DisplayItem(job));
        }

        // A unit's key: its own tile, or the top-left corner of an object (one claim / cooldown per unit).
        static (int, int, bool) KeyOf(MechSuitBlueprint.Block block) => block.ObjW > 0 ? (block.ObjX, block.ObjY, false) : (block.X, block.Y, block.IsWall);

        static (int, int, bool) Key(Arm arm) => KeyOf(arm.Target);

        // Where the hand goes: the tile's centre, or the centre of the object's footprint.
        static Vector2 CenterOf(MechSuitBlueprint.Block block)
        {
            return block.ObjW > 0
                ? new Vector2((block.ObjX + block.ObjW / 2f) * 16f, (block.ObjY + block.ObjH / 2f) * 16f)
                : new Vector2(block.X * 16f + 8f, block.Y * 16f + 8f);
        }

        void BeginReach(Arm arm)
        {
            arm.Phase = Phase.Reaching;
            arm.Timer = 0;
            arm.View.Motion.SetTarget(CenterOf(arm.Target));
        }

        void Release(Job job, Arm arm)
        {
            if (arm.Target != null)
                job.Claimed.Remove(Key(arm));

            arm.Target = null;
            arm.Phase = Phase.Idle;
            arm.Timer = 0;
        }

        // The claimed tile can be built by someone else, or the owner can fly out of range, while the hand is still on its way.
        bool StillWanted(Job job, Player owner, Arm arm)
        {
            if (!job.Pending.Contains(arm.Target))
                return false;

            Vector2 center = CenterOf(arm.Target);
            float reach = BuilderReachTiles * 16f * 1.1f;
            return Vector2.DistanceSquared(owner.Center, center) <= reach * reach;
        }

        // ---------------------------------------------------------------- choosing a target

        // Scans the whole pending list: drops what is already built, counts why the rest cannot be placed right now, and claims the closest
        // tile that can. Also refreshes the job's Placeable / WaitingForReach numbers that decide when the job ends.
        bool FindTarget(Job job, Player owner, Arm arm)
        {
            inventoryCounts.Clear();
            for (int s = 0; s < 58; s++)
            {
                Item slot = owner.inventory[s];
                if (slot != null && !slot.IsAir)
                    inventoryCounts[slot.type] = (inventoryCounts.TryGetValue(slot.type, out int have) ? have : 0) + slot.stack;
            }

            job.Placeable = job.WaitingForReach = job.Blocked = job.NoMaterial = job.Unsupported = 0;
            float reachPixels = BuilderReachTiles * 16f;
            float reachSquared = reachPixels * reachPixels;
            MechSuitBlueprint.Block bestBlock = null;
            float bestDistance = float.MaxValue;

            for (int i = job.Pending.Count - 1; i >= 0; i--)
            {
                MechSuitBlueprint.Block block = job.Pending[i];
                Rectangle footprint = MechSuitBlueprint.Footprint(block);
                if (!WorldGen.InWorld(footprint.Left, footprint.Top, 5) || !WorldGen.InWorld(footprint.Right - 1, footprint.Bottom - 1, 5))
                {
                    job.Blocked++;
                    continue;
                }

                // Built / blocked / free for the whole unit (every cell of an object), the same rule the holograms use.
                MechSuitBlueprint.UnitState state = MechSuitBlueprint.StateOf(block);
                if (state == MechSuitBlueprint.UnitState.Built)
                {
                    job.Pending[i] = job.Pending[job.Pending.Count - 1];
                    job.Pending.RemoveAt(job.Pending.Count - 1);
                    continue;
                }

                (int, int, bool) key = KeyOf(block);
                if (state == MechSuitBlueprint.UnitState.Blocked || (job.Cooldowns.TryGetValue(key, out uint until) && until > Main.GameUpdateCount))
                {
                    job.Blocked++;
                    continue;
                }

                if (job.Claimed.Contains(key))
                    continue;

                if (!inventoryCounts.TryGetValue(block.Item, out int count) || count <= 0)
                {
                    job.NoMaterial++;
                    continue;
                }

                Item item = GetItem(block.Item);
                bool isObject = block.ObjW > 0;
                float distance = Vector2.DistanceSquared(owner.Center, CenterOf(block));

                // The object check (TileObject.CanPlace over every variant) is the dear one: only run it for objects the arm could reach.
                if (isObject && distance > reachSquared)
                {
                    job.WaitingForReach++;
                    continue;
                }

                bool canPlace = block.IsWall
                    ? WorldPlacement.CanPlaceWall(block.X, block.Y, item)
                    : isObject
                        ? WorldPlacement.TryGetObjectAt(new Point(block.ObjX, block.ObjY), item, owner.direction, out _, out _, out _, out _)
                        : WorldPlacement.CanPlacePlain(block.X, block.Y, item);
                if (!canPlace)
                {
                    job.Unsupported++;
                    continue;
                }

                if (distance > reachSquared)
                {
                    job.WaitingForReach++;
                    continue;
                }

                job.Placeable++;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestBlock = block;
                }
            }

            if (bestBlock == null)
                return false;

            arm.Target = bestBlock;
            (int, int, bool) anchor = KeyOf(arm.Target);
            arm.Tile = new Point(anchor.Item1, anchor.Item2);
            job.Claimed.Add(Key(arm));
            return true;
        }

        // ---------------------------------------------------------------- the placement (at the impact point of the strike)

        void ApplyPlacement(Job job, Player owner, Arm arm)
        {
            MechSuitBlueprint.Block block = arm.Target;
            int x = arm.Tile.X;
            int y = arm.Tile.Y;

            int slot = -1;
            for (int s = 0; s < 58 && slot < 0; s++)
            {
                Item candidate = owner.inventory[s];
                if (candidate != null && !candidate.IsAir && candidate.type == block.Item && candidate.stack > 0)
                    slot = s;
            }

            if (slot < 0)
            {
                job.Cooldowns[Key(arm)] = Main.GameUpdateCount + (uint)BlockedCooldownTicks;
                return; // ran out meanwhile; the next scan counts it as out of material
            }

            Item item = GetItem(block.Item);
            bool placed;
            Rectangle placedArea = new Rectangle(x, y, 1, 1);
            Point objectCursor = new Point(x, y);
            if (block.IsWall)
            {
                placed = WorldPlacement.CanPlaceWall(x, y, item) && WorldPlacement.PlaceWall(x, y, item);
            }
            else if (block.ObjW > 0)
            {
                // The route a player's item takes (WorldPlacement.PlaceObject): chests get their empty Main.chest entry, machines their entity.
                // The facing follows the owner (the other side is tried too), see roadmap section 8.
                placed = false;
                if (WorldPlacement.TryGetObjectAt(new Point(block.ObjX, block.ObjY), item, owner.direction, out int direction, out Point cursor, out TileObject data, out Rectangle footprint))
                {
                    placed = WorldPlacement.PlaceObject(cursor.X, cursor.Y, item, direction, data, footprint);
                    placedArea = footprint;
                    objectCursor = cursor;
                }
            }
            else
            {
                placed = WorldPlacement.CanPlacePlain(x, y, item) && WorldPlacement.PlacePlain(x, y, item);
                if (placed && block.Half)
                {
                    Tile placedTile = Main.tile[x, y];
                    placedTile.IsHalfBlock = true;
                    WorldGen.SquareTileFrame(x, y);
                }
            }

            if (!placed)
            {
                job.Cooldowns[Key(arm)] = Main.GameUpdateCount + (uint)BlockedCooldownTicks;
                return;
            }

            // Take the material. Single-player edits the real inventory; on a server this is the server's copy and the owner is told (SendConsumed).
            Item stack = owner.inventory[slot];
            stack.stack--;
            if (stack.stack <= 0)
                stack.TurnToAir();

            RecordConsumed(job, slot, block.Item);
            job.Pending.Remove(block);
            job.Placed++;
            job.IdleTicks = 0;

            if (Main.netMode == NetmodeID.Server)
            {
                // One tile square around what was placed (3 x 3 for a block), then the chest message / tile-entity sharing of an object.
                int squareX = System.Math.Max(0, placedArea.X - 1);
                int squareY = System.Math.Max(0, placedArea.Y - 1);
                NetMessage.SendTileSquare(-1, squareX, squareY, System.Math.Min(255, placedArea.Width + 2), System.Math.Min(255, placedArea.Height + 2));
                if (block.ObjW > 0)
                    WorldPlacement.BroadcastObjectExtras(item, objectCursor.X, objectCursor.Y);
                RoboticArmNet.SendPlaceSound(ModContent.GetInstance<MechSuitBuilderArms>().Shoulder(owner, 0), x, y);
            }
        }

        void RecordConsumed(Job job, int slot, int type)
        {
            if (Main.netMode != NetmodeID.Server)
                return;

            for (int i = 0; i < job.Consumed.Count; i++)
            {
                MechSuitBuildNetSystem.ConsumedEntry entry = job.Consumed[i];
                if (entry.Slot == slot && entry.Type == type)
                {
                    entry.Count++;
                    job.Consumed[i] = entry;
                    return;
                }
            }

            job.Consumed.Add(new MechSuitBuildNetSystem.ConsumedEntry { Slot = (byte)slot, Type = (ushort)type, Count = 1 });
        }

        void FlushConsumed(Job job, Player owner)
        {
            job.ConsumeTimer = 0;
            if (job.Consumed.Count == 0)
                return;

            MechSuitBuildNet.SendConsumed(owner, job.Consumed);
            job.Consumed.Clear();
        }

        // ---------------------------------------------------------------- arm state broadcast

        // Sends the builder arms' state to the players near the owner when something changed (a target appeared, moved or vanished, a strike
        // started, the held item changed), as a refresh every StatusRefreshTicks, and always when force is set (start / end). Server only.
        void SendState(Job job, Player owner, bool building, bool force)
        {
            MechSuitBuildNetSystem.ArmState[] states = new MechSuitBuildNetSystem.ArmState[job.Arms.Length];
            bool changed = force;
            for (int i = 0; i < job.Arms.Length; i++)
            {
                Arm arm = job.Arms[i];
                RoboticArmMotion motion = arm.View.Motion;
                bool has = building && motion.HasTarget;
                Point16 tile = default;
                if (has)
                    tile = new Point16((int)(motion.Target.Value.X / 16f), (int)(motion.Target.Value.Y / 16f));

                bool striking = motion.Striking;
                bool started = striking && !arm.LastStriking;
                arm.LastStriking = striking;

                ushort held = building ? arm.View.HeldType : (ushort)0;
                if (has != arm.LastHasTarget || tile != arm.LastTile || started || held != arm.LastHeld)
                    changed = true;

                arm.LastHasTarget = has;
                arm.LastTile = tile;
                arm.LastHeld = held;
                states[i] = new MechSuitBuildNetSystem.ArmState
                {
                    HasTarget = has,
                    TargetX = tile.X,
                    TargetY = tile.Y,
                    StrikeTicks = started ? (byte)System.Math.Min(motion.StrikeDuration, 255) : (byte)0,
                    HeldType = held
                };
            }

            if (!changed && ++job.StatusTimer < StatusRefreshTicks)
                return;

            job.StatusTimer = 0;
            MechSuitBuildNet.SendArmState(owner, building, states);
        }
    }
}