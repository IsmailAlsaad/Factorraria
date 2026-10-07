using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.UI;

namespace Factorraria.Common.UI.RecipeBookUI
{
    /// <summary>
    /// Right page of the recipe book. Header (product icon, name, machine, status) plus, for the selected group,
    /// one recipe at a time: inputs -> arrow -> outputs -> time. Groups with several recipes get prev/next arrows.
    /// Details are shown for parchment-unlocked recipes too (only the product icon is faded). All geometry is in RecipeBookLayout.
    /// </summary>
    public class RecipeBookDetail : UIElement
    {
        readonly RecipeBookState book;
        RecipeOutputGroup shownGroup;
        int recipeIndex;

        // Clickable ingredient boxes of the recipe drawn this frame (box, catalog key to jump to). Refilled every draw.
        readonly List<(Rectangle box, string key)> navHits = new List<(Rectangle box, string key)>();

        public RecipeBookDetail(RecipeBookState book)
        {
            this.book = book;
            Rectangle a = RecipeBookLayout.DetailArea;
            Left.Set(a.X, 0f);
            Top.Set(a.Y, 0f);
            Width.Set(a.Width, 0f);
            Height.Set(a.Height, 0f);
        }

        // ------------------------------------------------------------------------------------------
        // Pager (hit rectangles are shared by drawing and clicking)
        // ------------------------------------------------------------------------------------------
        Rectangle PrevRect(Rectangle r) => new Rectangle(r.X, r.Bottom - RecipeBookLayout.DetailPagerHeight + 2, 40, RecipeBookLayout.DetailPagerHeight - 4);
        Rectangle NextRect(Rectangle r) => new Rectangle(r.Right - 40, r.Bottom - RecipeBookLayout.DetailPagerHeight + 2, 40, RecipeBookLayout.DetailPagerHeight - 4);

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (ContainsPoint(Main.MouseScreen)) Main.LocalPlayer.mouseInterface = true;
        }

        public override void LeftClick(UIMouseEvent evt)
        {
            base.LeftClick(evt);

            // Clicking an ingredient that has a recipe in the book jumps to that recipe (boxes were recorded while drawing).
            Point click = Main.MouseScreen.ToPoint();
            foreach (var hit in navHits)
            {
                if (!hit.box.Contains(click)) continue;
                SoundEngine.PlaySound(SoundID.MenuTick);
                book.GoTo(hit.key);
                return;
            }

            RecipeOutputGroup g = book.SelectedGroup;
            if (g == null || g.Recipes.Count < 2) return;

            Rectangle r = GetDimensions().ToRectangle();
            Point mouse = Main.MouseScreen.ToPoint();
            int count = g.Recipes.Count;
            if (PrevRect(r).Contains(mouse)) recipeIndex = (recipeIndex + count - 1) % count;
            else if (NextRect(r).Contains(mouse)) recipeIndex = (recipeIndex + 1) % count;
            else return;
            SoundEngine.PlaySound(SoundID.MenuTick);
        }

        // ------------------------------------------------------------------------------------------
        // Drawing
        // ------------------------------------------------------------------------------------------
        protected override void DrawSelf(SpriteBatch sb)
        {
            Rectangle r = GetDimensions().ToRectangle();
            navHits.Clear();

            if (book.SelectedGroup == null)
            {
                string msg = book.IsEmpty ? RecipeBookLayout.Text("Empty") : book.NoResults ? RecipeBookLayout.Text("NoResults") : RecipeBookLayout.Text("NothingSelected");
                DrawWrapped(sb, msg, new Vector2(r.X + 8, r.Center.Y - 30), r.Width - 16, RecipeBookLayout.TextFadedColor, 1f);
                return;
            }

            RecipeOutputGroup group = book.SelectedGroup;
            if (!ReferenceEquals(group, shownGroup))
            {
                shownGroup = group;
                recipeIndex = 0;          // new selection starts on its first recipe
            }
            recipeIndex = Math.Clamp(recipeIndex, 0, Math.Max(0, group.Recipes.Count - 1));

            bool faded = book.SelectedState != RecipeState.Crafted;
            RecipeIcon.Draw(sb, group.Key, new Vector2(r.X + 30, r.Y + 30), 44f, 1f, faded);

            string name = BookDraw.Fit(faded ? RecipeBookLayout.UnknownName : group.Key.DisplayName, r.Width - 70, 1.15f, out _);
            BookDraw.Text(sb, name, new Vector2(r.X + 64, r.Y + 6), RecipeBookLayout.TextColor, 1.15f);
            BookDraw.Text(sb, RecipeBookLayout.MachineName(book.SelectedMachine), new Vector2(r.X + 64, r.Y + 34), RecipeBookLayout.PageBorder, 0.9f);

            BookDraw.Rect(sb, new Rectangle(r.X, r.Y + 66, r.Width, 2), RecipeBookLayout.PageBorder * 0.6f);


            if (group.Recipes.Count == 0) return;
            DrawRecipe(sb, group.Recipes[recipeIndex], faded);
            if (group.Recipes.Count > 1) DrawPager(sb, r, group.Recipes.Count);
        }

        void DrawRecipe(SpriteBatch sb, CustomRecipe recipe, bool outputsHidden)
        {
            Rectangle area = RecipeBookLayout.DetailRecipeArea;
            // DetailRecipeArea is panel-relative; the element's own origin gives the panel offset.
            Rectangle self = GetDimensions().ToRectangle();
            Rectangle detail = RecipeBookLayout.DetailArea;
            area.Offset(self.X - detail.X, self.Y - detail.Y);

            float y = area.Y;

            // Products stay "???" until crafted. Ingredients follow the reveal rule below.
            y = DrawLabel(sb, RecipeBookLayout.Text("Inputs"), area, y);
            // An ingredient the player neither holds right now nor has crafted before stays a silhouette with "???", even
            // when a parchment taught the recipe (keeps recipe trees honest). A Crafted recipe shows everything.
            // Recipes known only from holding an input (FadedIngredient) also hide every liquid.
            y = DrawCells(sb, BuildCells(recipe.Inputs, recipe.LiquidInputs, false, book.SelectedState, book.SelectedKey), area, y, navHits);

            y += 4f;
            DrawArrow(sb, new Vector2(area.Center.X, y));
            y += 20f;

            y = DrawLabel(sb, RecipeBookLayout.Text("Outputs"), area, y);
            y = DrawCells(sb, BuildCells(recipe.Outputs, recipe.LiquidOutputs, outputsHidden), area, y);

            // Duration: only when the recipe states one. Otherwise the machine's default applies and is not shown (not guessed).
            if (recipe.DurationTicks.HasValue)
            {
                y += 4f;
                string secs = (recipe.DurationTicks.Value / 60f).ToString("0.##");
                string text = string.Format(RecipeBookLayout.Text("Time"), secs);
                Vector2 size = FontAssets.MouseText.Value.MeasureString(text) * 0.95f;
                BookDraw.Text(sb, text, new Vector2(area.Center.X - size.X / 2f, y), RecipeBookLayout.TextColor, 0.95f);
            }
        }

        static float DrawLabel(SpriteBatch sb, string text, Rectangle area, float y)
        {
            Vector2 size = FontAssets.MouseText.Value.MeasureString(text) * 0.9f;
            BookDraw.Text(sb, text, new Vector2(area.Center.X - size.X / 2f, y), RecipeBookLayout.PageBorder, 0.9f);
            return y + 22f;
        }

        // ---- ingredient cells ----
        struct Cell
        {
            public RecipeOutputKey Icon;
            public string Count;      // null = no number
            public string Name;       // hover text (liquids / any-water)
            public int ItemType;      // >0 -> real item tooltip
            public int Stack;
            public bool Hidden;       // product not crafted yet: black silhouette, "???" on hover
            public string NavKey;     // catalog key of the recipe that makes this ingredient (null = none in the book: not clickable)
        }

        // inputState != null means "these are the recipe's inputs": the reveal rule and click-to-jump apply. selfKey = the recipe on screen.
        static List<Cell> BuildCells(List<RecipeIngredient> items, List<LiquidIngredient> liquids, bool hidden, RecipeState? inputState = null, string selfKey = null)
        {
            var cells = new List<Cell>();
            bool isInput = inputState.HasValue;
            bool reveal = isInput && inputState.Value != RecipeState.Crafted;       // Crafted recipes show all ingredients
            bool fadeLiquids = inputState == RecipeState.FadedIngredient;
            foreach (RecipeIngredient i in items)
            {
                bool itemHidden = hidden || (reveal && !RecipeVisibility.IsIngredientRevealed(i.Type));
                cells.Add(new Cell
                {
                    Icon = RecipeOutputKey.ForItem(i.Type),
                    Count = i.Stack > 1 ? i.Stack.ToString() : null,
                    ItemType = i.Type,
                    Stack = i.Stack,
                    Hidden = itemHidden,
                    NavKey = isInput && !itemHidden ? RecipeVisibility.FindBookRecipeFor(RecipeOutputKey.ForItem(i.Type), selfKey) : null,
                });
            }
            foreach (LiquidIngredient l in liquids)
            {
                // AnyWater (-2) is not in the registry: show plain water's icon and a generic name.
                int iconId = l.IsAnyWater ? LiquidTypeRegistry.Water : l.LiquidType;
                string name = l.IsAnyWater ? RecipeBookLayout.Text("AnyWater") : LiquidTypeRegistry.Get(l.LiquidType).Name;
                string amount = l.Amount.ToString("0.##");
                bool liquidHidden = hidden || (isInput && (fadeLiquids || (reveal && !l.IsAnyWater && !RecipeVisibility.IsLiquidRevealed(l.LiquidType))));
                cells.Add(new Cell
                {
                    Icon = RecipeOutputKey.ForLiquid(iconId),
                    Count = amount,
                    Name = name + " - " + string.Format(RecipeBookLayout.Text("Units"), amount),
                    Hidden = liquidHidden,
                    NavKey = isInput && !liquidHidden && !l.IsAnyWater ? RecipeVisibility.FindBookRecipeFor(RecipeOutputKey.ForLiquid(l.LiquidType), selfKey) : null,
                });
            }
            return cells;
        }

        /// <summary>Draws cells in rows wrapped at the area's width, each row centered on the page. Returns the y below the last row.</summary>
        static float DrawCells(SpriteBatch sb, List<Cell> cells, Rectangle area, float y, List<(Rectangle box, string key)> navHits = null)
        {
            int size = RecipeBookLayout.DetailCell;
            int gap = RecipeBookLayout.DetailCellGap;
            int perRow = Math.Max(1, (area.Width + gap) / (size + gap));
            Point mouse = Main.MouseScreen.ToPoint();

            for (int rowStart = 0; rowStart < cells.Count; rowStart += perRow)
            {
                int n = Math.Min(perRow, cells.Count - rowStart);
                int rowWidth = n * size + (n - 1) * gap;
                int x0 = area.X + (area.Width - rowWidth) / 2;
                int rowY = (int)y + (rowStart / perRow) * (size + gap);

                for (int c = 0; c < n; c++)
                {
                    Cell cell = cells[rowStart + c];
                    var box = new Rectangle(x0 + c * (size + gap), rowY, size, size);

                    // Only ingredients that have a recipe in the book light up on hover; the rest stay inert.
                    bool navHover = cell.NavKey != null && box.Contains(mouse);
                    BookDraw.Frame(sb, box,
                        navHover ? new Color(214, 178, 48) * 0.55f : RecipeBookLayout.PageColor * 0.6f,
                        navHover ? new Color(150, 110, 30) : RecipeBookLayout.PageBorder * 0.7f, 2);
                    RecipeIcon.Draw(sb, cell.Icon, box.Center.ToVector2(), size - 12f, 1f, cell.Hidden);

                    if (cell.Count != null)
                    {
                        float scale = 0.75f;
                        Vector2 s = FontAssets.ItemStack.Value.MeasureString(cell.Count) * scale;
                        Utils.DrawBorderString(sb, cell.Count, new Vector2(box.Right - 4 - s.X, box.Bottom - 4 - s.Y), Color.White, scale);
                    }

                    if (cell.NavKey != null) navHits?.Add((box, cell.NavKey));

                    if (!box.Contains(mouse)) continue;

                    if (cell.Hidden) Main.hoverItemName = RecipeBookLayout.UnknownName;
                    else if (cell.ItemType > 0)
                    {
                        Item tip = ContentSamples.ItemsByType[cell.ItemType].Clone();
                        tip.stack = Math.Max(1, cell.Stack);
                        Main.HoverItem = tip;
                        Main.hoverItemName = tip.Name;
                    }
                    else Main.hoverItemName = cell.Name;
                }
            }

            int rows = cells.Count == 0 ? 0 : (cells.Count - 1) / perRow + 1;
            return y + rows * (size + gap);
        }

        /// <summary>Small downward arrow made of rectangles (no art needed).</summary>
        static void DrawArrow(SpriteBatch sb, Vector2 topCenter)
        {
            Color c = RecipeBookLayout.PageBorder;
            int x = (int)topCenter.X;
            int y = (int)topCenter.Y;
            BookDraw.Rect(sb, new Rectangle(x - 1, y, 3, 8), c);                          // stem
            for (int i = 0; i < 5; i++)                                                  // head
                BookDraw.Rect(sb, new Rectangle(x - (4 - i), y + 8 + i, (4 - i) * 2 + 1, 1), c);
        }

        void DrawPager(SpriteBatch sb, Rectangle r, int count)
        {
            Point mouse = Main.MouseScreen.ToPoint();
            DrawPagerButton(sb, PrevRect(r), "<", mouse);
            DrawPagerButton(sb, NextRect(r), ">", mouse);

            string label = (recipeIndex + 1) + " / " + count;
            Vector2 size = FontAssets.MouseText.Value.MeasureString(label) * 0.95f;
            BookDraw.Text(sb, label, new Vector2(r.Center.X - size.X / 2f, r.Bottom - RecipeBookLayout.DetailPagerHeight + 4), RecipeBookLayout.TextColor, 0.95f);
        }

        static void DrawPagerButton(SpriteBatch sb, Rectangle box, string glyph, Point mouse)
        {
            bool hover = box.Contains(mouse);
            BookDraw.Frame(sb, box, hover ? new Color(214, 178, 48) * 0.55f : RecipeBookLayout.PageColor * 0.6f, RecipeBookLayout.PageBorder, 2);
            Vector2 size = FontAssets.MouseText.Value.MeasureString(glyph);
            BookDraw.Text(sb, glyph, new Vector2(box.Center.X - size.X / 2f, box.Center.Y - size.Y / 2f + 3f), RecipeBookLayout.TextColor, 1f);
        }

        static void DrawWrapped(SpriteBatch sb, string text, Vector2 pos, float width, Color color, float scale)
        {
            var font = FontAssets.MouseText.Value;
            string[] lines = Utils.WordwrapString(text, font, (int)(width / scale), 8, out int count);
            for (int i = 0; i < count; i++)
                if (lines[i] != null) BookDraw.Text(sb, lines[i], pos + new Vector2(0, i * 24f * scale), color, scale);
        }
    }
}