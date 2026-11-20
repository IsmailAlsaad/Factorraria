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
    // Patch M3 builds tiles and walls only: multi-tile objects (and one-tile objects like platforms, torches, doors) are skipped (patch M5).
    public class MechSuitBuildSystem : ModSystem
    {
        // --- TUNABLES (plain fields so hot reload can change them) ---
        public float BuilderReachTiles = 6.5f;     // a builder arm reaches this far from the player's centre
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
            public int Total, ObjectBlocksSkipped, Placed;
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

            // The blocks of a job hold absolute world tiles (the destination origin is added here), so one job can hold several copies
            // placed at different spots. job.Origin stays at zero for the same reason.
            Job job = new Job { Owner = sender, Source = source, Origin = Point.Zero, Total = blocks.Count };
            foreach (MechSuitBlueprint.Block block in blocks)
            {
                if (block.IsWall || block.Plain)
                {
                    block.X += origin.X;
                    block.Y += origin.Y;
                    job.Pending.Add(block);
                }
                else
                    job.ObjectBlocksSkipped++;
            }

            if (job.Pending.Count == 0)
            {
                Log("request from " + sender + " refused: nothing to build in " + source + " (" + job.ObjectBlocksSkipped + " object blocks skipped)");
                return;
            }

            if (jobs.TryGetValue(sender, out Job running))
            {
                if (running.Pending.Count + job.Pending.Count > MaxPendingBlocks)
                {
                    Log("request from " + sender + " refused: the running job would hold more than " + MaxPendingBlocks + " blocks");
                    return;
                }

                running.Pending.AddRange(job.Pending);
                running.Total += job.Total;
                running.ObjectBlocksSkipped += job.ObjectBlocksSkipped;
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
            Log("player " + sender + " started a build: source " + source + " -> origin " + origin + ", " + job.Pending.Count + " blocks, " + job.ObjectBlocksSkipped + " object blocks skipped (patch M5)");
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
            if (player != null && player.active)
                Report(job, owner, reason);
            Log("player " + owner + "'s build ended (" + reason + "): placed " + job.Placed + " of " + (job.Placed + job.Pending.Count) + ", left " + job.Pending.Count
                + " (blocked " + job.Blocked + ", out of material " + job.NoMaterial + ", unsupported " + job.Unsupported + ", out of reach " + job.WaitingForReach + ")");
        }

        // One chat line for the owner when the job ends (server -> that client; single-player prints it directly).
        void Report(Job job, int owner, string reason)
        {
            string text = "Mech Suit build " + reason + ": placed " + job.Placed + ", left " + job.Pending.Count;
            if (job.Pending.Count > 0)
                text += " (blocked " + job.Blocked + ", out of material " + job.NoMaterial + ", unsupported " + job.Unsupported + ", out of reach " + job.WaitingForReach + ")";

            if (Main.netMode == NetmodeID.Server)
                Terraria.Chat.ChatHelper.SendChatMessageToClient(Terraria.Localization.NetworkText.FromLiteral(text), Color.LightGreen, owner);
            else if (Main.netMode == NetmodeID.SinglePlayer)
                Main.NewText(text, Color.LightGreen);
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
            else if (!busy && job.IdleTicks >= SettleTicks && job.Placeable == 0 && job.WaitingForReach == 0)
                EndJob(job.Owner, "nothing left can be placed");
            else if (job.IdleTicks >= IdleTimeoutTicks)
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

        static (int, int, bool) Key(Arm arm) => (arm.Tile.X, arm.Tile.Y, arm.Target.IsWall);

        void BeginReach(Arm arm)
        {
            arm.Phase = Phase.Reaching;
            arm.Timer = 0;
            arm.View.Motion.SetTarget(new Vector2(arm.Tile.X * 16f + 8f, arm.Tile.Y * 16f + 8f));
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

            Vector2 center = new Vector2(arm.Tile.X * 16f + 8f, arm.Tile.Y * 16f + 8f);
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
                int x = job.Origin.X + block.X;
                int y = job.Origin.Y + block.Y;
                if (!WorldGen.InWorld(x, y, 5))
                {
                    job.Blocked++;
                    continue;
                }

                Tile tile = Main.tile[x, y];
                bool done = block.IsWall ? tile.WallType == block.Type : tile.HasTile && tile.TileType == block.Type;
                if (done)
                {
                    job.Pending[i] = job.Pending[job.Pending.Count - 1];
                    job.Pending.RemoveAt(job.Pending.Count - 1);
                    continue;
                }

                bool occupied = block.IsWall ? tile.WallType != 0 : tile.HasTile;
                (int, int, bool) key = (x, y, block.IsWall);
                if (occupied || (job.Cooldowns.TryGetValue(key, out uint until) && until > Main.GameUpdateCount))
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
                bool canPlace = block.IsWall ? WorldPlacement.CanPlaceWall(x, y, item) : WorldPlacement.CanPlacePlain(x, y, item);
                if (!canPlace)
                {
                    job.Unsupported++;
                    continue;
                }

                float distance = Vector2.DistanceSquared(owner.Center, new Vector2(x * 16f + 8f, y * 16f + 8f));
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
            arm.Tile = new Point(job.Origin.X + arm.Target.X, job.Origin.Y + arm.Target.Y);
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
            if (block.IsWall)
            {
                placed = WorldPlacement.CanPlaceWall(x, y, item) && WorldPlacement.PlaceWall(x, y, item);
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
                NetMessage.SendTileSquare(-1, x - 1, y - 1, 3, 3);
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