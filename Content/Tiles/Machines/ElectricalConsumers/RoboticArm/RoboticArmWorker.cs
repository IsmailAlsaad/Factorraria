using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
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
    // Phase 4 handles plain blocks only; tools are Phase 5 (a tool in the slot is simply "no job" for now).
    public class RoboticArmWorker
    {
        // --- TUNABLES (plain fields so hot reload can change them) ---
        public int RescanInterval = 20;           // ticks between scans while idle
        public int MinStrikeTicks = 8;            // a block's strike lasts its useTime, but never less than this
        public int ReachTimeoutTicks = 120;       // give up on a target the hand cannot settle on
        public int BlockedCooldownTicks = 180;    // a tile PlaceTile refused (a player or NPC is probably standing on it) is skipped this long

        enum Phase { Idle, Reach, Strike }

        readonly RoboticArmTileEntity arm;
        readonly Dictionary<Point, uint> cooldowns = new Dictionary<Point, uint>();

        Phase phase;
        Point target;
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

            if (held.IsAir || !arm.IsBlock(held))
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
                    if (!FindTarget(held, out Point found))
                        return false;

                    BeginReach(found);
                    return true;

                case Phase.Reach:
                    TickReach(held);
                    return true;

                default:
                    TickStrike(held);
                    return true;
            }
        }

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

        void BeginReach(Point tile)
        {
            target = tile;
            phase = Phase.Reach;
            phaseTimer = 0;
            arm.Visual?.SetTarget(TileCenter(tile));
        }

        void TickReach(Item held)
        {
            if (!CanPlaceAt(target.X, target.Y, held))
            {
                Abort();
                return;
            }

            if (++phaseTimer > ReachTimeoutTicks)
            {
                cooldowns[target] = Main.GameUpdateCount + (uint)BlockedCooldownTicks;
                Abort();
                return;
            }

            RoboticArmVisual visual = arm.Visual;
            if (visual == null || !visual.InPosition || visual.Striking)
                return;

            // Brownout: the arm keeps its target (it still counts toward the grid's demand) but starts no new strike.
            // A strike that is already running always finishes.
            if (!arm.isOn)
                return;

            strikeLength = System.Math.Max(held.useTime, MinStrikeTicks);
            visual.StartStrike(strikeLength);
            phase = Phase.Strike;
            phaseTimer = 0;
            impactDone = false;
        }

        void TickStrike(Item held)
        {
            phaseTimer++;

            float impactPoint = arm.Visual?.ImpactPoint ?? 0.6f;
            if (!impactDone && phaseTimer >= (int)(strikeLength * impactPoint))
            {
                impactDone = true;
                TryPlace(held);
            }

            if (phaseTimer < strikeLength)
                return;

            // Strike finished: chain straight into the next target if there is one.
            Item now = arm.InputSlots[0];
            if (!now.IsAir && arm.IsBlock(now) && FindTarget(now, out Point next))
            {
                BeginReach(next);
                return;
            }

            Abort();
            rescanTimer = RescanInterval;
        }

        // The vanilla placement path for a plain block (plan section 2 / spike 8.6): TileLoader.CanPlace and the support rule were
        // checked in CanPlaceAt, then WorldGen.PlaceTile, TileLoader.PlaceInWorld and consuming one item ourselves.
        void TryPlace(Item held)
        {
            if (!CanPlaceAt(target.X, target.Y, held))
                return; // the world changed under the arm mid-strike; the next scan picks something else

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
        bool FindTarget(Item held, out Point best)
        {
            best = default;
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

                    if (!CanPlaceAt(x, y, held))
                        continue;

                    float distance = Vector2.DistanceSquared(TileCenter(tile), shoulder);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = tile;
                        found = true;
                    }
                }
            }

            return found;
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