using Factorraria.Common.Machines;
using Factorraria.Common.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.RoboticArm
{
    // One input slot (a pickaxe, axe, hammer or a block) and one output slot. No recipes.
    public class RoboticArmTileEntity : ElectricConsumerMachine, IMachineDynamicDraw
    {
        public override int ValidTileType => ModContent.TileType<RoboticArmTile>();
        public override float PowerDemand => 100f;
        protected override int InputSlotCount => 1;
        protected override int OutputSlotCount => 1;

        // Arm geometry (pixels). Reach is what the work area is clamped to in Phase 3.
        public float UpperArmLength = 56f;
        public float ForearmLength = 56f;
        public float Reach => UpperArmLength + ForearmLength;

        // Centre of the base tile: the arm's shoulder.
        public Vector2 Shoulder => new Vector2(Position.X * 16f + 8f, Position.Y * 16f + 8f);

        // Client-only animation state. Never saved and never synced; created on the first client tick.
        RoboticArmVisual visual;
        public RoboticArmVisual Visual => visual;

        // Working area: the tile offset of its top-left corner from the base tile, and its size in tiles. Saved. Phase 3's overlay
        // edits it (RoboticArmAreaEditorSystem) and Phase 4's scanner reads it. It always stays inside the square of tiles the arm
        // can reach (ReachTiles each way from the base), which is also its size cap.
        public int AreaX = -2;
        public int AreaY = -2;
        public int AreaW = 5;
        public int AreaH = 5;

        public int ReachTiles => (int)MathF.Ceiling(Reach / 16f);
        public Rectangle AreaTiles => new Rectangle(Position.X + AreaX, Position.Y + AreaY, AreaW, AreaH);

        public void SetArea(int x, int y, int w, int h)
        {
            int r = ReachTiles;
            int left = Math.Clamp(x, -r, r);
            int top = Math.Clamp(y, -r, r);
            int right = Math.Clamp(x + Math.Max(w, 1) - 1, left, r);
            int bottom = Math.Clamp(y + Math.Max(h, 1) - 1, top, r);

            AreaX = left;
            AreaY = top;
            AreaW = right - left + 1;
            AreaH = bottom - top + 1;
        }

        // Whether the middle of a tile is inside the reach circle (the overlay tints the rest red; the worker skips them).
        public bool IsTileInReach(int tileX, int tileY) =>
            Vector2.DistanceSquared(new Vector2(tileX * 16f + 8f, tileY * 16f + 8f), Shoulder) <= Reach * Reach;

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
        }

        public bool IsTool(Item item) => item.pick > 0 || item.axe > 0 || item.hammer > 0;

        // v1 only places plain solid blocks (the filter the spike validated): no furniture, torches, platforms, seeds,
        // grass-type or moss tiles. Keeping the slot and the conveyor intake to the same rule means nothing can sit in the
        // slot that the arm will never place.
        public bool IsBlock(Item item)
        {
            int tile = item.createTile;
            if (tile < 0 || IsTool(item))
                return false;

            if (!Main.tileSolid[tile] || Main.tileFrameImportant[tile] || Main.tileMoss[tile])
                return false;

            return tile is not (2 or 23 or 109 or 199 or 477 or 492 or 633 or 661 or 662 or 60 or 70);
        }

        // What the input slot accepts from the cursor.
        public bool IsAcceptedItem(Item item)
        {
            if (item == null || item.IsAir)
                return false;

            return IsTool(item) || IsBlock(item);
        }

        // Conveyors only ever hand over blocks, and only while the slot is empty or already holds that block;
        // a loaded tool is never replaced.
        protected override bool TryGetIntakeSlot(int itemType, out int slot, out int limit)
        {
            slot = 0;
            limit = 0;

            if (!ContentSamples.ItemsByType.TryGetValue(itemType, out Item sample) || !IsBlock(sample))
                return false;

            Item held = InputSlots[0];
            if (!held.IsAir && held.type != itemType)
                return false;

            limit = sample.maxStack;
            return true;
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
            visual.Update(Shoulder, UpperArmLength, ForearmLength, held, !held.IsAir && IsTool(held));
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