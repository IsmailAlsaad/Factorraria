using Microsoft.Xna.Framework;
using Terraria.ModLoader;
using Terraria.UI;

namespace Factorraria.Common.UI
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

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            PositionButton();
        }

        /// <summary>
        /// Keeps the button at a fixed offset from the vanilla/tModLoader defense icon, so it follows window size,
        /// UI scale, the minimap and the number of accessory slots. The icon position uses the same UI-scaled
        /// coordinates as this UIState, so no extra scaling is applied.
        /// </summary>
        void PositionButton()
        {
            if (Button == null) return;
            if (RecipeBookLayout.ReferenceDefenseY <= 0) return;      // not calibrated: keep the fixed reference position

            Vector2 defense = AccessorySlotLoader.DefenseIconPosition;
            if (defense == Vector2.Zero) return;                      // inventory has not drawn its accessory column yet

            Button.Left.Set(defense.X + RecipeBookLayout.ButtonOffsetX, 0f);
            Button.Top.Set(defense.Y + RecipeBookLayout.ButtonOffsetY, 0f);
            Button.Recalculate();
        }
    }
}