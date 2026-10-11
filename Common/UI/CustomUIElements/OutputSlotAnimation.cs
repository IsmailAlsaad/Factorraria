using Factorraria.Common.Machines;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.UI;

namespace Factorraria.Common.UI.CustomUIElements
{
    /// <summary>
    /// Custom draw for a machine's output slot (plugged into UIItemSlotWrapper.customDraw).
    /// Layers, back to front: red slot -> blue slot revealed bottom-to-top by WorkFraction ->
    /// (only while the slot is empty) faded ghost of the output item + colored item revealed the same way ->
    /// the real item and stack count, which covers the ghost once at least 1 is present.
    /// Also plays the finish sound when the machine's craft counter changes while the UI is open.
    /// </summary>
    public class OutputSlotAnimation
    {
        readonly Func<BaseMachine> getMachine;
        readonly int slotIndex;

        // Tweakables (instance fields on purpose).
        Color ghostTint = RecipeIcon.FadedTint;
        float itemBox = 32f;                                          // vanilla inventory icon box
        uint staleAfterTicks = 30;                                    // not drawn for this long = UI was closed
        BaseMachine lastMachine;
        int lastCrafts;
        uint lastDrawTick;

        public OutputSlotAnimation(Func<BaseMachine> _getMachine, int _slotIndex)
        {
            getMachine = _getMachine;
            slotIndex = _slotIndex;
        }

        public void Draw(SpriteBatch sb, CalculatedStyle dims, Item item)
        {
            BaseMachine machine = getMachine();
            Texture2D blueBack = TextureAssets.InventoryBack.Value;
            Texture2D redBack = TextureAssets.InventoryBack5.Value;

            float scale = dims.Width / blueBack.Width;
            Vector2 pos = dims.Position();
            float fraction = machine == null ? 0f : MathHelper.Clamp(machine.WorkFraction, 0f, 1f);

            if (machine != null) PlayFinishSound(machine);

            sb.Draw(redBack, pos, null, Main.inventoryBack, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            DrawBottomUp(sb, blueBack, blueBack.Bounds, pos, scale, Main.inventoryBack, fraction);

            if (item.IsAir)
            {
                int ghostType = machine == null ? -1 : machine.GetPendingOutputType(slotIndex);
                if (ghostType > 0) DrawGhost(sb, ghostType, pos, scale, fraction);
                return;
            }

            // Real item on top (same placement/stack text as vanilla ItemSlot.Draw).
            Vector2 center = pos + new Vector2(blueBack.Width, blueBack.Height) * 0.5f * scale;
            ItemSlot.DrawItemIcon(item, ItemSlot.Context.ChestItem, sb, center, scale, itemBox, Color.White);
            if (item.stack > 1)
            {
                Utils.DrawBorderStringFourWay(sb, FontAssets.ItemStack.Value, item.stack.ToString(),
                    pos.X + 10f * scale, pos.Y + 26f * scale, Color.White, Color.Black, Vector2.Zero, scale);
            }
        }

        void DrawGhost(SpriteBatch sb, int type, Vector2 slotPos, float scale, float fraction)
        {
            Main.instance.LoadItem(type);
            Texture2D tex = TextureAssets.Item[type].Value;
            Rectangle frame = Main.itemAnimations[type]?.GetFrame(tex) ?? tex.Bounds;

            float fit = Math.Min(1f, itemBox / Math.Max(frame.Width, frame.Height)) * scale;
            Vector2 slotCenter = slotPos + new Vector2(TextureAssets.InventoryBack.Value.Width, TextureAssets.InventoryBack.Value.Height) * 0.5f * scale;
            Vector2 topLeft = slotCenter - new Vector2(frame.Width, frame.Height) * fit * 0.5f;

            DrawBottomUp(sb, tex, frame, topLeft, fit, ghostTint, 1f);        // faded ghost (always whole)
            
            // One sweep line shared with the blue slot fill: slot bottom -> slot top as fraction goes 0 -> 1.
            // The sprite shows only the part of itself that is below that line.
            float slotHeight = TextureAssets.InventoryBack.Value.Height * scale;
            float lineY = slotPos.Y + slotHeight * (1f - fraction);
            float spriteHeight = frame.Height * fit;
            float spriteBottom = topLeft.Y + spriteHeight;
            float spriteFraction = MathHelper.Clamp((spriteBottom - lineY) / spriteHeight, 0f, 1f);

            DrawBottomUp(sb, tex, frame, topLeft, fit, Color.White, spriteFraction); // colored sprite, cut at the shared line
        }

        // Same slicing idea as FireUIElement: keep the bottom `fraction` of the frame, anchored to the bottom edge.
        static void DrawBottomUp(SpriteBatch sb, Texture2D tex, Rectangle frame, Vector2 topLeft, float drawScale, Color color, float fraction)
        {
            int h = (int)(frame.Height * fraction);
            if (h <= 0) return;

            int yOffset = frame.Height - h;
            Rectangle slice = new Rectangle(frame.X, frame.Y + yOffset, frame.Width, h);
            sb.Draw(tex, topLeft + new Vector2(0f, yOffset * drawScale), slice, color, 0f, Vector2.Zero, drawScale, SpriteEffects.None, 0f);
        }

        // Only slot 0 plays it, so multi-output machines don't stack the sound. Resyncs silently
        // when the UI was just opened or switched to another machine.
        void PlayFinishSound(BaseMachine machine)
        {
            uint now = Main.GameUpdateCount;
            bool stale = now - lastDrawTick > staleAfterTicks;
            lastDrawTick = now;

            if (stale || !ReferenceEquals(machine, lastMachine))
            {
                lastMachine = machine;
                lastCrafts = machine.CraftsFinished;
                return;
            }

            if (machine.CraftsFinished == lastCrafts) return;
            lastCrafts = machine.CraftsFinished;

            if (slotIndex == 0)
            {
                SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.5f }, machine.MachineCenter);
            }
        }
    }
}