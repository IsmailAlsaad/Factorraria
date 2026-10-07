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

        // Button top-left minus the accessory column anchor (see AnchorPoint). Saved per character (RecipeBookPlayer) once the player drags the button.
        static Vector2? UserOffset
        {
            get => Main.LocalPlayer.GetModPlayer<RecipeBookPlayer>().ButtonOffset;
            set => Main.LocalPlayer.GetModPlayer<RecipeBookPlayer>().ButtonOffset = value;
        }

        // Used until the player drags the button: anchored the first time the inventory draws, so the button starts at the fixed
        // reference spot (ButtonX/ButtonY) and follows the accessory column anchor from then on. RecipeBookLayout.ReferenceDefenseY is no longer used.
        static Vector2? sessionAnchor;

        bool dragging;
        Vector2 grab;     // mouse position minus the button's top-left when the drag started

        // Own right-click press detection. Main.mouseRightRelease can already be cleared by other code before UpdateUI runs.
        static bool rightWasDown;
        static uint lastPollTick;

        /// <summary>
        /// Stable reference point for the button: the accessory column's X and its TOP (AccessorySlotLoader.DrawVerticalAlignment).
        /// The defense icon sits below the LAST DRAWN slot, so its Y changes with the number of accessory slots shown (an extra
        /// slot appears in Expert/Master worlds), which used to move the button between worlds. The column top does not change
        /// with the world; it still follows window size, UI scale and the minimap.
        /// </summary>
        static Vector2 AnchorPoint(Vector2 defense) => new Vector2(defense.X, AccessorySlotLoader.DrawVerticalAlignment);

        /// <summary>Forget the dragged position and the session anchor (back to the default placement).</summary>
        public static void ResetPosition()
        {
            UserOffset = null;
            sessionAnchor = null;
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

            if (sessionAnchor == null)
                sessionAnchor = new Vector2(RecipeBookLayout.ButtonX, RecipeBookLayout.ButtonY) - anchor;

            UpdateDrag(anchor);

            Vector2 offset = UserOffset ?? sessionAnchor.Value;
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
                    if (ModContent.GetInstance<FurnaceOffsetConfig>().EnableDebugs) Main.NewText("[RecipeBook] button drag started");
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