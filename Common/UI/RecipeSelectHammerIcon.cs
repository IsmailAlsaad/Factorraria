using Factorraria.Common.Machines;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
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

    public class RecipeBrowserPanel : UIPanel
    {
        Vector2 recipeListOriginalSize;
        Vector2 recipeListOriginalPose;
        public UIList recipeList;

        Vector2 scrollbarOriginalView;
        Vector2 scrollbarOriginalSize;
        Vector2 scrollbarOriginalPose;
        public UIScrollbar scrollbar;
        List<CustomRecipe> machineRecipes;

        public RecipeBrowserPanel(List<CustomRecipe> _machineRecipes)
        {
            machineRecipes = _machineRecipes;
        }

        public bool showPanel = false;

        public override void OnInitialize()
        {
            Left.Set(50, 0);
            Top.Set(0, 0);

            recipeList = new UIList();
            scrollbar = new UIScrollbar();

            scrollbar = new UIScrollbar();
            scrollbarOriginalView = new Vector2(100f, 1000f);
            scrollbar.SetView(scrollbarOriginalView.X, scrollbarOriginalView.Y);
            scrollbarOriginalPose = new Vector2(10f, -25f);
            scrollbar.Top.Set(scrollbarOriginalPose.X, 0f);
            scrollbar.Left.Set(scrollbarOriginalPose.Y, 1f);
            scrollbarOriginalSize = new Vector2(20f, -20f);
            scrollbar.Width.Set(scrollbarOriginalSize.X, 0f);
            scrollbar.Height.Set(scrollbarOriginalSize.Y, 1f);
            Append(scrollbar);

            recipeList = new UIList();
            recipeListOriginalPose = new Vector2(10f, 10f);
            recipeList.Top.Set(recipeListOriginalPose.X, 0f);
            recipeList.Left.Set(recipeListOriginalPose.Y, 0f);
            recipeListOriginalSize = new Vector2(40f, -20f);
            recipeList.Width.Set(recipeListOriginalSize.X, 1f);
            recipeList.Height.Set(recipeListOriginalSize.Y, 1f);
            recipeList.ListPadding = 4f;   
            Append(recipeList); 

            recipeList.SetScrollbar(scrollbar);

            if(machineRecipes == null)
            {
                return;
            }

            PopulateRecipeList();
        }

        public override void OnActivate()
        {
            Left.Set(50, 0);
            Top.Set(0, 0);
            Width.Set(1000, 0f);
            Height.Set(1000, 0f);

            scrollbarOriginalView = new Vector2(100f, 1000f);
            scrollbar.SetView(scrollbarOriginalView.X, scrollbarOriginalView.Y);
            scrollbarOriginalPose = new Vector2(10f, 0f);
            scrollbar.Top.Set(scrollbarOriginalPose.X, 0f);
            scrollbar.Left.Set(scrollbarOriginalPose.Y, 1f);
            scrollbarOriginalSize = new Vector2(200f, 200f);
            scrollbar.Width.Set(scrollbarOriginalSize.X, 0f);
            scrollbar.Height.Set(scrollbarOriginalSize.Y, 0f);

            recipeListOriginalPose = new Vector2(10f, 0f);
            recipeList.Top.Set(recipeListOriginalPose.X, 0f);
            recipeList.Left.Set(recipeListOriginalPose.Y, 0f);
            recipeListOriginalSize = new Vector2(400f, 200f);
            recipeList.Width.Set(recipeListOriginalSize.X, 1f);
            recipeList.Height.Set(recipeListOriginalSize.Y, 1f);
            recipeList.ListPadding = 4f;

            PopulateRecipeList();
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (!showPanel)
            {
                return;
            }

            //setZoomScale();

            base.Draw(spriteBatch);
        }

        //void setZoomScale()
        //{
        //    float zoomScale = Main.GameViewMatrix.ZoomMatrix.M11 / Main.UIScale;

        //    scrollbar.SetView(scrollbarOriginalView.X * zoomScale, scrollbarOriginalView.Y * zoomScale);
        //    scrollbar.Top.Set(scrollbarOriginalPose.X * zoomScale, 0f);
        //    scrollbar.Left.Set(scrollbarOriginalPose.Y * zoomScale, 1f);
        //    scrollbar.Width.Set(scrollbarOriginalSize.X * zoomScale, 0f);
        //    scrollbar.Height.Set(scrollbarOriginalSize.Y * zoomScale, 0f);

        //    recipeList.Top.Set(recipeListOriginalPose.X * zoomScale, 0f);
        //    recipeList.Left.Set(recipeListOriginalPose.Y * zoomScale, 0f);
        //    recipeList.Width.Set(recipeListOriginalSize.X * zoomScale, 1f);
        //    recipeList.Height.Set(recipeListOriginalSize.Y * zoomScale, 1f);

        //    foreach (var recipeElement in recipeList.OfType<RecipeElement>())
        //    {

        //        recipeElement.Top.Set(recipeElement.originalPose.X * zoomScale, 0f);
        //        recipeElement.Left.Set(recipeElement.originalPose.Y * zoomScale, 0f);
        //        recipeElement.Width.Set(recipeElement.originalSize.X * zoomScale, 1f);
        //        recipeElement.Height.Set(recipeElement.originalSize.Y * zoomScale, 1f);
        //    }
        //}

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
        //public Vector2 originalSize;
        //public Vector2 originalPose;

        private CustomRecipe recipe;
        public RecipeElement(CustomRecipe recipe)
        {
            this.recipe = recipe;
        }

        Texture2D recipeTexture;

        public override void OnInitialize()
        {
            // check if the player has unlocked this recipe OR has its ingredients in his inventory currently

            Main.instance.LoadItem(recipe.Output.Type);
            recipeTexture = TextureAssets.Item[recipe.Output.Type].Value;

            //originalPose = new Vector2(0f, 0f);
            //originalSize = new Vector2(50f, 50f);

            //Width.Set(originalSize.X, 0f);
            //Height.Set(originalSize.Y, 0f);

            Width.Set(50f, 0f);
            Height.Set(50f, 0f);

        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            base.DrawSelf(spriteBatch);

            if (recipeTexture != null)
            {
                Vector2 drawPosition = GetInnerDimensions().Center();
                Vector2 origin = recipeTexture.Size() / 2f;

                spriteBatch.Draw(recipeTexture, drawPosition, null, Color.White, 0f, origin, 1f, SpriteEffects.None, 0f);
            }
        }
    }
}
