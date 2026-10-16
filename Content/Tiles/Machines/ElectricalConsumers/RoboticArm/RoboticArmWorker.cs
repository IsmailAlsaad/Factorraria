using Factorraria.Common.Machines;
using Factorraria.Content.Tiles.Ores;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

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
    // A placeable item in the slot (block, furniture, torch, platform, wall, sapling...) is placed into empty tiles (Phase 4, item 4). A tool mines (Phase 5): the pickaxe breaks tiles, the axe chops
    // trees, the hammer smashes walls. One target is struck repeatedly until it changes or stops responding. Drops are caught by
    // RoboticArmDropCapture for the duration of each strike and routed by the entity (conveyor vItems, off-screen vItems, or world items).
    public class RoboticArmWorker
    {
        // --- TUNABLES (plain fields so hot reload can change them) ---
        public int RescanInterval = 20;           // ticks between scans while idle
        public int MinStrikeTicks = 8;            // a block's strike lasts its useTime, but never less than this
        public int ReachTimeoutTicks = 120;       // give up on a target the hand cannot settle on
        public int BlockedCooldownTicks = 180;    // a tile PlaceTile refused (a player or NPC is probably standing on it) is skipped this long
        public int TreeStickyColumns = 3;         // an axe candidate within this many columns of the last tree chopped counts as the same tree
        public bool FootprintMustFitArea = true;  // a multi-tile object is only placed when every tile it covers is inside the area and the reach
        public bool PlatformsInOpenAir = false;   // false: a platform needs an active tile somewhere in the 3x3 around its target (vanilla's legacy rule), so the arm does not tile the whole area with platforms
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
        bool hasPending;       // idle scan found a job; it starts as soon as the arm has power
        Point pendingTarget;
        Job pendingJob;
        int lastTreeX;
        bool hasLastTree;

        public RoboticArmWorker(RoboticArmTileEntity arm)
        {
            this.arm = arm;
        }

        // One logic tick. Returns whether the arm has a valid job right now (it feeds isWorking: power is only drawn then).
        public bool Tick()
        {
            Item held = arm.InputSlots[0];
            WatchForChanges(held);

            if (held.IsAir || !(arm.IsPlaceable(held) || arm.IsTool(held)))
            {
                Abort();
                return false;
            }

            switch (phase)
            {
                case Phase.Idle:
                    if (--rescanTimer <= 0)
                    {
                        rescanTimer = RescanInterval;
                        hasPending = FindTarget(held, out pendingTarget, out pendingJob) && OutputAllows(pendingJob);
                    }

                    if (!hasPending)
                        return false;

                    // A job exists, so the arm keeps counting toward the grid's demand (isWorking), but it does not move without power.
                    if (!arm.isOn)
                        return true;

                    hasPending = false;
                    if (!IsValidTarget(pendingTarget.X, pendingTarget.Y, held, pendingJob))
                    {
                        rescanTimer = 0; // the world changed while it waited; scan again next tick
                        return false;
                    }

                    BeginReach(pendingTarget, pendingJob);
                    return true;

                case Phase.Reach:
                    return TickReach(held);

                default:
                    // Power lost mid-strike: SetTarget(null) cancels the visual strike and sends the hand home. The target stays, and
                    // the arm waits in Reach until power returns.
                    if (!arm.isOn)
                    {
                        arm.Visual?.SetTarget(null);
                        phase = Phase.Reach;
                        phaseTimer = 0;
                        return true;
                    }

                    TickStrike(held);
                    return true;
            }
        }

        // Mining drops onto an adjacent conveyor, so a belt with no free tile pauses the arm before it swings (placing never drops anything).
        bool OutputAllows(Job forJob) => forJob == Job.Place || arm.CanDeliverDrops;

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
            hasLastTree = false;
            Abort();
        }

        void Abort()
        {
            phase = Phase.Idle;
            rescanTimer = 0;
            hasPending = false;
            arm.Visual?.SetTarget(null);
        }

        void BeginReach(Point tile, Job newJob)
        {
            target = tile;
            job = newJob;
            if (newJob == Job.Axe)
            {
                lastTreeX = tile.X;
                hasLastTree = true;
            }
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

            // No power: no reaching and no strikes. The hand goes back to its idle pose; the target and the reach timer stay frozen.
            // The arm still reports a job (isWorking) so the grid keeps counting its demand and does not flicker.
            if (!arm.isOn)
            {
                if (arm.Visual != null && arm.Visual.HasTarget)
                    arm.Visual.SetTarget(null);

                return OutputAllows(job);
            }

            // Power is (back): aim at the kept target.
            if (arm.Visual != null && !arm.Visual.HasTarget)
                arm.Visual.SetTarget(TileCenter(target));

            // Conveyor clogged: wait where we are, no power, and the reach timeout does not run.
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
            if (now.IsAir || !(arm.IsPlaceable(now) || arm.IsTool(now)))
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
            ModContent.GetInstance<RoboticArmDropCaptureSystem>().Run(arm, () =>
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

            int damage = tile.TileType == TileID.Cactus ? (int)(axePower * 3 * 1.2f) : (int)(axePower * 1.2f);
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

        // Places the held item on the target by the route a player's item takes (Player.PlaceThing_Walls / PlaceThing_Tiles):
        //  wall    -> WallLoader.CanPlace, WorldGen.PlaceWall, WallLoader.PlaceInWorld
        //  object  -> TileObject.CanPlace + Place, SquareTileFrame, the post-placement hook, TileLoader.PlaceInWorld (anything with TileObjectData)
        //  plain   -> WorldGen.PlaceTile, TileLoader.PlaceInWorld
        // Always exactly one item is used up. ItemLoader.ConsumeItem is skipped on purpose: its hooks are for player buffs / ammo saving
        // (and VanillaConveyorGlobalItem returns false so players get infinite conveyors), none of which apply to a machine.
        // Vanilla's extras after a placement (PlaceThing_Walls_FillEmptySpace, auto paint, UnslopeForSolids...) are private and left out.
        void TryPlace(Item held)
        {
            bool placed;
            if (held.createWall >= 0)
                placed = PlaceWall(held);
            else if (TileObjectData.GetTileData(held.createTile, held.placeStyle) != null)
                placed = PlaceObject(held);
            else
                placed = PlacePlain(held);

            if (!placed)
            {
                // Refused at the last moment (a player or NPC is probably standing there): skip the tile for a while.
                cooldowns[target] = Main.GameUpdateCount + (uint)BlockedCooldownTicks;
                return;
            }

            held.stack--;

            if (held.stack <= 0)
                arm.InputSlots[0] = new Item();
        }

        bool PlaceWall(Item held)
        {
            int x = target.X;
            int y = target.Y;
            if (!WallLoader.CanPlace(x, y, held.createWall))
                return false;

            WorldGen.PlaceWall(x, y, held.createWall);
            if (Framing.GetTileSafely(x, y).WallType != held.createWall)
                return false;

            WallLoader.PlaceInWorld(x, y, held);
            return true;
        }

        bool PlaceObject(Item held)
        {
            int x = target.X;
            int y = target.Y;
            if (!TryGetObjectPlacement(x, y, held, false, out TileObject data, out Rectangle footprint))
                return false;

            if (!TileObject.Place(data))
                return false;

            // Place can report success without changing anything; only a footprint that now holds the tile counts.
            bool any = false;
            for (int tx = footprint.Left; tx < footprint.Right && !any; tx++)
                for (int ty = footprint.Top; ty < footprint.Bottom && !any; ty++)
                {
                    Tile tile = Framing.GetTileSafely(tx, ty);
                    any = tile.HasTile && tile.TileType == held.createTile;
                }

            if (!any)
                return false;

            WorldGen.SquareTileFrame(x, y);
            TileObjectData.CallPostPlacementPlayerHook(x, y, held.createTile, held.placeStyle, FacingFor(x), data.alternate, data);
            TileLoader.PlaceInWorld(x, y, held);
            return true;
        }

        bool PlacePlain(Item held)
        {
            // plr: -1 on purpose. The ghost's whoAmI is 0, which is the real local player in singleplayer.
            // PlaceTile refuses a tile a player or town NPC stands on (forced stays false), so that is "blocked, retry later".
            if (!WorldGen.PlaceTile(target.X, target.Y, held.createTile, mute: false, forced: false, plr: -1, style: held.placeStyle))
                return false;

            TileLoader.PlaceInWorld(target.X, target.Y, held);
            return true;
        }

        // Which way a directional item (chair, bed...) faces: toward the side of the arm the target is on.
        int FacingFor(int tileX) => tileX >= arm.Position.X ? 1 : -1;

        // Nearest to the shoulder first (ties resolved by scan order, so it is deterministic).
        bool FindTarget(Item held, out Point best, out Job bestJob)
        {
            best = default;
            bestJob = Job.Place;
            int bestTier = 0;
            int bestSticky = 0;
            double bestKey = 0;
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
                    RankTarget(x, y, tileJob, distance, out int tier, out int sticky, out double key);
                    if (!found || tier < bestTier || (tier == bestTier && (sticky < bestSticky || (sticky == bestSticky && key < bestKey))))
                    {
                        bestTier = tier;
                        bestSticky = sticky;
                        bestKey = key;
                        best = tile;
                        bestJob = tileJob;
                        found = true;
                    }
                }
            }

            return found;
        }

        // Lower is better, compared as (tier, sticky, key). Everything except the axe is simply nearest first. The axe prefers the base
        // of a tree (the tile below is not a tileAxe tile): striking it fells everything above, the cheapest cut. With no base in reach
        // it takes the lowest tree tile in reach. Candidates near the last tree chopped win ties so the arm stays on one tree.
        // Cactus (also tileAxe) follows the same rule, and nothing here depends on tree frame layouts.
        void RankTarget(int x, int y, Job tileJob, float distance, out int tier, out int sticky, out double key)
        {
            tier = 0;
            sticky = 0;
            key = distance;

            if (tileJob != Job.Axe)
                return;

            Tile below = Framing.GetTileSafely(x, y + 1);
            bool isBase = !(below.HasTile && Main.tileAxe[below.TileType]);
            tier = isBase ? 0 : 1;
            sticky = hasLastTree && System.Math.Abs(x - lastTreeX) <= TreeStickyColumns ? 0 : 1;

            if (!isBase)
                key = -y * 1000000.0 + distance; // larger y (lower on screen) first, then nearest
        }

        // What the item in the slot would do to this tile, if anything. A block places into empty tiles; a tool follows vanilla's routing:
        // the axe only hits tileAxe tiles, the pickaxe every other active tile (never tileAxe or tileHammer), the hammer only walls.
        bool TryClassify(int x, int y, Item held, out Job tileJob)
        {
            tileJob = Job.Place;
            if (arm.IsPlaceable(held))
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
                    return arm.IsPlaceable(held) && CanPlaceAt(x, y, held);

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
            if (tile.HasTile || tile.WallType <= WallID.None)
                return false;

            if (Main.wallDungeon[tile.WallType] && !NPC.downedBoss3)
                return false;

            if (tile.WallType == WallID.LihzahrdBrickUnsafe && !NPC.downedGolemBoss)
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

        // Whether the held item can be placed with this tile as the target (the tile a player's cursor would be on).
        bool CanPlaceAt(int x, int y, Item held)
        {
            if (!WorldGen.InWorld(x, y, 5))
                return false;

            if (held.createWall >= 0)
                return CanPlaceWallAt(x, y, held);

            // Tiles and objects only go into an empty tile, so never onto the arm's own base or another machine.
            if (Framing.GetTileSafely(x, y).HasTile)
                return false;

            if (!TileLoader.CanPlace(x, y, held.createTile))
                return false;

            if (TileObjectData.GetTileData(held.createTile, held.placeStyle) == null)
                return HasSupport(x, y);

            return TryGetObjectPlacement(x, y, held, true, out _, out _);
        }

        // Copy of the rule in Player.PlaceThing_Walls: a wall goes where one of the four neighbours is a tile (any tile) or has a wall.
        // Replacing a different wall is left out (the target must have no wall). Re-verify after tModLoader updates.
        bool CanPlaceWallAt(int x, int y, Item held)
        {
            if (Framing.GetTileSafely(x, y).WallType != WallID.None)
                return false;

            if (!WallLoader.CanPlace(x, y, held.createWall))
                return false;

            for (int i = 0; i < 4; i++)
            {
                int nx = x + (i == 0 ? -1 : i == 1 ? 1 : 0);
                int ny = y + (i == 2 ? -1 : i == 3 ? 1 : 0);
                Tile neighbour = Framing.GetTileSafely(nx, ny);

                if (neighbour.HasTile || neighbour.WallType > WallID.None)
                    return true;
            }

            return false;
        }

        // The object route's whole check. TileObject.CanPlace is vanilla's support / anchor / overlap-with-tiles check for TileObjectData
        // tiles and says where the object's top-left corner lands, so the footprint comes from it. On top of that the arm requires:
        // every footprint tile empty (no replacing plants or machines) and free of players / NPCs, and (FootprintMustFitArea) inside
        // the working area and the reach. onlyCheck true is for scanning; false fills in the data that TileObject.Place needs.
        bool TryGetObjectPlacement(int x, int y, Item held, bool onlyCheck, out TileObject data, out Rectangle footprint)
        {
            footprint = default;
            if (!TileObject.CanPlace(x, y, held.createTile, held.placeStyle, FacingFor(x), out data, onlyCheck))
                return false;

            TileObjectData objectData = TileObjectData.GetTileData(data.type, data.style, data.alternate)
                ?? TileObjectData.GetTileData(held.createTile, held.placeStyle);
            footprint = objectData == null
                ? new Rectangle(x, y, 1, 1)
                : new Rectangle(data.xCoord, data.yCoord, objectData.Width, objectData.Height);

            if (TileID.Sets.Platforms[held.createTile] && !PlatformsInOpenAir && !AnyTileAround(x, y))
                return false;

            Rectangle area = arm.AreaTiles;
            for (int tx = footprint.Left; tx < footprint.Right; tx++)
            {
                for (int ty = footprint.Top; ty < footprint.Bottom; ty++)
                {
                    if (!WorldGen.InWorld(tx, ty, 5) || Framing.GetTileSafely(tx, ty).HasTile)
                        return false;

                    if (FootprintMustFitArea && (!area.Contains(tx, ty) || !arm.IsTileInReach(tx, ty)))
                        return false;

                    if (!Collision.EmptyTile(tx, ty, true))
                        return false;
                }
            }

            return true;
        }

        // Any active tile in the 3x3 around the target (the target itself is empty by now).
        bool AnyTileAround(int x, int y)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if ((dx != 0 || dy != 0) && Framing.GetTileSafely(x + dx, y + dy).HasTile)
                        return true;

            return false;
        }

        // Copy of the vanilla rule for plain blocks (Player.PlaceThing_Tiles_BlockPlacementForAssortedThings, final else branch):
        // WorldGen.PlaceTile does not enforce it. Allowed when the target or one of its four neighbours has a wall, or a neighbour
        // is an active solid / beam / rope / tile 314. Re-verify this against the decompile after tModLoader updates.
        bool HasSupport(int x, int y)
        {
            if (Framing.GetTileSafely(x, y).WallType > WallID.None)
                return true;

            for (int i = 0; i < 4; i++)
            {
                int nx = x + (i == 0 ? -1 : i == 1 ? 1 : 0);
                int ny = y + (i == 2 ? -1 : i == 3 ? 1 : 0);
                Tile neighbour = Framing.GetTileSafely(nx, ny);

                if (neighbour.WallType > WallID.None)
                    return true;

                if (neighbour.HasTile &&
                    (Main.tileSolid[neighbour.TileType]
                    || TileID.Sets.IsBeam[neighbour.TileType]
                    || Main.tileRope[neighbour.TileType]
                    || neighbour.TileType == TileID.MinecartTrack))
                {
                    return true;
                }
            }

            return false;
        }

        static Vector2 TileCenter(Point tile) => new Vector2(tile.X * 16f + 8f, tile.Y * 16f + 8f);
    }
}