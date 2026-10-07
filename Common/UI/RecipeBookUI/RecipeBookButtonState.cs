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

        // Button top-left minus the defense icon position. Saved per character (RecipeBookPlayer) once the player drags the button.
        static Vector2? UserOffset
        {
            get => Main.LocalPlayer.GetModPlayer<RecipeBookPlayer>().ButtonOffset;
            set => Main.LocalPlayer.GetModPlayer<RecipeBookPlayer>().ButtonOffset = value;
        }

        // Used until the player drags the button (or RecipeBookLayout.ReferenceDefenseY is calibrated): anchored the first time
        // the inventory draws, so the button starts at the fixed reference spot and follows the defense icon from then on.
        static Vector2? sessionAnchor;

        bool dragging;
        Vector2 grab;     // mouse position minus the button's top-left when the drag started

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

            if (sessionAnchor == null)
            {
                sessionAnchor = RecipeBookLayout.ReferenceDefenseY > 0
                    ? new Vector2(RecipeBookLayout.ButtonOffsetX, RecipeBookLayout.ButtonOffsetY)
                    : new Vector2(RecipeBookLayout.ButtonX, RecipeBookLayout.ButtonY) - defense;
            }

            UpdateDrag(defense);

            Vector2 offset = UserOffset ?? sessionAnchor.Value;
            Button.Left.Set(defense.X + offset.X, 0f);
            Button.Top.Set(defense.Y + offset.Y, 0f);
            Button.Recalculate();
        }

        void UpdateDrag(Vector2 defense)
        {
            if (!dragging)
            {
                if (Main.mouseRight && Main.mouseRightRelease && Button.MouseInside)
                {
                    dragging = true;
                    grab = UserInterface.ActiveInstance.MousePosition - new Vector2(Button.Left.Pixels, Button.Top.Pixels);
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

            UserOffset = topLeft - defense;
        }
    }
}