using Factorraria.Common.Machines;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.UI.Elements;
using Terraria.UI;

namespace Factorraria.Common.UI
{
    public class RecipeSelectHammerIcon : UIElement
    {
        Asset<Texture2D> HammerIcon;
        Asset<Texture2D> HammerIconHover;

        Asset<Texture2D> CurrentTexture => IsMouseHovering ? HammerIconHover : HammerIcon;

        RecipeBrowserPanel recipeBrowserPanel;

        public RecipeSelectHammerIcon(RecipeBrowserPanel browserPanel)
        {
            recipeBrowserPanel = browserPanel;
        }

        public override void OnInitialize()
        {
            HammerIcon = ModContent.Request<Texture2D>("Factorraria/Common/UI/RecipeSelectHammerIcon");
            HammerIconHover = ModContent.Request<Texture2D>("Factorraria/Common/UI/RecipeSelectHammerIcon_Hover");
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            CalculatedStyle dimensions = GetDimensions();
            Vector2 drawPosition = dimensions.Position();

            float scale = dimensions.Width / HammerIcon.Width();

            spriteBatch.Draw(CurrentTexture.Value, drawPosition, null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        public override void Update(GameTime gameTime)
        {
            if(IsMouseHovering)
            {
                Main.LocalPlayer.mouseInterface = true;
            }
        }

        public override void LeftClick(UIMouseEvent evt)
        {
            base.LeftClick(evt);

            if(recipeBrowserPanel == null)
            {
                Main.NewText("Panel is null");
                return;
            }

            recipeBrowserPanel.showPanel = !recipeBrowserPanel.showPanel;

            SoundEngine.PlaySound(SoundID.MenuTick);
        }
    }

    public class RecipeBrowserPanel : UIPanel, IZoomScalable
    {
        const float ScrollbarWidth = 20f, Gap = 6f, ListPadding = 4f, PanelPadding = 12f;

        public bool showPanel;
        public UIGrid recipeList; 
        public UIScrollbar scrollbar;
        readonly List<CustomRecipe> machineRecipes;
        float zoom = 1f;

        public RecipeBrowserPanel(List<CustomRecipe> recipes) => machineRecipes = recipes;

        public override void OnInitialize()
        {
            scrollbar = new UIScrollbar();
            scrollbar.HAlign = 1f;
            Append(scrollbar);

            recipeList = new UIGrid();
            recipeList.SetScrollbar(scrollbar); 
            Append(recipeList);

            PopulateRecipeList();
            SetZoomScale(zoom);
        }

        public void SetZoomScale(float z)
        {
            zoom = z;
            if (scrollbar == null || recipeList == null) return;

            SetPadding(PanelPadding * z);

            scrollbar.Width.Set(ScrollbarWidth * z, 0f);
            scrollbar.Height.Set(0f, 1f);

            recipeList.Width.Set(-(ScrollbarWidth + Gap) * z, 1f);
            recipeList.Height.Set(0f, 1f);
            recipeList.ListPadding = ListPadding * z;

            foreach (var cell in recipeList.OfType<RecipeElement>())
                cell.SetZoomScale(z);
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (!showPanel)
            {
                return;
            }

            base.Draw(spriteBatch);
        }

        public override bool ContainsPoint(Vector2 point)
        {
            if (!showPanel)
            {
                return false;
            }

            return base.ContainsPoint(point);
        }

        public void PopulateRecipeList()
        {
            recipeList.Clear();

            foreach (var recipe in machineRecipes)
            {
                var recipeElement = new RecipeElement(recipe);
                recipeList.Add(recipeElement);
            }

            recipeList.Recalculate();
        }
    }

    public class RecipeElement : UIPanel
    {
        const float BaseSize = 50f;
        readonly CustomRecipe recipe;
        Texture2D recipeTexture;

        public RecipeElement(CustomRecipe recipe)
        {
            this.recipe = recipe;
            Width.Set(BaseSize, 0f);
            Height.Set(BaseSize, 0f);
        }

        public void SetZoomScale(float z)
        {
            Width.Set(BaseSize * z, 0f);
            Height.Set(BaseSize * z, 0f);
        }

        public override void OnInitialize()
        {
            Main.instance.LoadItem(recipe.Output.Type);
            recipeTexture = TextureAssets.Item[recipe.Output.Type].Value;
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            base.DrawSelf(spriteBatch);
            if (recipeTexture == null) return;

            CalculatedStyle inner = GetInnerDimensions();
            float scale = Math.Min(1f, Math.Min(inner.Width / recipeTexture.Width, inner.Height / recipeTexture.Height));
            spriteBatch.Draw(recipeTexture, inner.Center(), null, Color.White, 0f,
                recipeTexture.Size() / 2f, scale, SpriteEffects.None, 0f);
        }
    }
}
