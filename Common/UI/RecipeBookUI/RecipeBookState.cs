using Factorraria.Common.Knowledge;
using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Factorraria.Common.Systems;
using Factorraria.Common.UI.CustomUIElements;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.UI;

namespace Factorraria.Common.UI.RecipeBookUI
{
    /// <summary>
    /// The recipe book (UI only, owned by RecipeBookSystem). Lists parchment-unlocked and crafted recipes
    /// grouped by machine. Not a MachineUIStateBase: it is not bound to a machine entity.
    /// Rebuilds when RecipeKnowledgeSystem.Version changes and whenever it is opened.
    /// </summary>
    public class RecipeBookState : UIState
    {
        RecipeBookPanel panel;
        UIElement listArea;
        BookScrollbar scrollbar;
        RecipeSearchBar searchBar;
        string searchFilter = "";
        bool filterDirty;
        bool layoutDirty;                                    // a header was toggled: rebuild on the next Update (never mid-click)
        // Folded tabs and the Rows/Grid choice live on RecipeBookPlayer (saved with the character).
        readonly Dictionary<string, string> aliases = new(); // a station recipe hidden because its machine twin lists the same product -> the machine's key (filled by Rebuild)

        // Window position: pixels away from the centered spot. Static so the book reopens where the player left it
        // (for the game session). Zero = centered.
        static Vector2 dragOffset;
        int lastScreenW = -1, lastScreenH = -1;

        readonly List<UIElement> rows = new();
        readonly List<float> rowBase = new();
        readonly List<int> headerIndex = new();              // indices into rows/rowBase of the machine headers (sticky while scrolling)
        readonly List<UIElement> headerRows = new();         // the same headers; appended to listArea after every entry so they draw and take clicks on top
        readonly Dictionary<string, float> entryTop = new();
        float totalHeight;
        float lastView = -1f;
        int builtVersion = int.MinValue;
        int builtHeldVersion = int.MinValue;
        int builtFavVersion = int.MinValue;
        bool builtGrid;                                      // R7: the mode the list was built in (Rows = false)
        float entryHeight = RecipeBookLayout.EntryRowHeight; // R7: height of one entry in the built mode (a row, or a grid square)

        // Selection is a catalog key so it survives rebuilds.
        public string SelectedKey { get; private set; }
        public string SelectedMachine { get; private set; }
        public RecipeOutputGroup SelectedGroup { get; private set; }
        /// <summary>The selected entry when it is an art entry (then SelectedGroup is null). The detail page draws its PNG.</summary>
        public RecipeArtRegistry.Entry SelectedArt { get; private set; }
        public RecipeState SelectedState { get; private set; }
        public bool IsEmpty { get; private set; } = true;
        /// <summary>The book has entries but the search filter hides all of them.</summary>
        public bool NoResults { get; private set; }

        /// <summary>True while the mouse is over the book panel (read by RecipeBookSystem for outside-click closing).</summary>
        public bool MouseInsidePanel => panel != null && panel.MouseInside;

        public override void OnInitialize()
        {
            panel = new RecipeBookPanel(this);
            panel.HAlign = 0.5f;
            panel.VAlign = 0.5f;
            panel.Width.Set(RecipeBookLayout.PanelWidth, 0f);
            panel.Height.Set(RecipeBookLayout.PanelHeight, 0f);
            Append(panel);
            panel.Append(new RecipeBookDragHandle(this));
            panel.Append(new RecipeBookCloseButton());   // after the drag handle: it must sit on top of it
            ApplyPosition();

            Rectangle l = RecipeBookLayout.ListArea;
            listArea = new UIElement();
            listArea.Left.Set(l.X, 0f);
            listArea.Top.Set(l.Y, 0f);
            listArea.Width.Set(l.Width, 0f);
            listArea.Height.Set(l.Height, 0f);
            listArea.OverflowHidden = true;
            panel.Append(listArea);

            Rectangle s = RecipeBookLayout.ScrollbarArea;
            scrollbar = new BookScrollbar();
            scrollbar.Left.Set(s.X, 0f);
            scrollbar.Top.Set(s.Y, 0f);
            scrollbar.Height.Set(s.Height, 0f);
            panel.Append(scrollbar);

            BuildSearchBar();
            panel.Append(new RecipeBookModeButton(this));   // R7: Rows / Grid, right of the search bar

            panel.Append(new RecipeBookDetail(this));
            panel.Append(new RecipeBookTabTipOverlay());   // last: the hover tip of a tab header draws above everything
        }

        public override void OnActivate()
        {
            // Opening always starts from fresh data. Selection (if any) is kept.
            RecipeVisibility.InvalidateHeld();
            Rebuild();
        }

        public override void Update(GameTime gameTime)
        {
            // A saved offset must stay valid after a resolution / UI scale change (and once layout dimensions exist).
            if (Main.screenWidth != lastScreenW || Main.screenHeight != lastScreenH)
            {
                lastScreenW = Main.screenWidth;
                lastScreenH = Main.screenHeight;
                ApplyPosition();
            }

            if (filterDirty && scrollbar != null) scrollbar.ViewPosition = 0f;   // new search starts at the top
            RecipeVisibility.Poll();    // HeldVersion only moves if someone scans; the book is visible, so keep it current
            if (RecipeKnowledgeSystem.Version != builtVersion || RecipeVisibility.HeldVersion != builtHeldVersion || filterDirty || layoutDirty
                || Main.LocalPlayer.GetModPlayer<RecipeBookPlayer>().FavoritesVersion != builtFavVersion
                || Main.LocalPlayer.GetModPlayer<RecipeBookPlayer>().GridMode != builtGrid) Rebuild();
            ApplyScroll(false);
            base.Update(gameTime);
        }

        // ------------------------------------------------------------------------------------------
        // List building
        // ------------------------------------------------------------------------------------------
        void Rebuild()
        {
            builtVersion = RecipeKnowledgeSystem.Version;
            RecipeBookPlayer favorites = Main.LocalPlayer.GetModPlayer<RecipeBookPlayer>();
            builtFavVersion = favorites.FavoritesVersion;
            filterDirty = false;
            layoutDirty = false;
            if (listArea == null) return;

            listArea.RemoveAllChildren();
            rows.Clear();
            rowBase.Clear();
            headerIndex.Clear();
            headerRows.Clear();
            entryTop.Clear();
            aliases.Clear();

            // R7: Rows = one row per entry. Grid = squares that flow left to right and then down, restarting under every tab header.
            bool grid = favorites.GridMode;
            bool modeChanged = grid != builtGrid;
            builtGrid = grid;
            entryHeight = grid ? RecipeBookLayout.GridCellSize : RecipeBookLayout.EntryRowHeight;
            int gridCols = RecipeBookLayout.GridColumns;
            float gridX0 = RecipeBookLayout.GridLeft(gridCols);
            float gridStepX = RecipeBookLayout.GridCellSize + RecipeBookLayout.GridGapFor(gridCols);
            float gridStep = (float)Math.Round(gridStepX);   // vertical step = horizontal step, so the gaps match
            int gridCol = 0;

            string lastMachine = null;
            float y = 0f;
            var tabKeys = new Dictionary<string, List<string>>();   // keys listed under each tab (a folded tab header shows "!" for a new one)
            bool foundSelected = false;
            bool any = false;
            int shown = 0;
            string filter = searchFilter?.Trim();
            // "#Name" searches by ingredient (recipes that take Name as an input); plain text searches by product name, as before.
            bool byIngredient = !string.IsNullOrEmpty(filter) && filter[0] == '#';
            if (byIngredient) filter = filter.Substring(1).Trim();
            bool filtering = !string.IsNullOrEmpty(filter);

            // Per tab (a machine, or a vanilla station; a station with a machine twin shares the machine's tab, see RecipeCatalog.TabOf):
            // the machine's own recipes first, then the station's; inside each of those favorited first, then crafted, then the faded ones.
            // OrderBy is stable, so each tier keeps the catalog's A-Z order, and tabs keep the catalog order of their first entry
            // (counted even while nothing in the tab is listed yet, so a merged tab always sits where its machine is).
            RecipeBookNew.RecordTabOrder(favorites);   // R1.8: a tab found since the last poll gets its place in the unlock sequence before the list is sorted
            var machineOrder = new Dictionary<string, int>();
            var listed = new List<(RecipeCatalog.Entry e, RecipeState state)>();
            foreach (RecipeCatalog.Entry cat in RecipeCatalog.EntriesWithArt())
            {
                string catTab = RecipeCatalog.TabOf(cat.Machine);
                if (!machineOrder.ContainsKey(catTab)) machineOrder[catTab] = catTab == VanillaStationBooks.HandName ? -1 : machineOrder.Count;   // By Hand always comes first
                if (!RecipeTabDiscovery.IsDiscovered(catTab)) continue;   // the tab has not been discovered yet (RecipeTabDiscovery)
                RecipeState catState = RecipeVisibility.GetState(cat.Key);
                if (!RecipeVisibility.InBook(catState)) continue;
                listed.Add((cat, catState));
            }

            // A merged tab lists a product once: when the machine already lists it, the twin station's entry for the same product is
            // left out (the crafted-anywhere rule already treats them as one). The left-out key maps to the machine's key, so a jump,
            // a parchment or a saved selection that names the station key still lands on the visible entry.
            var machineProducts = new Dictionary<string, string>();
            foreach (var (me, _) in listed)
                if (me.Group != null && !me.Station) machineProducts.TryAdd(RecipeCatalog.TabOf(me.Machine) + "|" + RecipeKey.For(me.Group), me.Key);
            listed.RemoveAll(x =>
            {
                if (x.e.Group == null || !x.e.Station) return false;
                string tab = RecipeCatalog.TabOf(x.e.Machine);
                if (tab == x.e.Machine) return false;   // a station tab without a machine twin
                if (!machineProducts.TryGetValue(tab + "|" + RecipeKey.For(x.e.Group), out string own)) return false;
                aliases[x.e.Key] = own;
                return true;
            });

            // Several machines of one tab (Furnace and Hellforge share the Hellforge tab) list a product once: the first machine's entry
            // stays, the others map to it. The detail page merges their recipes (RecipeCatalog.MergedRecipes), each with its own fuel.
            var firstOfProduct = new Dictionary<string, string>();
            listed.RemoveAll(x =>
            {
                if (x.e.Group == null || x.e.Station) return false;
                string id = RecipeCatalog.TabOf(x.e.Machine) + "|" + RecipeKey.For(x.e.Group);
                if (!firstOfProduct.TryGetValue(id, out string first))
                {
                    firstOfProduct[id] = x.e.Key;
                    return false;
                }
                aliases[x.e.Key] = first;
                return true;
            });
            SelectedKey = Resolve(SelectedKey);

            // Ingredient search: if some listed recipe takes an ingredient named exactly like the text ("Wood"), only exact
            // names match, so "#Wood" does not also pull in Boreal Wood. Otherwise it falls back to a partial match.
            bool exactIngredient = byIngredient && filtering && listed.Any(x => x.e.Group != null && UsesIngredient(x.e.Group, x.state, filter, true));

            foreach (var (e, state) in listed.OrderBy(x => TabSort(favorites, RecipeCatalog.TabOf(x.e.Machine))).ThenBy(x => machineOrder[RecipeCatalog.TabOf(x.e.Machine)]).ThenBy(x => x.e.Station ? 1 : 0).ThenBy(x => favorites.IsFavorite(x.e.Key) ? 0 : favorites.IsNew(x.e.Key) ? 2 : x.state == RecipeState.Crafted ? 1 : 3))
            {
                any = true;

                // Selection is tracked against the whole book, not the filtered list, so a search never drops it.
                bool isSelected = e.Key == SelectedKey;
                if (isSelected)
                {
                    foundSelected = true;
                    SelectedMachine = e.Machine;
                    SelectedGroup = e.Group;
                    SelectedArt = e.Art;
                    SelectedState = state;
                }

                if (filtering)
                {
                    string searchName = e.Art != null ? e.Art.Name : e.Group.Key.DisplayName;
                    bool matches = byIngredient
                        ? e.Group != null && UsesIngredient(e.Group, state, filter, exactIngredient)
                        : searchName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!matches) continue;
                }
                shown++;

                // Headers stay toggleable while searching: a folded tab hides its matches until it is opened again.
                string tabName = RecipeCatalog.TabOf(e.Machine);
                bool collapsed = favorites.IsTabCollapsed(tabName);
                if (!tabKeys.TryGetValue(tabName, out List<string> childKeys)) tabKeys[tabName] = childKeys = new List<string>();
                childKeys.Add(e.Key);
                if (tabName != lastMachine)
                {
                    lastMachine = tabName;
                    if (gridCol > 0) { gridCol = 0; y += gridStep; }   // R7: a half-filled grid row ends where the next tab starts
                    AddRow(new RecipeBookHeaderRow(tabName, collapsed, ToggleMachine, childKeys), y, true);
                    y += RecipeBookLayout.HeaderRowHeight;
                }
                if (collapsed) continue;

                bool isFavorite = favorites.IsFavorite(e.Key);
                bool cellGrid = grid && e.Art == null;   // art entries (Notes tab) always stay in rows, whatever the layout button says
                RecipeBookEntry entry;
                if (cellGrid)
                    entry = e.Art != null
                        ? new RecipeBookGridCell(e.Key, e.Machine, e.Art, isFavorite)
                        : new RecipeBookGridCell(e.Key, e.Machine, e.Group, state, isFavorite);
                else
                    entry = e.Art != null
                        ? new RecipeBookEntry(e.Key, e.Machine, e.Art, isFavorite)
                        : new RecipeBookEntry(e.Key, e.Machine, e.Group, state, isFavorite);
                entry.OnChosen = key => Select(key, false);
                entry.Selected = isSelected;
                entryTop[e.Key] = y;
                if (cellGrid)
                {
                    entry.Left.Set(gridX0 + (float)Math.Round(gridCol * gridStepX), 0f);
                    AddRow(entry, y);
                    if (++gridCol >= gridCols) { gridCol = 0; y += gridStep; }
                }
                else
                {
                    AddRow(entry, y);
                    y += RecipeBookLayout.EntryRowHeight;
                }
            }

            if (gridCol > 0) y += gridStep;   // R7: the last grid row of the last tab

            // Headers go in last so they draw above the entries (a pinned header must cover the rows that scroll under it).
            foreach (UIElement header in headerRows) listArea.Append(header);

            // Stamped AFTER the loop: GetState can run the first inventory scan, which bumps HeldVersion.
            builtHeldVersion = RecipeVisibility.HeldVersion;

            IsEmpty = !any;
            NoResults = any && shown == 0;
            if ((searchBar != null) == IsEmpty)
            {
                BuildSearchBar();
                if (searchBar != null && !string.IsNullOrEmpty(searchFilter)) searchBar.SetText(searchFilter);
            }
            if (!foundSelected) ClearSelection();

            totalHeight = y;
            float viewH = RecipeBookLayout.ListArea.Height;
            scrollbar.SetBookView(viewH, Math.Max(totalHeight, viewH));
            ApplyScroll(true);
            if (modeChanged) ScrollToSelected();   // R7: the list changed height, so bring the selected recipe back into view
        }

        /// <summary>R1.8: By Hand first, then the tabs in the order this character found them in this world (unrecorded ones last, in catalog order).</summary>
        static int TabSort(RecipeBookPlayer player, string tab) => tab == VanillaStationBooks.HandName ? int.MinValue : tab == RecipeArtRegistry.Machine ? int.MaxValue : player.TabRank(tab);   // By Hand first, Notes (art) last

        // ------------------------------------------------------------------------------------------
        // Search
        // ------------------------------------------------------------------------------------------
        void BuildSearchBar()
        {
            if (panel == null) return;
            if (searchBar != null)
            {
                panel.RemoveChild(searchBar);
                searchBar = null;
            }
            if (IsEmpty) return;   // "The pages are blank": nothing to search, so no bar (Rebuild brings it back once something is listed)

            Rectangle s = RecipeBookLayout.SearchArea;
            searchBar = new RecipeSearchBar(text =>
            {
                searchFilter = text ?? "";
                filterDirty = true;
            }, RecipeBookLayout.SearchTheme);
            searchBar.Left.Set(s.X, 0f);
            searchBar.Top.Set(s.Y, 0f);
            searchBar.Width.Set(s.Width, 0f);
            searchBar.Height.Set(s.Height, 0f);
            searchBar.HoverText = () => string.Format(RecipeBookLayout.Text("SearchTooltip"), RecipeBookKeybindSystem.ItemSearchKeyName());
            panel.Append(searchBar);
        }

        /// <summary>
        /// Clears the search text and focus. Called by RecipeBookSystem every time the book is opened, so a stale
        /// filter can never hide the entry a parchment just unlocked. The bar is recreated (RecipeSearchBar has no SetText).
        /// </summary>
        public void ResetSearch()
        {
            searchFilter = "";
            filterDirty = true;
            BuildSearchBar();   // no-op before OnInitialize; the first OnInitialize builds a fresh bar
        }

        /// <summary>Types text into the search bar from code (the item-search keybind uses "#Name"). Clears the selection so the right page does not show a recipe the filter hides.</summary>
        public void SetSearch(string text)
        {
            searchFilter = text ?? "";
            filterDirty = true;
            searchBar?.SetText(searchFilter);
            ClearSelection();
        }

        /// <summary>
        /// Does any recipe of this group take an ingredient called `name`? Follows the detail page's reveal rule, so the search
        /// never matches an ingredient the page would show as "???": a Crafted recipe shows every input; otherwise an item input
        /// needs to have been seen (or crafted), liquids are hidden on held-ingredient recipes, and a made liquid needs to have been crafted.
        /// </summary>
        static bool UsesIngredient(RecipeOutputGroup group, RecipeState state, string name, bool exact)
        {
            bool reveal = state != RecipeState.Crafted;
            bool fadeLiquids = state == RecipeState.FadedIngredient;
            foreach (CustomRecipe recipe in group.Recipes)
            {
                foreach (RecipeIngredient input in recipe.Inputs)
                {
                    if (reveal && !RecipeVisibility.IsIngredientRevealed(input.Type)) continue;
                    if (NameMatches(ContentSamples.ItemsByType[input.Type].Name, name, exact)) return true;
                }
                foreach (LiquidIngredient liquid in recipe.LiquidInputs)
                {
                    if (fadeLiquids && (liquid.IsAnyWater || !RecipeVisibility.IsLiquidRevealed(liquid.LiquidType))) continue;
                    if (reveal && !liquid.IsAnyWater && !RecipeVisibility.IsLiquidRevealed(liquid.LiquidType)) continue;
                    string liquidName = liquid.IsAnyWater ? RecipeBookLayout.Text("AnyWater") : LiquidTypeRegistry.Get(liquid.LiquidType).Name;
                    if (NameMatches(liquidName, name, exact)) return true;
                }
            }
            return false;
        }

        static bool NameMatches(string candidate, string name, bool exact) =>
            exact ? string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase)
                  : candidate.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0;

        // ------------------------------------------------------------------------------------------
        // Window dragging (the handle lives in RecipeBookDragHandle)
        // ------------------------------------------------------------------------------------------
        public Vector2 PanelOffset => dragOffset;

        public void SetPanelOffset(Vector2 offset)
        {
            dragOffset = offset;
            ApplyPosition();
        }

        /// <summary>Back to the centered default.</summary>
        public void ResetPanelOffset() => SetPanelOffset(Vector2.Zero);

        /// <summary>
        /// Clamps the offset so the drag bar can never leave the screen, then moves the panel. The panel keeps
        /// HAlign/VAlign = 0.5 and the offset is added as Left/Top pixels, so the centered default stays the origin.
        /// </summary>
        void ApplyPosition()
        {
            if (panel == null) return;

            CalculatedStyle dims = GetDimensions();
            if (dims.Width > 0f && dims.Height > 0f)
            {
                float keep = RecipeBookLayout.DragKeepVisible;
                float centerX = (dims.Width - RecipeBookLayout.PanelWidth) / 2f;
                float centerY = (dims.Height - RecipeBookLayout.PanelHeight) / 2f;

                dragOffset.X = MathHelper.Clamp(dragOffset.X, -(RecipeBookLayout.PanelWidth - keep) - centerX, dims.Width - keep - centerX);
                dragOffset.Y = MathHelper.Clamp(dragOffset.Y, -centerY, dims.Height - RecipeBookLayout.Margin - centerY);
            }

            panel.Left.Set(dragOffset.X, 0f);
            panel.Top.Set(dragOffset.Y, 0f);
            panel.Recalculate();
        }

        void AddRow(UIElement row, float y, bool header = false)
        {
            row.Top.Set(y, 0f);
            if (header)
            {
                headerIndex.Add(rows.Count);
                headerRows.Add(row);   // appended after the loop, see Rebuild
            }
            else listArea.Append(row);
            rows.Add(row);
            rowBase.Add(y);
        }

        void ApplyScroll(bool force)
        {
            if (scrollbar == null) return;
            float view = scrollbar.ViewPosition;
            if (!force && view == lastView) return;
            lastView = view;
            for (int i = 0; i < rows.Count; i++) rows[i].Top.Set(rowBase[i] - view, 0f);

            // Sticky headers: a header scrolled past the top margin stays pinned there. The next header pushes it up as it arrives and
            // then holds the slot itself, so only one machine name is ever pinned. Earlier headers end up above the top and are clipped.
            float headerH = RecipeBookLayout.HeaderRowHeight;
            for (int h = 0; h < headerIndex.Count; h++)
            {
                var header = (RecipeBookHeaderRow)rows[headerIndex[h]];
                float natural = rowBase[headerIndex[h]] - view;
                header.Pinned = natural < 0f;
                if (!header.Pinned) continue;
                float pinnedTop = 0f;
                if (h + 1 < headerIndex.Count) pinnedTop = Math.Min(0f, rowBase[headerIndex[h + 1]] - view - headerH);
                header.Top.Set(pinnedTop, 0f);
            }
            listArea.Recalculate();
        }

        public void ScrollBy(float wheelValue)
        {
            if (scrollbar != null) scrollbar.ViewPosition -= wheelValue;
        }

        // ------------------------------------------------------------------------------------------
        // Selection
        // ------------------------------------------------------------------------------------------
        void ToggleMachine(string machine)
        {
            Main.LocalPlayer.GetModPlayer<RecipeBookPlayer>().ToggleTabCollapsed(machine);
            layoutDirty = true;
        }

        /// <summary>The key of the visible entry for a recipe key: a station recipe left out of a merged tab maps to its machine's entry.</summary>
        string Resolve(string key) => key != null && aliases.TryGetValue(key, out string shown) ? shown : key;

        void ExpandMachineOf(string key)
        {
            string machine = RecipeCatalog.TabOf(RecipeCatalog.MachineOf(key));
            if (machine != null && Main.LocalPlayer.GetModPlayer<RecipeBookPlayer>().SetTabCollapsed(machine, false)) Rebuild();
        }

        void ClearSelection()
        {
            SelectedKey = null;
            SelectedMachine = null;
            SelectedGroup = null;
            SelectedArt = null;
            SelectedState = RecipeState.Hidden;
        }

        /// <summary>Selects a recipe by catalog key. Ignored if it is not in the book list.</summary>
        public void Select(string key, bool scrollTo)
        {
            if (key == null) return;
            key = Resolve(key);
            ExpandMachineOf(key);   // a jump (ingredient click, parchment) must not land inside a folded machine
            if (!entryTop.ContainsKey(key)) return;

            SelectedKey = key;
            Main.LocalPlayer.GetModPlayer<RecipeBookPlayer>().MarkViewed(key);   // opened by a click or a jump: no longer new
            foreach (UIElement row in rows)
            {
                if (row is not RecipeBookEntry entry) continue;
                entry.Selected = entry.Key == key;
                if (entry.Selected)
                {
                    SelectedMachine = entry.Machine;
                    SelectedGroup = entry.Group;
                    SelectedArt = entry.Art;
                    SelectedState = entry.State;
                }
            }
            if (scrollTo) ScrollToSelected();
        }

        /// <summary>
        /// Jumps to a recipe from inside the book (ingredient click). If a search filter hides the target, the filter is cleared first.
        /// Ignored if the recipe is not in the book list.
        /// </summary>
        public void GoTo(string key)
        {
            if (key == null) return;
            key = Resolve(key);
            if (!entryTop.ContainsKey(key) && !string.IsNullOrEmpty(searchFilter?.Trim()))
            {
                ResetSearch();
                Rebuild();
            }
            Select(key, true);
        }

        /// <summary>
        /// Ingredient click: types the item's plain name in the search bar (product-name filter, so the target recipe stays listed) and selects
        /// the recipe that makes it. Client-only UI, nothing is synced. No name = plain GoTo.
        /// </summary>
        public void GoToAndSearch(string key, string text)
        {
            if (key == null) return;
            if (string.IsNullOrWhiteSpace(text)) { GoTo(key); return; }

            searchFilter = text;
            searchBar?.SetText(text);
            SelectedKey = key;   // Rebuild tracks the selection against the whole book, so it survives the new filter
            Rebuild();
            Select(key, true);
        }

        void ScrollToSelected()
        {
            if (SelectedKey == null || !entryTop.TryGetValue(SelectedKey, out float top)) return;
            float viewH = RecipeBookLayout.ListArea.Height;
            float view = scrollbar.ViewPosition;
            float h = SelectedArt != null ? RecipeBookLayout.EntryRowHeight : entryHeight;   // art entries are rows even in Grid
            if (top - RecipeBookLayout.HeaderRowHeight < view) scrollbar.ViewPosition = top - RecipeBookLayout.HeaderRowHeight;
            else if (top + h > view + viewH) scrollbar.ViewPosition = top + h - viewH;
            ApplyScroll(false);
        }
    }

    /// <summary>The book itself: draws the background and left-page title, blocks clicks, and forwards the mouse wheel to the list.</summary>
    public class RecipeBookPanel : UIElement
    {
        readonly RecipeBookState book;
        public bool MouseInside { get; private set; }

        public RecipeBookPanel(RecipeBookState book) { this.book = book; }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            Vector2 mouse = Main.MouseScreen;
            MouseInside = ContainsPoint(mouse);
            if (!MouseInside) return;

            Main.LocalPlayer.mouseInterface = true;

            Rectangle origin = GetDimensions().ToRectangle();
            Rectangle list = RecipeBookLayout.ListArea;
            list.Offset(origin.X, origin.Y);
            if (list.Contains(mouse.ToPoint())) PlayerInput.LockVanillaMouseScroll("Factorraria/RecipeBook");   // wheel must not change the hotbar slot
        }

        public override void ScrollWheel(UIScrollWheelEvent evt)
        {
            base.ScrollWheel(evt);
            book.ScrollBy(evt.ScrollWheelValue);
        }

        protected override void DrawSelf(SpriteBatch sb)
        {
            Rectangle r = GetDimensions().ToRectangle();

            if (RecipeBookArt.Background != null)
            {
                float k = RecipeBookLayout.BackgroundScale;
                int w = (int)System.MathF.Round(r.Width * k);
                int h = (int)System.MathF.Round(r.Height * k);
                var bg = new Rectangle(r.Center.X - w / 2, r.Center.Y - h / 2, w, h);

                // Point sampling: the default bilinear filter smears the texture whenever its size differs from the panel's
                // (or the UI scale is not 100%). Restart the batch with the same UI transform, then restore it.
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
                sb.Draw(RecipeBookArt.Background.Value, bg, Color.White);
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
            }
            else
            {
                BookDraw.Frame(sb, r, RecipeBookLayout.CoverColor, new Color(52, 32, 14), 3);
                DrawPage(sb, RecipeBookLayout.LeftPage, r);
                DrawPage(sb, RecipeBookLayout.RightPage, r);
            }

        }

        static void DrawPage(SpriteBatch sb, Rectangle page, Rectangle origin)
        {
            page.Offset(origin.X, origin.Y);
            BookDraw.Frame(sb, page, RecipeBookLayout.PageColor, RecipeBookLayout.PageBorder, 2);
        }
    }
}