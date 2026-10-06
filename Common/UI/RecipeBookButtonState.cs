using Microsoft.Xna.Framework;
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

            //Button.Top.Set(0, 0);
            //Button.Left.Set(0, 0);
        }
    }
}