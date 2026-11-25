using Factorraria.Common.Knowledge;
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

        // Tweakables (instance fields on purpose, so hot reload can change them).
        int arrowLength = 54;          // arrow height when the machine has fuels: room for one 42px fuel cell beside its middle
        int shortArrowLength = 13;     // the original arrow, used when the machine has no fuels
        int fuelCycleTicks = 60;       // the shown fuel changes this often (60 = once a second)
        int fuelBoxOffsetX = 12;       // gap between the arrow's centre line and the fuel cell
        int fuelOrPadding = 6;         // pixels between a fuel cell and the "or" on each side of it
        int fuelRightMargin = 6;       // the last fuel cell may come this close to the right edge of the page before the recipe is pushed left
        int fuelMaxShift = 140;        // the most the recipe (ingredients, arrow, products) is pushed left to make room for the fuels
        int fuelBoxOffsetY = -8;       // vertical nudge of the fuel cells (0 = centered between the label and the arrow end)
        int fuelOrOffsetY = 0;         // vertical nudge of the "or" next to a fuel cell (+ = down)
        float fuelOrScale = 0.85f;     // text size of the "or" between fuel cells
        int fuelLabelHeight = 18;      // room above the top fuel cell for the single "Fuel" label

        // Clickable ingredient boxes of the recipe drawn this frame (box, catalog key to jump to). Refilled every draw.
        readonly List<(Rectangle box, string key, string text)> navHits = new List<(Rectangle box, string key, string text)>();

        // Revealed ingredient boxes that have NO recipe in the book (box, name to type in the search bar). Refilled every draw.
        readonly List<(Rectangle box, string text)> searchHits = new List<(Rectangle box, string text)>();

        // The recipes of the selected product across the machines of its tab (RecipeCatalog.MergedRecipes), cached per selection.
        List<RecipeCatalog.MergedRecipe> merged = new List<RecipeCatalog.MergedRecipe>();
        RecipeOutputGroup mergedGroup;
        string mergedMachine;
        int mergedVersion = -1;

        List<RecipeCatalog.MergedRecipe> Merged()
        {
            if (!ReferenceEquals(mergedGroup, book.SelectedGroup) || mergedMachine != book.SelectedMachine || mergedVersion != RecipeKnowledgeSystem.Version)
            {
                mergedGroup = book.SelectedGroup;
                mergedMachine = book.SelectedMachine;
                mergedVersion = RecipeKnowledgeSystem.Version;
                merged = RecipeCatalog.MergedRecipes(mergedMachine, mergedGroup);
            }
            return merged;
        }

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
                book.GoToAndSearch(hit.key, hit.text);
                return;
            }

            // No recipe to jump to: type the item's name in the search bar anyway (like the item-search keybind), so the click always does something.
            foreach (var hit in searchHits)
            {
                if (!hit.box.Contains(click)) continue;
                SoundEngine.PlaySound(SoundID.MenuTick);
                book.SetSearch(hit.text);
                return;
            }

            if (book.SelectedGroup == null || Merged().Count < 2) return;

            Rectangle r = GetDimensions().ToRectangle();
            Point mouse = Main.MouseScreen.ToPoint();
            int count = Merged().Count;
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
            searchHits.Clear();

            // Art entries (RecipeArtRegistry) replace the whole page with their PNG.
            if (book.SelectedArt != null)
            {
                DrawArt(sb, r, book.SelectedArt);
                return;
            }

            if (book.SelectedGroup == null)
            {
                string msg = book.IsEmpty ? RecipeBookLayout.Text("Empty") : book.NoResults ? RecipeBookLayout.Text("NoResults") : RecipeBookLayout.Text("NothingSelected");
                DrawCenteredMessage(sb, msg, r, RecipeBookLayout.TextFadedColor, 1f);
                return;
            }

            RecipeOutputGroup group = book.SelectedGroup;
            if (!ReferenceEquals(group, shownGroup))
            {
                shownGroup = group;
                recipeIndex = 0;          // new selection starts on its first recipe
            }
            List<RecipeCatalog.MergedRecipe> recipes = Merged();
            recipeIndex = Math.Clamp(recipeIndex, 0, Math.Max(0, recipes.Count - 1));

            bool faded = book.SelectedState != RecipeState.Crafted;
            RecipeIcon.Draw(sb, group.Key, new Vector2(r.X + 30, r.Y + 30), 44f, 1f, faded);

            string name = BookDraw.Fit(faded ? RecipeBookLayout.UnknownName : group.Key.DisplayName, r.Width - 70, 1.15f, out _);
            BookDraw.Text(sb, name, new Vector2(r.X + 64, r.Y + 6), RecipeBookLayout.TextColor, 1.15f);
            if (recipes.Count > 0) DrawStationBlock(sb, r, recipes[recipeIndex]);
            else headerExtra = 0;

            BookDraw.Rect(sb, new Rectangle(r.X, r.Y + 66 + headerExtra, r.Width, 2), RecipeBookLayout.PageBorder * 0.6f);


            if (recipes.Count == 0) return;
            DrawRecipe(sb, recipes[recipeIndex].Recipe, faded, recipes[recipeIndex].Machines);
            if (recipes.Count > 1) DrawPager(sb, r, recipes.Count);
        }

        // ---- Station block (R1.7), under the product name ----
        // One row per alternative station ("[sprite] Autohammer", then "[sprite] Anvils"): an alternative always starts a new row. A recipe that needs
        // several stations at once joins them with "&" in ONE row that wraps ("[sprite] Tables & [sprite] Chairs"). After the stations come the
        // vanilla conditions of the recipe ("In the Graveyard", ...), one per row. Built for the recipe shown and cached.
        class StationPart
        {
            public string Station;   // not null: sprite + this station's name; null: plain text ("&" or a condition line)
            public string Text;
            public float X;          // pixels from the left edge of the block
        }

        readonly List<List<StationPart>> stationRows = new List<List<StationPart>>();
        RecipeCatalog.MergedRecipe stationFor;
        string stationMachine;
        int stationSeenVersion = -1;
        float stationWidth = -1f;
        int headerExtra;                              // px the extra station rows push the recipe body down

        /// <summary>Pixels between two rows of the station block. Not const, so hot reload can tweak it.</summary>
        public static int StationLineStep = 20;
        public static int StationIconWidth = 24;      // room taken by a station sprite and the gap after it
        public static float StationIconSize = 20f;
        public static int StationAmpGap = 4;          // pixels before the "&"; the same again after it

        List<List<StationPart>> StationBlock(RecipeCatalog.MergedRecipe recipe, float maxWidth)
        {
            if (ReferenceEquals(stationFor, recipe) && stationMachine == book.SelectedMachine && stationSeenVersion == RecipeKnowledgeSystem.SeenVersion && stationWidth == maxWidth)
                return stationRows;

            stationFor = recipe;
            stationMachine = book.SelectedMachine;
            stationSeenVersion = RecipeKnowledgeSystem.SeenVersion;
            stationWidth = maxWidth;
            stationRows.Clear();

            float scale = 0.9f;
            var font = Terraria.GameContent.FontAssets.MouseText.Value;

            // The alternatives: every station or machine of the tab that makes THIS recipe, only the discovered ones (all if none is yet).
            var units = new List<string>();
            foreach (string machine in recipe.Machines)
            {
                string unit = RecipeCatalog.UnitOf(machine);
                if (unit != null && !units.Contains(unit)) units.Add(unit);
            }
            if (units.Count == 0) units.Add(RecipeCatalog.TabOf(book.SelectedMachine));
            List<string> known = units.FindAll(RecipeTabDiscovery.IsOwnDiscovered);
            if (known.Count == 0) known = units;

            // Stations the vanilla recipe needs at the same time that the tab does not already stand for (the Chairs of the Table's Watch).
            var extras = new List<string>();
            Recipe source = recipe.Recipe.Source;
            if (source != null)
            {
                HashSet<int> own = RecipeTabDiscovery.TilesOfTab(RecipeCatalog.TabOf(book.SelectedMachine));
                foreach (int tile in source.requiredTile)
                    if (tile >= 0 && tile < TileID.Count && !own.Contains(tile)) extras.Add(VanillaStationBooks.NameFor(tile));
            }

            float ampWidth = font.MeasureString("&").X * scale;
            foreach (string unit in known)
            {
                var row = new List<StationPart>();
                stationRows.Add(row);
                float x = 0f;
                var names = new List<string> { unit };
                names.AddRange(extras);

                for (int i = 0; i < names.Count; i++)
                {
                    bool last = i == names.Count - 1;
                    string text = RecipeBookLayout.MachineName(names[i]);
                    float width = (RecipeBookStationIcon.Has(names[i]) ? StationIconWidth : 0f) + font.MeasureString(text).X * scale;
                    float tail = last ? 0f : StationAmpGap + ampWidth;
                    if (x > 0f && x + width + tail > maxWidth)
                    {
                        row = new List<StationPart>();
                        stationRows.Add(row);
                        x = 0f;
                    }

                    row.Add(new StationPart { Station = names[i], Text = text, X = x });
                    x += width;
                    if (last) continue;
                    x += StationAmpGap;
                    row.Add(new StationPart { Text = "&", X = x });
                    x += ampWidth + StationAmpGap;
                }
            }

            // The conditions vanilla puts on the recipe, after the stations.
            if (source != null)
            {
                var seen = new HashSet<string>();
                foreach (Condition condition in source.Conditions)
                {
                    string text = condition?.Description?.Value;
                    if (string.IsNullOrEmpty(text) || !seen.Add(text)) continue;
                    foreach (string line in Wrap(text, maxWidth, scale))
                        stationRows.Add(new List<StationPart> { new StationPart { Text = line, X = 0f } });
                }
            }

            return stationRows;
        }

        void DrawStationBlock(SpriteBatch sb, Rectangle r, RecipeCatalog.MergedRecipe recipe)
        {
            List<List<StationPart>> rows = StationBlock(recipe, r.Width - 70);
            headerExtra = Math.Max(0, rows.Count - 1) * StationLineStep;
            for (int row = 0; row < rows.Count; row++)
            {
                float y = r.Y + 34 + row * StationLineStep;
                foreach (StationPart part in rows[row])
                {
                    float x = r.X + 64 + part.X;
                    if (part.Station != null && RecipeBookStationIcon.Draw(sb, part.Station, new Vector2(x + StationIconSize / 2f, y + 9f), StationIconSize))
                        x += StationIconWidth;
                    BookDraw.Text(sb, part.Text, new Vector2(x, y), RecipeBookLayout.PageBorder, 0.9f);
                }
            }
        }

        // Greedy word wrap with the book font. A single word wider than the line stays on its own line.
        static List<string> Wrap(string text, float maxWidth, float scale)
        {
            var font = FontAssets.MouseText.Value;
            var lines = new List<string>();
            string current = "";
            foreach (string word in text.Split(' '))
            {
                string test = current.Length == 0 ? word : current + " " + word;
                if (current.Length > 0 && font.MeasureString(test).X * scale > maxWidth)
                {
                    lines.Add(current);
                    current = word;
                }
                else current = test;
            }
            if (current.Length > 0) lines.Add(current);
            return lines;
        }

        void DrawRecipe(SpriteBatch sb, CustomRecipe recipe, bool outputsHidden, List<string> machines)
        {
            Rectangle area = RecipeBookLayout.DetailRecipeArea;
            // DetailRecipeArea is panel-relative; the element's own origin gives the panel offset.
            Rectangle self = GetDimensions().ToRectangle();
            Rectangle detail = RecipeBookLayout.DetailArea;
            area.Offset(self.X - detail.X, self.Y - detail.Y);
            area.Y += headerExtra;        // the wrapped station line above pushes everything down
            area.Height -= headerExtra;

            // Fuel options (R1.9): ONE row of cells to the right of the arrow, one cell per machine that makes THIS recipe and burns something, in
            // machine order, with an "or" between them (the Furnace's items, then the Hellforge's lava). Stations and machines without fuels add
            // nothing. A machine's fuels show only once that machine is discovered; if none is discovered yet (a parchment recipe of a machine
            // nobody found), all of them show.
            var fuelRows = new List<(string machine, List<RecipeCatalog.FuelEntry> fuels)>();
            var knownRows = new List<(string machine, List<RecipeCatalog.FuelEntry> fuels)>();
            foreach (string machine in machines)
            {
                List<RecipeCatalog.FuelEntry> machineFuels = RecipeCatalog.FuelsOf(machine);
                if (machineFuels == null || machineFuels.Count == 0) continue;
                fuelRows.Add((machine, machineFuels));
                if (RecipeTabDiscovery.IsOwnDiscovered(machine)) knownRows.Add((machine, machineFuels));
            }
            if (knownRows.Count > 0) fuelRows = knownRows;
            bool hasFuel = fuelRows.Count > 0;

            // Not enough room right of the arrow: the whole recipe below the station rows (ingredients, arrow, products, time) slides left.
            if (hasFuel) area.X -= FuelShift(area, fuelRows.Count);

            float y = area.Y;

            // Products stay "???" until crafted. Ingredients follow the reveal rule below.
            y = DrawLabel(sb, RecipeBookLayout.Text("Inputs"), area, y);
            // An ingredient the player neither holds right now nor has crafted before stays a silhouette with "???", even
            // when a parchment taught the recipe (keeps recipe trees honest). A Crafted recipe shows everything.
            // Recipes known only from holding an input (FadedIngredient) hide every liquid that is not revealed yet (biome visited or crafted).
            y = DrawCells(sb, BuildCells(recipe.Inputs, recipe.LiquidInputs, false, book.SelectedState, book.SelectedKey), area, y, navHits, searchHits);

            y += 4f;
            int length = hasFuel ? Math.Max(arrowLength, fuelLabelHeight + RecipeBookLayout.DetailCell + 12) + 8 : shortArrowLength; DrawArrow(sb, new Vector2(area.Center.X, y), length);
            for (int row = 0; row < fuelRows.Count; row++)
                DrawFuel(sb, area, y, length, row, fuelRows[row].machine, fuelRows[row].fuels);
            y += length + 7f;     // same 7px gap the short arrow always had

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
            public string SearchName; // plain name typed into the search bar when the cell is clicked and NavKey is null (never used while Hidden)
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
                    SearchName = isInput ? ContentSamples.ItemsByType[i.Type].Name : null,
                });
            }
            foreach (LiquidIngredient l in liquids)
            {
                // AnyWater (-2) and BiomeWater (-3) are not in the registry: show plain water's icon and a generic name.
                int iconId = l.IsAnyWater || l.IsBiomeWater ? LiquidTypeRegistry.Water : l.LiquidType;
                string name = l.IsAnyWater ? RecipeBookLayout.Text("AnyWater")
                    : l.IsBiomeWater ? RecipeBookLayout.Text("BiomeWater")
                    : LiquidTypeRegistry.Get(l.LiquidType).Name;
                string amount = l.Amount.ToString("0.##");
                // A specific liquid follows the liquid rule (biome visited or crafted) in EVERY non-crafted state, faded ones included.
                // Only "any water" has no rule of its own, so it stays hidden in a faded recipe.
                bool liquidShown = l.IsAnyWater || l.IsBiomeWater ? !fadeLiquids : RecipeVisibility.IsLiquidRevealed(l.LiquidType);
                bool liquidHidden = hidden || (isInput && (fadeLiquids || reveal) && !liquidShown);
                cells.Add(new Cell
                {
                    Icon = RecipeOutputKey.ForLiquid(iconId),
                    Count = amount,
                    Name = name + " - " + string.Format(RecipeBookLayout.Text("Units"), amount),
                    Hidden = liquidHidden,
                    NavKey = isInput && !liquidHidden && !l.IsAnyWater ? RecipeVisibility.FindBookRecipeFor(RecipeOutputKey.ForLiquid(l.LiquidType), selfKey) : null,
                    SearchName = isInput ? name : null,
                });
            }
            return cells;
        }

        /// <summary>Draws cells in rows wrapped at the area's width, each row centered on the page. Returns the y below the last row.</summary>
        static float DrawCells(SpriteBatch sb, List<Cell> cells, Rectangle area, float y, List<(Rectangle box, string key, string text)> navHits = null, List<(Rectangle box, string text)> searchHits = null)
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

                    if (cell.NavKey != null) navHits?.Add((box, cell.NavKey, cell.SearchName));
                    else if (cell.SearchName != null && !cell.Hidden) searchHits?.Add((box, cell.SearchName));   // no highlight for these: only recipe jumps light up

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

        /// <summary>Small downward arrow made of rectangles (no art needed). length = total height: the stem plus a 5px head.</summary>
        void DrawArrow(SpriteBatch sb, Vector2 topCenter, int length)
        {
            Color c = RecipeBookLayout.PageBorder;
            int x = (int)topCenter.X;
            int y = (int)topCenter.Y;
            int stem = Math.Max(1, length - 5);
            BookDraw.Rect(sb, new Rectangle(x - 1, y, 3, stem), c);                       // stem
            for (int i = 0; i < 5; i++)                                                  // head
                BookDraw.Rect(sb, new Rectangle(x - (4 - i), y + stem + i, (4 - i) * 2 + 1, 1), c);
        }

        float FuelOrWidth() => FontAssets.MouseText.Value.MeasureString(RecipeBookLayout.Text("Or")).X * fuelOrScale;

        // Distance from the left edge of one fuel cell to the left edge of the next: the cell, the "or" and its padding on both sides.
        float FuelStep() => RecipeBookLayout.DetailCell + FuelOrWidth() + 2 * fuelOrPadding;

        /// <summary>How many pixels the recipe has to slide left so the whole row of fuel cells fits to the right of the arrow (0 when it already fits).</summary>
        int FuelShift(Rectangle area, int fuelCount)
        {
            float needed = fuelBoxOffsetX + RecipeBookLayout.DetailCell + (fuelCount - 1) * FuelStep() + fuelRightMargin;
            float available = area.Right - area.Center.X;
            return (int)Math.Clamp(Math.Ceiling(needed - available), 0, fuelMaxShift);
        }

        /// <summary>
        /// One machine's fuel (from RecipeCatalog.FuelsOf, i.e. the machine's registry), cycling through all of its fuels. The cells sit in one row
        /// to the right of the arrow: the first machine's cell next to the arrow, then "or", then the next machine's cell. Same reveal rule,
        /// hover tooltip and click-to-jump as an ingredient cell.
        /// </summary>
        void DrawFuel(SpriteBatch sb, Rectangle area, float arrowTop, int length, int index, string machine, List<RecipeCatalog.FuelEntry> fuels)
        {
            int size = RecipeBookLayout.DetailCell;
            int cycle = (int)((Main.GameUpdateCount / (uint)Math.Max(1, fuelCycleTicks)) % (uint)fuels.Count);
            RecipeCatalog.FuelEntry fuel = fuels[cycle];

            // Not tied to the selected recipe: once any recipe of this machine was crafted, every fuel of the machine shows.
            // Before that, a fuel shows once it was held (any member, for a group like Any Wood).
            bool hidden = !RecipeVisibility.IsFuelRevealed(machine, fuel);
            bool single = fuel.LabelKey == null;

            var cell = new Cell
            {
                Icon = fuel.Icon,
                ItemType = single && !fuel.Icon.IsLiquid ? fuel.Icon.Id : 0,    // a group has no single item tooltip
                Stack = 1,
                Name = !single ? RecipeBookLayout.Text(fuel.LabelKey) : fuel.Icon.IsLiquid ? (LiquidTypeRegistry.Get(fuel.Icon.Id)?.Name ?? "") : null,
                Hidden = hidden,
                NavKey = hidden || !single ? null : RecipeVisibility.FindBookRecipeFor(fuel.Icon, book.SelectedKey),
                SearchName = hidden || !single ? null : fuel.Icon.IsLiquid ? LiquidTypeRegistry.Get(fuel.Icon.Id)?.Name : ContentSamples.ItemsByType[fuel.Icon.Id].Name,
            };

            // DrawCells with a one-cell area draws, highlights, shows the tooltip and records the click box.
            int x = area.Center.X + fuelBoxOffsetX + (int)Math.Round(index * FuelStep());
            var box = new Rectangle(x, (int)(arrowTop + fuelLabelHeight + (length - fuelLabelHeight - size) / 2f + fuelBoxOffsetY), size, size);
            DrawCells(sb, new List<Cell> { cell }, box, box.Y, navHits, searchHits);

            if (index == 0)
            {
                // The only "Fuel" label: centered above the first fuel cell.
                string label = RecipeBookLayout.Text("Fuel");
                Vector2 labelSize = FontAssets.MouseText.Value.MeasureString(label) * 0.9f;
                BookDraw.Text(sb, label, new Vector2(box.Center.X - labelSize.X / 2f, box.Y - fuelLabelHeight - 2f), RecipeBookLayout.PageBorder, 0.9f);
            }
            else
            {
                // "or" between the previous cell and this one, vertically centered on the cells.
                string orText = RecipeBookLayout.Text("Or");
                float orX = box.X - fuelOrPadding - FuelOrWidth();
                BookDraw.Text(sb, orText, new Vector2(orX, box.Center.Y - RecipeBookTabTipOverlay.InkCenterY * 1.1f + fuelOrOffsetY), RecipeBookLayout.PageBorder, fuelOrScale);
            }
        }

        /// <summary>Art entry page: the same header text and separator line as a recipe page, then the PNG centered below it, shrunk to fit, never enlarged.</summary>
        void DrawArt(SpriteBatch sb, Rectangle r, RecipeArtRegistry.Entry art)
        {
            string title = BookDraw.Fit(art.Name, r.Width - 12, 1.15f, out _);
            BookDraw.Text(sb, title, new Vector2(r.X + 6, r.Y + 6), RecipeBookLayout.TextColor, 1.15f);
            BookDraw.Rect(sb, new Rectangle(r.X, r.Y + 36, r.Width, 2), RecipeBookLayout.PageBorder * 0.6f);
            var body = new Rectangle(r.X, r.Y + 68, r.Width, r.Height - 68);

            Texture2D tex = art.Texture;
            if (tex == null)
            {
                DrawWrapped(sb, "Missing art: " + art.TexturePath, new Vector2(body.X + 8, body.Center.Y - 30), body.Width - 16, RecipeBookLayout.TextFadedColor, 1f);
                return;
            }

            float fit = Math.Min(1f, Math.Min(body.Width / (float)tex.Width, body.Height / (float)tex.Height));
            int w = Math.Max(1, (int)(tex.Width * fit));
            int h = Math.Max(1, (int)(tex.Height * fit));
            // Own batch so transparent pixels blend over the page whatever state the batch was left in. Point sampling at 1:1, linear when shrunk.
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, fit < 1f ? SamplerState.LinearClamp : SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
            sb.Draw(tex, new Rectangle(body.Center.X - w / 2, body.Center.Y - h / 2, w, h), Color.White);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
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

        /// <summary>
        /// Wraps text into lines at the given pixel width and honours \n. Utils.WordwrapString's lineAmount is the INDEX of the
        /// last line, not the number of lines, so it is bumped by one here (that is why the last line used to be cut off).
        /// </summary>
        static List<string> WrapLines(string text, float width, float scale)
        {
            var font = FontAssets.MouseText.Value;
            var result = new List<string>();
            foreach (string paragraph in text.Split('\n'))
            {
                if (paragraph.Length == 0)
                {
                    result.Add("");
                    continue;
                }

                string[] lines = Utils.WordwrapString(paragraph, font, (int)(width / scale), 8, out int count);
                count++;
                for (int i = 0; i < count && i < lines.Length; i++)
                    if (lines[i] != null) result.Add(lines[i]);
            }
            return result;
        }

        static void DrawWrapped(SpriteBatch sb, string text, Vector2 pos, float width, Color color, float scale)
        {
            List<string> lines = WrapLines(text, width, scale);
            for (int i = 0; i < lines.Count; i++)
                BookDraw.Text(sb, lines[i], pos + new Vector2(0, i * 24f * scale), color, scale);
        }

        /// <summary>A message block centered in the area both ways, with every line centered on its own. \n in the text starts a new line.</summary>
        static void DrawCenteredMessage(SpriteBatch sb, string text, Rectangle area, Color color, float scale)
        {
            List<string> lines = WrapLines(text, area.Width - 16, scale);
            float lineHeight = 24f * scale;
            float top = area.Center.Y - lines.Count * lineHeight / 2f;
            for (int i = 0; i < lines.Count; i++)
            {
                Vector2 size = FontAssets.MouseText.Value.MeasureString(lines[i]) * scale;
                BookDraw.Text(sb, lines[i], new Vector2(area.Center.X - size.X / 2f, top + i * lineHeight), color, scale);
            }
        }
    }
}