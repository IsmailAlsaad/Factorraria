using Factorraria.Content.Configs;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace Factorraria.Common.UI.RecipeBookUI
{
    /// <summary>Tiny UIState that only holds the inventory button. Shown by RecipeBookSystem while the inventory is open.</summary>
    public class RecipeBookButtonState : UIState
    {
        public RecipeBookButton Button { get; private set; }

        public override void OnInitialize()
        {
            Button = new RecipeBookButton();
            Append(Button);
        }

        // Button top-left minus the defense icon position (see AnchorPoint). Saved per character (RecipeBookPlayer) once the player drags the button.
        static Vector2? UserOffset
        {
            get => Main.LocalPlayer.GetModPlayer<RecipeBookPlayer>().ButtonOffset;
            set => Main.LocalPlayer.GetModPlayer<RecipeBookPlayer>().ButtonOffset = value;
        }

        /// <summary>
        /// Default placement, as an offset from the anchor (the defense icon point). tModLoader puts that point at the LAST drawn accessory
        /// row, in the equipment column's x. The button goes one row up (the equipment slot before the last one), just left of that row's
        /// dye slot, and is centered vertically on the slot. Everything is computed from the anchor each frame, so it follows resolution,
        /// UI scale and the number of accessory slots, and nothing is captured once and kept.
        /// </summary>
        static Vector2 DefaultOffset()
        {
            float rowPitch = 56f * 0.85f;      // vertical step between accessory rows (tModLoader draws the column at inventoryScale 0.85)
            float slotSize = 52f * 0.85f;      // one slot's square
            float rowLeft = 47f * 2f;          // vanity slot is 47px left of the equipment slot, the dye slot 94px (hardcoded in AccessorySlotLoader)
            float gap = 6f;                    // space between the button and the dye slot
            return new Vector2(-(rowLeft + gap + RecipeBookLayout.ButtonSize), -rowPitch + (slotSize - RecipeBookLayout.ButtonSize) / 2f);
        }

        bool dragging;
        Vector2 grab;     // mouse position minus the button's top-left when the drag started

        // Own right-click press detection. Main.mouseRightRelease can already be cleared by other code before UpdateUI runs.
        static bool rightWasDown;
        static uint lastPollTick;

        /// <summary>
        /// Reference point for the button: tModLoader's defense icon point. Its Y is the row of the LAST DRAWN accessory slot, so it moves
        /// down when an extra slot appears (Expert/Master worlds, mod slots) and the button moves with it. It is computed from the
        /// current window size and UI scale every draw, so it follows them too.
        /// </summary>
        static Vector2 AnchorPoint(Vector2 defense) => defense;

        /// <summary>Forget the dragged position (back to the default placement).</summary>
        public static void ResetPosition()
        {
            UserOffset = null;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);       // updates the button (MouseInside)
            PositionButton();
        }

        /// <summary>
        /// Keeps the button at a fixed offset from tModLoader's defense icon position, so it follows window size, UI scale,
        /// the minimap and the number of accessory slots. Hold right mouse on the button to drag it; the new offset is saved.
        /// Both the icon position and this UIState use the same UI-scaled coordinates, so no extra scaling is applied.
        /// </summary>
        void PositionButton()
        {
            if (Button == null) return;

            Vector2 defense = AccessorySlotLoader.DefenseIconPosition;
            if (defense == Vector2.Zero) return;                      // inventory has not drawn its accessory column yet

            Vector2 anchor = AnchorPoint(defense);

            UpdateDrag(anchor);

            Vector2 offset = UserOffset ?? DefaultOffset();
            Button.Left.Set(anchor.X + offset.X, 0f);
            Button.Top.Set(anchor.Y + offset.Y, 0f);
            Button.Recalculate();
        }

        void UpdateDrag(Vector2 anchor)
        {
            // A right press = mouseRight is down now and was not down on the previous poll. If the button was hidden for a while
            // (inventory closed) the old state is stale, so a held button is never treated as a new press.
            bool stale = Main.GameUpdateCount - lastPollTick > 1;
            lastPollTick = Main.GameUpdateCount;
            if (stale) { rightWasDown = Main.mouseRight; dragging = false; }
            bool pressed = Main.mouseRight && !rightWasDown;
            rightWasDown = Main.mouseRight;

            if (!dragging)
            {
                if (pressed && Button.MouseInside)
                {
                    dragging = true;
                    grab = UserInterface.ActiveInstance.MousePosition - new Vector2(Button.Left.Pixels, Button.Top.Pixels);
                    if (ModContent.GetInstance<DevConfig>().EnableDebugs) Main.NewText("[RecipeBook] button drag started");
                }
                return;
            }

            if (!Main.mouseRight) { dragging = false; return; }

            Main.LocalPlayer.mouseInterface = true;

            // Keep the whole button on screen (UI-scaled screen size).
            float maxX = Main.screenWidth / Main.UIScale - RecipeBookLayout.ButtonSize;
            float maxY = Main.screenHeight / Main.UIScale - RecipeBookLayout.ButtonSize;
            Vector2 topLeft = UserInterface.ActiveInstance.MousePosition - grab;
            topLeft = new Vector2(MathHelper.Clamp(topLeft.X, 0f, maxX), MathHelper.Clamp(topLeft.Y, 0f, maxY));

            UserOffset = topLeft - anchor;
        }
    }
}