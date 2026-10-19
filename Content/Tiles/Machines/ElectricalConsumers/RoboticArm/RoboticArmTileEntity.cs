using Factorraria.Common.Machines;
using Factorraria.Common.Systems;
using Factorraria.Content.VirtualItems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.RoboticArm
{
    // One input slot (a pickaxe, axe, hammer or a block) and no output slot: drops become vItems or world items. No recipes.
    public class RoboticArmTileEntity : ElectricConsumerMachine, IMachineDynamicDraw
    {
        public override int ValidTileType => ModContent.TileType<RoboticArmTile>();
        public override float PowerDemand => 100f;
        protected override int InputSlotCount => 1;
        protected override int OutputSlotCount => 0;

        // Arm geometry (pixels). Reach is what the work area is clamped to in Phase 3.
        public float UpperArmLength = 56f;
        public float ForearmLength = 56f;
        public float Reach => UpperArmLength + ForearmLength;

        // Centre of the base tile: the arm's shoulder.
        public Vector2 Shoulder => new Vector2(Position.X * 16f + 8f, Position.Y * 16f + 8f);

        // Client-only animation state. Never saved and never synced; created on the first client tick.
        RoboticArmVisual visual;
        public RoboticArmVisual Visual => visual;

        // The hand's motion (target, side, reach spring, strike timer, impact point). Owned here so the worker never waits on the client-only
        // visual (A1). Ticked once per logic tick right after the worker; the visual only reads it. Never saved, never synced.
        public RoboticArmMotion Motion { get; } = new RoboticArmMotion();

        // Working area: the tile offset of its top-left corner from the base tile, and its size in tiles. Saved. Phase 3's overlay
        // edits it (RoboticArmAreaEditorSystem) and Phase 4's scanner reads it. It always stays inside the square of tiles the arm
        // can reach (ReachTiles each way from the base), which is also its size cap.
        public int AreaX = -4;
        public int AreaY = -4;
        public int AreaW = 7;
        public int AreaH = 7;

        // How far from the base tile (in tiles, in x and in y: a square, not a circle) the arm works. It does not depend on the
        // visual arm length: past the arm's physical length the hand just stretches toward the target. The area can be dragged one
        // tile further (AreaLimit); that outer ring is drawn red and is never worked.
        public int ReachTiles = 7;
        public int AreaLimit => ReachTiles + 1;
        public Rectangle AreaTiles => new Rectangle(Position.X + AreaX, Position.Y + AreaY, AreaW, AreaH);

        public void SetArea(int x, int y, int w, int h)
        {
            int r = AreaLimit;
            int left = Math.Clamp(x, -r, r);
            int top = Math.Clamp(y, -r, r);
            int right = Math.Clamp(x + Math.Max(w, 1) - 1, left, r);
            int bottom = Math.Clamp(y + Math.Max(h, 1) - 1, top, r);

            AreaX = left;
            AreaY = top;
            AreaW = right - left + 1;
            AreaH = bottom - top + 1;
        }

        // Whether a tile is inside the square reach (ReachTiles each way from the base). The overlay tints the rest red; the worker skips them.
        public bool IsTileInReach(int tileX, int tileY) =>
            Math.Abs(tileX - Position.X) <= ReachTiles && Math.Abs(tileY - Position.Y) <= ReachTiles;

        // Patch 5.1: the working area is part of the machine snapshot. SetArea clamps it to the reach square, so a bad value cannot give an
        // invalid area. The worker, the hand motion and the visual stay runtime state.
        protected override void WriteSnapshotExtra(BinaryWriter writer)
        {
            base.WriteSnapshotExtra(writer);
            writer.Write((short)AreaX);
            writer.Write((short)AreaY);
            writer.Write((short)AreaW);
            writer.Write((short)AreaH);
        }

        protected override void ReadSnapshotExtra(BinaryReader reader)
        {
            base.ReadSnapshotExtra(reader);
            int x = reader.ReadInt16();
            int y = reader.ReadInt16();
            int w = reader.ReadInt16();
            int h = reader.ReadInt16();
            SetArea(x, y, w, h);
        }

        protected override string DescribeSnapshotExtra() => base.DescribeSnapshotExtra() + " area=(" + AreaX + ", " + AreaY + ") " + AreaW + "x" + AreaH;

        public override void SaveData(TagCompound tag)
        {
            base.SaveData(tag);
            tag["AreaX"] = AreaX;
            tag["AreaY"] = AreaY;
            tag["AreaW"] = AreaW;
            tag["AreaH"] = AreaH;
        }

        public override void LoadData(TagCompound tag)
        {
            base.LoadData(tag);
            if (tag.ContainsKey("AreaX"))
                SetArea(tag.GetInt("AreaX"), tag.GetInt("AreaY"), tag.GetInt("AreaW"), tag.GetInt("AreaH"));
        }

        // Work logic (scanner, reach / strike state machine, placing). Runtime only, never saved.
        RoboticArmWorker worker;

        public override void Update()
        {
            base.Update();

            worker ??= new RoboticArmWorker(this);

            // Only draws power while the worker has a valid job (nothing to do = idle, no demand).
            isWorking = worker.Tick();

            Item held = InputSlots[0];
            Motion.Update(Shoulder, UpperArmLength, ForearmLength, held, !held.IsAir && IsTool(held));
        }

        public bool IsTool(Item item) => item.pick > 0 || item.axe > 0 || item.hammer > 0;

        // Anything a player could place: blocks, furniture, torches, platforms, saplings, walls. Left out: tools, containers (a chest
        // would swallow items) and plain tiles the v1 plain-block filter rejects (grass-type, moss, non-solid or frame-important
        // tiles that have no TileObjectData). Keeping the slot and the conveyor intake to this rule means nothing can sit in the slot
        // that the arm will never place.
        public bool IsPlaceable(Item item)
        {
            if (item == null || item.IsAir || IsTool(item))
                return false;

            if (item.createWall >= 0)
                return true;

            int tile = item.createTile;
            if (tile < 0 || Main.tileContainer[tile])
                return false;

            // Object route (TileObjectData): TileObject.CanPlace does the anchor / support check, so the tile type needs no filter.
            if (TileObjectData.GetTileData(tile, item.placeStyle) != null)
                return true;

            if (!Main.tileSolid[tile] || Main.tileFrameImportant[tile] || Main.tileMoss[tile])
                return false;

            return tile is not (2 or 23 or 109 or 199 or 477 or 492 or 633 or 661 or 662 or 60 or 70);
        }

        // What the input slot accepts from the cursor.
        public bool IsAcceptedItem(Item item)
        {
            if (item == null || item.IsAir)
                return false;

            return IsTool(item) || IsPlaceable(item);
        }

        // Patch 6.2: the slot filter the UI and the server's slot edit validation share.
        public override bool AcceptsSlotItem(global::Factorraria.Common.Net.MachineSlotKind kind, int index, Item item)
        {
            return kind == global::Factorraria.Common.Net.MachineSlotKind.Input && index == 0 && IsAcceptedItem(item);
        }

        // The arm is a 1x1 solid tile, so vItems can never sit on its own footprint: it reads the 8 tiles around its anchor instead.
        protected override IEnumerable<Point> IntakeTiles()
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (dx != 0 || dy != 0)
                        yield return new Point(Position.X + dx, Position.Y + dy);
        }

        // Conveyors only ever hand over placeable items, and only while the slot is empty or already holds that item;
        // a loaded tool is never replaced.
        protected override bool TryGetIntakeSlot(int itemType, out int slot, out int limit)
        {
            slot = 0;
            limit = 0;

            if (!ContentSamples.ItemsByType.TryGetValue(itemType, out Item sample) || !IsPlaceable(sample))
                return false;

            Item held = InputSlots[0];
            if (!held.IsAir && held.type != itemType)
                return false;

            limit = sample.maxStack;
            return true;
        }

        // Mining needs somewhere to put what it breaks. With a conveyor in the 3x3 around the arm, drops go onto it as vItems, so a
        // belt with no free tile pauses the arm (back-pressure). Without a conveyor nothing can clog and the arm never waits.
        public bool CanDeliverDrops => !HasAdjacentConveyor() || TryFindFreeAdjacentConveyorTile(out _);

        bool HasAdjacentConveyor()
        {
            foreach (Point t in IntakeTiles())
                if (VirtualItemSystem.IsConveyorTile(t.X, t.Y, out _, out _))
                    return true;

            return false;
        }

        bool TryFindFreeAdjacentConveyorTile(out Point tile)
        {
            foreach (Point t in IntakeTiles())
            {
                if (VirtualItemSystem.IsConveyorTile(t.X, t.Y, out _, out _) && !VirtualItemSystem.IsTileOccupied(t.X, t.Y))
                {
                    tile = t;
                    return true;
                }
            }

            tile = default;
            return false;
        }

        // A free, non-solid neighbour: a vItem put there falls to the floor and waits to be picked up.
        bool TryFindFreeGroundTile(out Point tile)
        {
            foreach (Point t in IntakeTiles())
            {
                if (!WorldGen.InWorld(t.X, t.Y))
                    continue;

                Tile neighbour = Main.tile[t.X, t.Y];
                if (neighbour.HasTile && Main.tileSolid[neighbour.TileType])
                    continue;

                if (VirtualItemSystem.IsTileOccupied(t.X, t.Y))
                    continue;

                tile = t;
                return true;
            }

            tile = default;
            return false;
        }

        // Called by RoboticArmDropCapture for every item a strike spawns. Turns the drop into a vItem:
        //  - a conveyor in the 3x3: onto a free conveyor tile (a full belt leaves the drop as a world item; CanDeliverDrops already paused the arm);
        //  - no conveyor and no player near (and not on this screen): onto a free neighbouring tile, so the world item cap is not eaten by unseen drops;
        //  - no conveyor and someone is near: left alone, so it drops into the world like vanilla.
        public void RouteDrop(Item item)
        {
            Point tile;
            bool found;

            if (HasAdjacentConveyor())
                found = TryFindFreeAdjacentConveyorTile(out tile);
            else if (!IsAnyPlayerNear(Shoulder) && !IsOnScreen(Shoulder))
                found = TryFindFreeGroundTile(out tile);
            else
                return;

            if (!found)
                return;

            if (VirtualItemSystem.SpawnVirtualItem(item.Clone(), tile.X, tile.Y) == null)
                return;

            item.TurnToAir();
            item.active = false;
        }

        bool TilePresent()
        {
            Tile tile = Framing.GetTileSafely(Position.X, Position.Y);
            return tile.HasTile && tile.TileType == ValidTileType;
        }

        public void DynamicUpdate()
        {
            if (!TilePresent())
            {
                visual = null;
                return;
            }

            visual ??= new RoboticArmVisual();

            Item held = InputSlots[0];
            bool heldIsTool = !held.IsAir && IsTool(held);

            // On a multiplayer client no logic tick runs for the arm, so nothing else ticks its motion model here (Phase 7b feeds its target
            // and strikes from the server's events). In single-player and on the server the entity's own Update already did.
            if (Main.netMode == NetmodeID.MultiplayerClient)
                Motion.Update(Shoulder, UpperArmLength, ForearmLength, held, heldIsTool);

            visual.Update(Motion, Shoulder, UpperArmLength, ForearmLength, held, heldIsTool);
        }

        public bool TryGetDynamicBounds(out Vector2 center, out float radius)
        {
            center = Shoulder;
            radius = Reach + 70f; // the hand can sit at full reach and a tool sprite sticks out past it
            return visual != null;
        }

        // Nothing is drawn in front of the tiles: the whole arm is drawn in DynamicDrawBehindTiles.
        public void DynamicDraw(SpriteBatch spriteBatch) { }

        // Behind the tiles: the beams, the elbow joint, the claw and the held item.
        public void DynamicDrawBehindTiles(SpriteBatch spriteBatch)
        {
            Item held = InputSlots[0];
            visual?.DrawBehind(spriteBatch, Shoulder, held, !held.IsAir && IsTool(held));
        }
    }
}