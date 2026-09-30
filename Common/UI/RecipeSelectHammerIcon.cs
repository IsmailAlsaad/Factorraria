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
        public ZoomScrollbar scrollbar;
        readonly List<CustomRecipe> machineRecipes;
        float zoom = 1f;

        readonly Func<CustomRecipe> getSelectedRecipe;
        readonly Action<CustomRecipe> setSelectedRecipe;

        public RecipeBrowserPanel(List<CustomRecipe> recipes,Func<CustomRecipe> getSelected,Action<CustomRecipe> setSelected)
        {
            machineRecipes = recipes;
            getSelectedRecipe = getSelected;
            setSelectedRecipe = setSelected;
        }

        public void PopulateRecipeList()
        {
            recipeList.Clear();
            CustomRecipe current = getSelectedRecipe?.Invoke();

            for (int i = 0; i < machineRecipes.Count; i++)
            {
                var cell = new RecipeElement(machineRecipes[i], i);
                cell.Selected = ReferenceEquals(machineRecipes[i], current);  // restores the highlight when the UI reopens
                cell.OnSelected = SelectRecipe;
                recipeList.Add(cell);
            }

            recipeList.Recalculate();
        }

        void SelectRecipe(RecipeElement chosen)
        {
            bool wasSelected = chosen.Selected;

            foreach (var cell in recipeList.OfType<RecipeElement>())
                cell.Selected = !wasSelected && cell == chosen;

            setSelectedRecipe?.Invoke(wasSelected ? null : chosen.Recipe);

            recipeList.UpdateOrder();      // selected goes first, or everything returns to registry order
            recipeList.Recalculate();
            scrollbar.ViewPosition = 0f;

            SoundEngine.PlaySound(SoundID.MenuTick);
        }

        public override void OnInitialize()
        {
            recipeList = new UIGrid();

            scrollbar = new ZoomScrollbar(
                () => recipeList.GetInnerDimensions().Height,
                () => recipeList.GetTotalHeight());
            scrollbar.HAlign = 1f;

            recipeList.SetScrollbar(scrollbar);

            Append(scrollbar);
            Append(recipeList);

            PopulateRecipeList();
            SetZoomScale(zoom);
        }
        
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (showPanel && IsMouseHovering)
            {
                Main.LocalPlayer.mouseInterface = true;
            }
        }

        public void SetZoomScale(float z)
        {
            zoom = z;
            if (scrollbar == null || recipeList == null) return;

            SetPadding(PanelPadding * z);

            scrollbar.SetZoomScale(z);
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

        protected override void DrawSelf(SpriteBatch sb) => ScaledPanel.Draw(sb, GetDimensions().ToRectangle(), BackgroundColor, BorderColor, zoom);

        public override bool ContainsPoint(Vector2 point)
        {
            if (!showPanel)
            {
                return false;
            }

            return base.ContainsPoint(point);
        }

    }

    public class RecipeElement : UIPanel
    {
        const float BaseSize = 50f;
        const float IconBox = 34f;

        static readonly Color NormalColor = new Color(63, 82, 151) * 0.7f;   // vanilla UIPanel default
        static readonly Color SelectedColor = new Color(214, 178, 48) * 0.9f;

        public readonly CustomRecipe Recipe;
        public readonly int Index;                 // original position in the registry
        public Action<RecipeElement> OnSelected;

        Texture2D recipeTexture;
        float zoom = 1f;
        bool selected;

        public bool Selected
        {
            get => selected;
            set
            {
                selected = value;
                BackgroundColor = value ? SelectedColor : NormalColor;
            }
        }

        public RecipeElement(CustomRecipe recipe, int index)
        {
            Recipe = recipe;
            Index = index;
            SetPadding(0f);
            BackgroundColor = NormalColor;
            SetZoomScale(1f);
        }

        // UIGrid sorts its items with CompareTo. The default returns 0 for everything,
        // which gives no guaranteed order, so we define one: selected first, then registry order.
        public override int CompareTo(object obj) =>
            obj is RecipeElement other ? SortKey.CompareTo(other.SortKey) : 0;

        int SortKey => Selected ? -1 : Index;

        public override void OnInitialize()
        {
            Main.instance.LoadItem(Recipe.Output.Type);
            recipeTexture = TextureAssets.Item[Recipe.Output.Type].Value;
        }

        public void SetZoomScale(float z)
        {
            zoom = z;
            Width.Set(BaseSize * z, 0f);
            Height.Set(BaseSize * z, 0f);
        }

        public override void LeftClick(UIMouseEvent evt)
        {
            base.LeftClick(evt);
            OnSelected?.Invoke(this);
        }

        static readonly Color HoverBorderColor = new Color(255, 220, 60);
        protected override void DrawSelf(SpriteBatch sb)
        {
            Color border = IsMouseHovering ? HoverBorderColor : BorderColor;
            ScaledPanel.Draw(sb, GetDimensions().ToRectangle(), BackgroundColor, border, zoom);

            if (recipeTexture == null) return;

            float fit = Math.Min(1f, IconBox / Math.Max(recipeTexture.Width, recipeTexture.Height));
            sb.Draw(recipeTexture, GetDimensions().Center(), null, Color.White, 0f,
                recipeTexture.Size() / 2f, fit * zoom, SpriteEffects.None, 0f);
        }
    }

    public static class ScaledPanel
    {
        const int Corner = 12, Bar = 4;   // layout of vanilla's 28x28 panel textures
        static readonly Asset<Texture2D> Bg = Main.Assets.Request<Texture2D>("Images/UI/PanelBackground", AssetRequestMode.ImmediateLoad);
        static readonly Asset<Texture2D> Border = Main.Assets.Request<Texture2D>("Images/UI/PanelBorder", AssetRequestMode.ImmediateLoad);

        public static void Draw(SpriteBatch sb, Rectangle r, Color bg, Color border, float zoom)
        {
            int c = Math.Max(1, (int)Math.Round(Corner * zoom));
            c = Math.Min(c, Math.Min(r.Width, r.Height) / 2);
            Slice(sb, Bg.Value, r, c, bg);
            Slice(sb, Border.Value, r, c, border);
        }

        static void Slice(SpriteBatch sb, Texture2D tex, Rectangle r, int c, Color color)
        {
            int[] dx = { r.X, r.X + c, r.Right - c, r.Right };
            int[] dy = { r.Y, r.Y + c, r.Bottom - c, r.Bottom };
            int[] s = { 0, Corner, Corner + Bar, Corner * 2 + Bar };

            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++)
                    sb.Draw(tex,
                        new Rectangle(dx[col], dy[row], dx[col + 1] - dx[col], dy[row + 1] - dy[row]),
                        new Rectangle(s[col], s[row], s[col + 1] - s[col], s[row + 1] - s[row]),
                        color);
        }
    }

    public class ZoomScrollbar : UIScrollbar
    {
        const float BaseWidth = 20f, BaseCap = 6f, BasePad = 5f;

        readonly Func<float> viewSize, maxSize;
        readonly Asset<Texture2D> track = Main.Assets.Request<Texture2D>("Images/UI/Scrollbar", AssetRequestMode.ImmediateLoad);
        readonly Asset<Texture2D> thumb = Main.Assets.Request<Texture2D>("Images/UI/ScrollbarInner", AssetRequestMode.ImmediateLoad);
        float zoom = 1f, dragOffset;
        bool dragging;

        public ZoomScrollbar(Func<float> viewSize, Func<float> maxSize)
        {
            this.viewSize = viewSize;
            this.maxSize = maxSize;
        }

        public void SetZoomScale(float z)
        {
            zoom = z;
            Width.Set(BaseWidth * z, 0f);
            MaxWidth.Set(BaseWidth * z, 0f);   // vanilla caps this at 20, which would block zoom > 1
            PaddingTop = PaddingBottom = BasePad * z;
        }

        Rectangle ThumbRect()
        {
            CalculatedStyle inner = GetInnerDimensions();
            float view = viewSize();
            float max = Math.Max(maxSize(), view);
            if (max <= 0f) max = 1f;
            return new Rectangle((int)inner.X, (int)(inner.Y + inner.Height * ViewPosition / max),
                                 (int)inner.Width, (int)(inner.Height * view / max));
        }

        public override void LeftMouseDown(UIMouseEvent evt)
        {
            base.LeftMouseDown(evt);   // vanilla still handles click-the-track-to-jump
            if (evt.Target != this) return;
            Rectangle t = ThumbRect();
            dragging = true;
            dragOffset = MathHelper.Clamp(evt.MousePosition.Y - t.Y, 0f, t.Height);
        }

        protected override void DrawSelf(SpriteBatch sb)
        {
            CalculatedStyle inner = GetInnerDimensions();
            Vector2 mouse = UserInterface.ActiveInstance.MousePosition;

            if (dragging)
            {
                if (!Main.mouseLeft) dragging = false;
                else
                {
                    float max = Math.Max(maxSize(), viewSize());
                    ViewPosition = (mouse.Y - inner.Y - dragOffset) / inner.Height * max; // setter clamps
                }
            }

            Rectangle t = ThumbRect();
            bool hover = t.Contains(mouse.ToPoint());
            DrawBar(sb, track.Value, GetDimensions().ToRectangle(), Color.White);
            DrawBar(sb, thumb.Value, t, Color.White * (dragging || hover ? 1f : 0.85f));
        }

        void DrawBar(SpriteBatch sb, Texture2D tex, Rectangle r, Color c)
        {
            int cap = Math.Max(1, (int)(BaseCap * zoom));
            sb.Draw(tex, new Rectangle(r.X, r.Y - cap, r.Width, cap), new Rectangle(0, 0, tex.Width, 6), c);
            sb.Draw(tex, r, new Rectangle(0, 6, tex.Width, 4), c);
            sb.Draw(tex, new Rectangle(r.X, r.Bottom, r.Width, cap), new Rectangle(0, tex.Height - 6, tex.Width, 6), c);
        }
    }
}
