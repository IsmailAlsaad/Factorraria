using Factorraria.Common.Machines;
using Factorraria.Content.Tiles.Ores;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.RoboticArm
{
    // The one hidden Player the arms share. It is never ticked, never put in Main.player and never runs ItemCheck: it only exists
    // because vanilla wants a Player for ItemLoader.ConsumeItem (Phase 4) and for its hitTile damage buffers (Phase 5).
    // Held by a ModSystem (instead of a static field) so it is created lazily and dropped on unload.
    public class RoboticArmGhostSystem : ModSystem
    {
        Player ghost;
        public Player Ghost => ghost ??= new Player();

        public override void Unload()
        {
            ghost = null;
        }
    }

    // The work logic of one Robotic Arm (runs in the entity's Update, never saved). Idle -> Reach -> Strike, then straight on to the
    // next target if there is one, otherwise back to Idle (the visual eases the hand to its idle pose when its target is cleared).
    // It drives RoboticArmVisual through SetTarget / StartStrike / InPosition and reads the area and reach from the entity.
    // A block in the slot is placed into empty tiles (Phase 4). A tool mines (Phase 5): the pickaxe breaks tiles, the axe chops
    // trees, the hammer smashes walls. One target is struck repeatedly until it changes or stops responding. Drops are caught into the
    // output slot by RoboticArmDropCapture for the duration of each strike.
    public class RoboticArmWorker
    {
        // --- TUNABLES (plain fields so hot reload can change them) ---
        public int RescanInterval = 20;           // ticks between scans while idle
        public int MinStrikeTicks = 8;            // a block's strike lasts its useTime, but never less than this
        public int ReachTimeoutTicks = 120;       // give up on a target the hand cannot settle on
        public int BlockedCooldownTicks = 180;    // a tile PlaceTile refused (a player or NPC is probably standing on it) is skipped this long
        public int MaxStrikesPerTile = 40;        // strikes in a row that change nothing before the tile is given up on (PickTile silently ignores tiles above the pickaxe's power)

        enum Phase { Idle, Reach, Strike }
        enum Job { Place, Pick, Axe, Wall }

        readonly RoboticArmTileEntity arm;
        readonly Dictionary<Point, uint> cooldowns = new Dictionary<Point, uint>();

        Phase phase;
        Point target;
        Job job;
        int strikesOnTarget;
        int rescanTimer;
        int phaseTimer;
        int strikeLength;
        bool impactDone;
        int lastItemType = -1;
        Rectangle lastArea;

        public RoboticArmWorker(RoboticArmTileEntity arm)
        {
            this.arm = arm;
        }

        // One logic tick. Returns whether the arm has a valid job right now (it feeds isWorking: power is only drawn then).
        public bool Tick()
        {
            Item held = arm.InputSlots[0];
            WatchForChanges(held);

            if (held.IsAir || !(arm.IsBlock(held) || arm.IsTool(held)))
            {
                Abort();
                return false;
            }

            switch (phase)
            {
                case Phase.Idle:
                    if (--rescanTimer > 0)
                        return false;

                    rescanTimer = RescanInterval;
                    if (!FindTarget(held, out Point found, out Job foundJob) || !OutputAllows(foundJob))
                        return false;

                    BeginReach(found, foundJob);
                    return true;

                case Phase.Reach:
                    return TickReach(held);

                default:
                    TickStrike(held);
                    return true;
            }
        }

        // Mining fills the output slot, so a full one pauses the arm before it swings (placing never touches it).
        bool OutputAllows(Job forJob) => forJob == Job.Place || arm.OutputHasRoom;

        // A different item in the slot or a moved/resized area throws away everything the worker has learned.
        void WatchForChanges(Item held)
        {
            int itemType = held.IsAir ? 0 : held.type;
            Rectangle area = arm.AreaTiles;

            if (itemType == lastItemType && area == lastArea)
                return;

            lastItemType = itemType;
            lastArea = area;
            cooldowns.Clear();
            Abort();
        }

        void Abort()
        {
            phase = Phase.Idle;
            rescanTimer = 0;
            arm.Visual?.SetTarget(null);
        }

        void BeginReach(Point tile, Job newJob)
        {
            target = tile;
            job = newJob;
            strikesOnTarget = 0;
            phase = Phase.Reach;
            phaseTimer = 0;
            arm.Visual?.SetTarget(TileCenter(tile));
        }

        bool TickReach(Item held)
        {
            if (!IsValidTarget(target.X, target.Y, held, job))
            {
                Abort();
                return false;
            }

            // Output full: wait where we are, no power, and the reach timeout does not run.
            if (!OutputAllows(job))
            {
                phaseTimer = 0;
                return false;
            }

            if (++phaseTimer > ReachTimeoutTicks)
            {
                cooldowns[target] = Main.GameUpdateCount + (uint)BlockedCooldownTicks;
                Abort();
                return true;
            }

            RoboticArmVisual visual = arm.Visual;
            if (visual == null || !visual.InPosition || visual.Striking)
                return true;

            // Brownout: the arm keeps its target (it still counts toward the grid's demand) but starts no new strike.
            // A strike that is already running always finishes.
            if (!arm.isOn)
                return true;

            strikeLength = System.Math.Max(held.useTime, MinStrikeTicks);
            visual.StartStrike(strikeLength);
            phase = Phase.Strike;
            phaseTimer = 0;
            impactDone = false;
            return true;
        }

        void TickStrike(Item held)
        {
            phaseTimer++;

            float impactPoint = arm.Visual?.ImpactPoint ?? 0.6f;
            if (!impactDone && phaseTimer >= (int)(strikeLength * impactPoint))
            {
                impactDone = true;
                ApplyStrike(held);
            }

            if (phaseTimer < strikeLength)
                return;

            // Strike finished. Placing is one strike per tile; mining keeps hitting the same tile until it changes, stops responding
            // (blacklisted until the tool or area changes) or goes invalid.
            Item now = arm.InputSlots[0];
            if (now.IsAir || !(arm.IsBlock(now) || arm.IsTool(now)))
            {
                Abort();
                return;
            }

            if (job != Job.Place)
            {
                if (strikesOnTarget >= MaxStrikesPerTile)
                    cooldowns[target] = uint.MaxValue;
                else if (IsValidTarget(target.X, target.Y, now, job))
                {
                    phase = Phase.Reach;
                    phaseTimer = 0;
                    return;
                }
            }

            // Chain straight into the next target if there is one.
            if (FindTarget(now, out Point next, out Job nextJob) && OutputAllows(nextJob))
            {
                BeginReach(next, nextJob);
                return;
            }

            Abort();
            rescanTimer = RescanInterval;
        }

        // The world change of one strike, at the impact point of the swing. Everything runs inside the drop-capture scope.
        void ApplyStrike(Item held)
        {
            if (!IsValidTarget(target.X, target.Y, held, job))
                return; // the world changed under the arm mid-strike; the next scan picks something else

            if (job == Job.Place)
            {
                TryPlace(held);
                return;
            }

            Player ghost = ModContent.GetInstance<RoboticArmGhostSystem>().Ghost;
            int x = target.X;
            int y = target.Y;
            Tile before = Framing.GetTileSafely(x, y);
            bool hadTile = before.HasTile;
            ushort type = before.TileType;
            ushort wall = before.WallType;

            strikesOnTarget++;
            Job strikeJob = job;
            ModContent.GetInstance<RoboticArmDropCapture>().Run(arm, () =>
            {
                switch (strikeJob)
                {
                    case Job.Pick:
                        ghost.PickTile(x, y, held.pick);
                        break;
                    case Job.Axe:
                        ChopTile(ghost, x, y, held.axe);
                        break;
                    default:
                        // Vanilla checks this in the caller, not inside PickWall, so it is checked again right before every hit.
                        if (Player.CanPlayerSmashWall(x, y))
                            ghost.PickWall(x, y, (int)(held.hammer * 1.5f));
                        break;
                }
            });

            // Any change (tile gone, grass turned into dirt, wall gone) counts as progress and resets the no-progress count.
            Tile after = Framing.GetTileSafely(x, y);
            if (after.HasTile != hadTile || after.TileType != type || after.WallType != wall)
                strikesOnTarget = 0;
        }

        // Copy of the axe branch of Player.ItemCheck_UseMiningTools_ActuallyUseMiningTool (vanilla has no public method for it).
        // Damage: axe * 1.2 (cactus, tile 80, x3; modded tiles axe / MineResist; good world x1.3). Spike-verified: an Iron Axe does 10.
        // Re-verify against the decompile after tModLoader updates.
        void ChopTile(Player ghost, int x, int y, int axePower)
        {
            Tile tile = Framing.GetTileSafely(x, y);
            int tileId = ghost.hitTile.HitObject(x, y, 1);

            int damage = tile.TileType == 80 ? (int)(axePower * 3 * 1.2f) : (int)(axePower * 1.2f);
            ModTile modTile = TileLoader.GetTile(tile.TileType);
            if (modTile != null)
                damage = (int)(axePower / modTile.MineResist);

            if (Main.getGoodWorld)
                damage = (int)(damage * 1.3f);

            if (!WorldGen.CanKillTile(x, y))
                damage = 0;

            if (ghost.hitTile.AddDamage(tileId, damage, true) >= 100)
            {
                ghost.hitTile.Clear(tileId); // Player.ClearMiningCacheAt is not public
                WorldGen.KillTile(x, y);
            }
            else
            {
                WorldGen.KillTile(x, y, fail: true);
            }
        }

        // The vanilla placement path for a plain block (plan section 2 / spike 8.6): TileLoader.CanPlace and the support rule were
        // checked in CanPlaceAt, then WorldGen.PlaceTile, TileLoader.PlaceInWorld and consuming one item ourselves.
        void TryPlace(Item held)
        {
            // plr: -1 on purpose. The ghost's whoAmI is 0, which is the real local player in singleplayer.
            // PlaceTile refuses a tile a player or town NPC stands on (forced stays false), so that is "blocked, retry later".
            if (!WorldGen.PlaceTile(target.X, target.Y, held.createTile, mute: false, forced: false, plr: -1, style: held.placeStyle))
            {
                cooldowns[target] = Main.GameUpdateCount + (uint)BlockedCooldownTicks;
                return;
            }

            TileLoader.PlaceInWorld(target.X, target.Y, held);

            if (held.consumable && ItemLoader.ConsumeItem(held, ModContent.GetInstance<RoboticArmGhostSystem>().Ghost))
                held.stack--;

            if (held.stack <= 0)
                arm.InputSlots[0] = new Item();
        }

        // Nearest to the shoulder first (ties resolved by scan order, so it is deterministic).
        bool FindTarget(Item held, out Point best, out Job bestJob)
        {
            best = default;
            bestJob = Job.Place;
            float bestDistance = float.MaxValue;
            bool found = false;
            Rectangle area = arm.AreaTiles;
            Vector2 shoulder = arm.Shoulder;

            for (int x = area.Left; x < area.Right; x++)
            {
                for (int y = area.Top; y < area.Bottom; y++)
                {
                    if (!arm.IsTileInReach(x, y))
                        continue;

                    var tile = new Point(x, y);
                    if (cooldowns.TryGetValue(tile, out uint until))
                    {
                        if (until > Main.GameUpdateCount)
                            continue;

                        cooldowns.Remove(tile);
                    }

                    if (!TryClassify(x, y, held, out Job tileJob))
                        continue;

                    float distance = Vector2.DistanceSquared(TileCenter(tile), shoulder);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = tile;
                        bestJob = tileJob;
                        found = true;
                    }
                }
            }

            return found;
        }

        // What the item in the slot would do to this tile, if anything. A block places into empty tiles; a tool follows vanilla's routing:
        // the axe only hits tileAxe tiles, the pickaxe every other active tile (never tileAxe or tileHammer), the hammer only walls.
        bool TryClassify(int x, int y, Item held, out Job tileJob)
        {
            tileJob = Job.Place;
            if (arm.IsBlock(held))
                return CanPlaceAt(x, y, held);

            Tile tile = Framing.GetTileSafely(x, y);
            tileJob = !tile.HasTile ? Job.Wall : Main.tileAxe[tile.TileType] ? Job.Axe : Job.Pick;
            return IsValidTarget(x, y, held, tileJob);
        }

        bool IsValidTarget(int x, int y, Item held, Job forJob)
        {
            if (held.IsAir)
                return false;

            switch (forJob)
            {
                case Job.Place:
                    return arm.IsBlock(held) && CanPlaceAt(x, y, held);

                case Job.Pick:
                    return held.pick > 0 && IsMineable(x, y) && !Main.tileAxe[Main.tile[x, y].TileType] && !Main.tileHammer[Main.tile[x, y].TileType];

                case Job.Axe:
                    return held.axe > 0 && IsMineable(x, y) && Main.tileAxe[Main.tile[x, y].TileType];

                default:
                    return held.hammer > 0 && IsSmashableWall(x, y);
            }
        }

        // An active tile the arm may break: not one of this mod's machines, conveyors or pipes, and vanilla itself must be willing
        // (CheckTileBreakability 2 = KillTile would do nothing, CanKillTile false = a chest with items and the like).
        bool IsMineable(int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 5))
                return false;

            if (!Framing.GetTileSafely(x, y).HasTile || IsProtectedTile(x, y))
                return false;

            return WorldGen.CheckTileBreakability(x, y) != 2 && WorldGen.CanKillTile(x, y);
        }

        // Hammer rule: walls only, never a tile. Dungeon walls ignore hammers until Skeletron is dead and wall 87 until Golem is dead.
        bool IsSmashableWall(int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 5))
                return false;

            Tile tile = Framing.GetTileSafely(x, y);
            if (tile.HasTile || tile.WallType <= 0)
                return false;

            if (Main.wallDungeon[tile.WallType] && !NPC.downedBoss3)
                return false;

            if (tile.WallType == 87 && !NPC.downedGolemBoss)
                return false;

            return Player.CanPlayerSmashWall(x, y);
        }

        // The arm never breaks this mod's own machinery: machine tile entities (also on vanilla tiles), every ModTile of this mod
        // (arm, motors, pipes, priority conveyors) except the two ores, and vanilla conveyor belts.
        bool IsProtectedTile(int x, int y)
        {
            Tile tile = Framing.GetTileSafely(x, y);
            if (tile.TileType is TileID.ConveyorBeltLeft or TileID.ConveyorBeltRight)
                return true;

            ModTile modTile = TileLoader.GetTile(tile.TileType);
            if (modTile != null && modTile.Mod == arm.Mod && tile.TileType != ModContent.TileType<SiliconOreTile>() && tile.TileType != ModContent.TileType<PureSiliconTile>())
                return true;

            return TileEntityHelper.TryGetEntityFromTile(x, y, out TileEntity entity, out _) && entity is BaseMachine;
        }

        // Empty tile (so never the arm's own base or another machine), the block may go there, and vanilla's support rule holds.
        bool CanPlaceAt(int x, int y, Item held)
        {
            if (!WorldGen.InWorld(x, y, 5))
                return false;

            if (Framing.GetTileSafely(x, y).HasTile)
                return false;

            return TileLoader.CanPlace(x, y, held.createTile) && HasSupport(x, y);
        }

        // Copy of the vanilla rule for plain blocks (Player.PlaceThing_Tiles_BlockPlacementForAssortedThings, final else branch):
        // WorldGen.PlaceTile does not enforce it. Allowed when the target or one of its four neighbours has a wall, or a neighbour
        // is an active solid / beam / rope / tile 314. Re-verify this against the decompile after tModLoader updates.
        bool HasSupport(int x, int y)
        {
            if (Framing.GetTileSafely(x, y).WallType > 0)
                return true;

            for (int i = 0; i < 4; i++)
            {
                int nx = x + (i == 0 ? -1 : i == 1 ? 1 : 0);
                int ny = y + (i == 2 ? -1 : i == 3 ? 1 : 0);
                Tile neighbour = Framing.GetTileSafely(nx, ny);

                if (neighbour.WallType > 0)
                    return true;

                if (neighbour.HasTile && (Main.tileSolid[neighbour.TileType] || Main.tileBeam[neighbour.TileType] || Main.tileRope[neighbour.TileType] || neighbour.TileType == 314))
                    return true;
            }

            return false;
        }

        static Vector2 TileCenter(Point tile) => new Vector2(tile.X * 16f + 8f, tile.Y * 16f + 8f);
    }
}