using Factorraria.Common.Liquids;
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
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.UI.Elements;
using Terraria.UI;

namespace Factorraria.Common.UI.CustomUIElements
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
            HammerIcon = ModContent.Request<Texture2D>("Factorraria/Common/UI/CustomUIElements/RecipeSelectHammerIcon");
            HammerIconHover = ModContent.Request<Texture2D>("Factorraria/Common/UI/CustomUIElements/RecipeSelectHammerIcon_Hover");
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
        float zoom = 1f;

        const float SearchHeight = 24f;
        RecipeSearchBar searchBar;
        string searchFilter = "";
        bool listDirty;

        readonly List<RecipeOutputGroup> machineGroups;
        readonly Func<RecipeOutputGroup> getSelectedGroup;
        readonly Action<RecipeOutputGroup> setSelectedGroup;

        public RecipeBrowserPanel(List<RecipeOutputGroup> groups, Func<RecipeOutputGroup> getSelected, Action<RecipeOutputGroup> setSelected)
        {
            machineGroups = groups;
            getSelectedGroup = getSelected;
            setSelectedGroup = setSelected;
        }

        public void PopulateRecipeList()
        {
            recipeList.Clear();
            RecipeOutputGroup current = getSelectedGroup?.Invoke();

            for (int i = 0; i < machineGroups.Count; i++)
            {
                if (!MatchesFilter(machineGroups[i])) continue;

                var cell = new RecipeElement(machineGroups[i], i);
                cell.Selected = ReferenceEquals(machineGroups[i], current);  // restores the highlight when the UI reopens
                cell.OnSelected = SelectRecipe;
                cell.SetZoomScale(zoom);
                recipeList.Add(cell);
            }

            recipeList.Recalculate();
        }

        bool MatchesFilter(RecipeOutputGroup group)
        {
            if (string.IsNullOrWhiteSpace(searchFilter)) return true;
            string name = group.Key.DisplayName;
            return name.Contains(searchFilter.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        void SelectRecipe(RecipeElement chosen)
        {
            bool wasSelected = chosen.Selected;

            foreach (var cell in recipeList.OfType<RecipeElement>())
                cell.Selected = !wasSelected && cell == chosen;

            setSelectedGroup?.Invoke(wasSelected ? null : chosen.CurrentGroup);

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

            searchBar = new RecipeSearchBar(text =>
            {
                searchFilter = text;
                listDirty = true;
            });

            Append(searchBar);
            Append(scrollbar);
            Append(recipeList);

            PopulateRecipeList();
            SetZoomScale(zoom);
        }
        
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (listDirty)
            {
                listDirty = false;
                PopulateRecipeList();
                scrollbar.ViewPosition = 0f;
            }

            if (!showPanel)
            {
                searchBar?.Unfocus();
            }

            if (showPanel && IsMouseHovering)
            {
                Main.LocalPlayer.mouseInterface = true;
            }
        }


        public void SetZoomScale(float z)
        {
            zoom = z;
            if (scrollbar == null || recipeList == null || searchBar == null) return;

            SetPadding(PanelPadding * z);

            scrollbar.SetZoomScale(z);

            float listTop = (SearchHeight + Gap) * z;

            searchBar.Width.Set(0f, 1f);
            searchBar.Height.Set(SearchHeight * z, 0f);
            searchBar.SetZoomScale(z);

            scrollbar.Top.Set(listTop, 0f);
            scrollbar.Height.Set(-listTop, 1f);

            recipeList.Top.Set(listTop, 0f);
            recipeList.Height.Set(-listTop, 1f);

            recipeList.Width.Set(-(ScrollbarWidth + Gap) * z, 1f);
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

        public Action<RecipeElement> OnSelected;

        public string ProductName;
        Texture2D recipeTexture;
        Color? swatchColor;   // set when a liquid has no icon PNG
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

        public readonly RecipeOutputGroup CurrentGroup;
        public readonly int Index;                 // original position in the registry's group list

        public RecipeElement(RecipeOutputGroup group, int index)
        {
            CurrentGroup = group;
            Index = index;
            SetPadding(0f);
            BackgroundColor = NormalColor;
            SetZoomScale(1f);

            ProductName = group.Key.DisplayName;

            if (group.Key.IsLiquid)
            {
                LiquidTypeDefinition liquid = LiquidTypeRegistry.Get(group.Key.Id);
                if (liquid.IconPath != null) recipeTexture = ModContent.Request<Texture2D>(liquid.IconPath).Value;
                else swatchColor = liquid.RenderColor;
            }
            else
            {
                Main.instance.LoadItem(group.Key.Id);
                recipeTexture = TextureAssets.Item[group.Key.Id].Value;
            }
        }

        // UIGrid sorts its items with CompareTo. The default returns 0 for everything,
        // which gives no guaranteed order, so we define one: selected first, then registry order.
        public override int CompareTo(object obj) =>
            obj is RecipeElement other ? SortKey.CompareTo(other.SortKey) : 0;

        int SortKey => Selected ? -1 : Index;

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

            if (IsMouseHovering)
            {
                Main.hoverItemName = ProductName;
            }

            if (swatchColor.HasValue)
            {
                int s = (int)(IconBox * 0.6f * zoom);
                Vector2 c = GetDimensions().Center();
                sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)c.X - s / 2, (int)c.Y - s / 2, s, s), swatchColor.Value);
                return;
            }
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

    public class RecipeSearchBar : UIElement
    {
        const int MaxLength = 24;
        const string Hint = "Search...";

        static readonly Color BgColor = new Color(20, 26, 55) * 0.9f;
        static readonly Color FocusBorder = new Color(255, 220, 60);
        static readonly Color IdleBorder = Color.Black;

        readonly Action<string> onTextChanged;
        string text = "";
        bool focused;
        float zoom = 1f;

        public RecipeSearchBar(Action<string> onTextChanged)
        {
            this.onTextChanged = onTextChanged;
        }

        public void SetZoomScale(float z) => zoom = z;

        public void Unfocus() => focused = false;

        public override void LeftClick(UIMouseEvent evt)
        {
            base.LeftClick(evt);
            if (!focused)
            {
                focused = true;

                Main.clrInput(); // drop any keys buffered before focusing
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
        }

        public override void RightClick(UIMouseEvent evt)
        {
            base.RightClick(evt);
            if (text.Length > 0)
            {
                text = "";
                onTextChanged?.Invoke(text);
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (IsMouseHovering)
                Main.LocalPlayer.mouseInterface = true;

            if (!focused) return;

            // Clicking anywhere else releases focus
            if (Main.mouseLeft && !IsMouseHovering)
            {
                focused = false;
                return;
            }

            //PlayerInput.WritingText = true; // blocks keybinds (inventory key, hotbar, etc.) while typing
            //Main.instance.HandleIME();

            //string newText = Main.GetInputText(text);

            //if (newText != text) Main.NewText("typed: " + newText);

            //if (newText.Length > MaxLength)
            //    newText = newText.Substring(0, MaxLength);

            //if (newText != text)
            //{
            //    text = newText;
            //    onTextChanged?.Invoke(text);
            //}

            //if (Main.inputTextEnter || Main.inputTextEscape)
            //{
            //    Main.inputTextEnter = false;
            //    Main.inputTextEscape = false;
            //    focused = false;
            //}
        }

        protected override void DrawSelf(SpriteBatch sb)
        {
            if (focused)
            {
                PlayerInput.WritingText = true; // blocks keybinds (inventory key, hotbar, etc.) while typing
                Main.instance.HandleIME();

                string newText = Main.GetInputText(text);

                if (newText.Length > MaxLength)
                    newText = newText.Substring(0, MaxLength);

                if (newText != text)
                {
                    text = newText;
                    onTextChanged?.Invoke(text);
                }

                if (Main.inputTextEnter || Main.inputTextEscape)
                {
                    Main.inputTextEnter = false;
                    Main.inputTextEscape = false;
                    focused = false;
                }
            }

            CalculatedStyle dims = GetDimensions();
            ScaledPanel.Draw(sb, dims.ToRectangle(), BgColor, focused ? FocusBorder : IdleBorder, zoom);

            var font = FontAssets.MouseText.Value;
            float scale = 0.9f * zoom;
            float pad = 8f * zoom;
            float available = dims.Width - pad * 2f;

            bool showHint = text.Length == 0 && !focused;
            string display = showHint ? Hint : text;
            if (focused && (int)(Main.GlobalTimeWrappedHourly * 2f) % 2 == 0)
                display += "|";

            // Long text: keep the tail visible, like a normal text field
            while (display.Length > 1 && font.MeasureString(display).X * scale > available)
                display = display.Substring(1);

            float lineHeight = font.MeasureString("Ag").Y * scale;
            Vector2 pos = new Vector2(dims.X + pad, dims.Y + 4f * scale + (dims.Height - lineHeight) / 2f);
            Utils.DrawBorderString(sb, display, pos, showHint ? Color.Gray : Color.White, scale);
        }
    }
}
