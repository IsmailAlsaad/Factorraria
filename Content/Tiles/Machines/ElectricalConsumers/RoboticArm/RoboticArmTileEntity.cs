using Factorraria.Common.Machines;
using Factorraria.Common.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

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

        // PHASE 2 DEBUG (removed in Phase 4): ticks to wait before the debug controller starts the next strike.
        int debugStrikeCooldown;

        public override void Update()
        {
            base.Update();

            // Phase 1 stand-in so power draw and brownout can be tested before the arm has any work to do:
            // it counts as working whenever the slot is loaded. Phase 4 replaces this with a real "has a valid job" check.
            isWorking = !InputSlots[0].IsAir;
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
            DebugFollowCursor();

            Item held = InputSlots[0];
            visual.Update(Shoulder, UpperArmLength, ForearmLength, held, !held.IsAir && IsTool(held));
        }

        public bool TryGetDynamicBounds(out Vector2 center, out float radius)
        {
            center = Shoulder;
            radius = Reach + 70f; // the hand can sit at full reach and a tool sprite sticks out past it
            return visual != null;
        }

        public void DynamicDraw(SpriteBatch spriteBatch)
        {
            Item held = InputSlots[0];
            visual?.Draw(spriteBatch, Shoulder, UpperArmLength, ForearmLength, held, !held.IsAir && IsTool(held));
        }

        // PHASE 2 DEBUG (removed in Phase 4, when the worker drives the visual): while this arm's UI is open, the arm follows the
        // tile under the cursor and keeps striking at it with whatever is in the slot, so the animation can be judged without a worker.
        void DebugFollowCursor()
        {
            if (ModContent.GetInstance<MachineUISystem>().OpenMachine != this)
            {
                visual.SetTarget(null);
                debugStrikeCooldown = 0;
                return;
            }

            if (!Main.LocalPlayer.mouseInterface)
            {
                Point mouseTile = Main.MouseWorld.ToTileCoordinates();
                if (mouseTile.X == Position.X && mouseTile.Y == Position.Y)
                    visual.SetTarget(null);
                else
                    visual.SetTarget(new Vector2(mouseTile.X * 16f + 8f, mouseTile.Y * 16f + 8f));
            }

            if (debugStrikeCooldown > 0)
                debugStrikeCooldown--;

            Item held = InputSlots[0];
            if (!held.IsAir && visual.HasTarget && visual.InPosition && !visual.Striking && debugStrikeCooldown <= 0)
            {
                visual.StartStrike(Math.Max(held.useTime, 8));
                debugStrikeCooldown = 4;
            }
        }
    }
}