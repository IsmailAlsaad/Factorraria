using Factorraria.Common.Knowledge;
using Factorraria.Common.Machines;
using Factorraria.Common.UI.CustomUIElements;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.GameInput;
using Terraria.UI;

namespace Factorraria.Common.UI
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
        UIScrollbar scrollbar;
        RecipeSearchBar searchBar;
        string searchFilter = "";
        bool filterDirty;

        // Window position: pixels away from the centered spot. Static so the book reopens where the player left it
        // (for the game session). Zero = centered.
        static Vector2 dragOffset;
        int lastScreenW = -1, lastScreenH = -1;

        readonly List<UIElement> rows = new();
        readonly List<float> rowBase = new();
        readonly Dictionary<string, float> entryTop = new();
        float totalHeight;
        float lastView = -1f;
        int builtVersion = int.MinValue;

        // Selection is a catalog key so it survives rebuilds.
        public string SelectedKey { get; private set; }
        public string SelectedMachine { get; private set; }
        public RecipeOutputGroup SelectedGroup { get; private set; }
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
            scrollbar = new UIScrollbar();
            scrollbar.Left.Set(s.X, 0f);
            scrollbar.Top.Set(s.Y, 0f);
            scrollbar.Height.Set(s.Height, 0f);
            panel.Append(scrollbar);

            BuildSearchBar();

            panel.Append(new RecipeBookDetail(this));
        }

        public override void OnActivate()
        {
            // Opening always starts from fresh data. Selection (if any) is kept.
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
            if (RecipeKnowledgeSystem.Version != builtVersion || filterDirty) Rebuild();
            ApplyScroll(false);
            base.Update(gameTime);
        }

        // ------------------------------------------------------------------------------------------
        // List building
        // ------------------------------------------------------------------------------------------
        void Rebuild()
        {
            builtVersion = RecipeKnowledgeSystem.Version;
            filterDirty = false;
            if (listArea == null) return;

            listArea.RemoveAllChildren();
            rows.Clear();
            rowBase.Clear();
            entryTop.Clear();

            string lastMachine = null;
            float y = 0f;
            bool foundSelected = false;
            bool any = false;
            int shown = 0;
            string filter = searchFilter?.Trim();
            bool filtering = !string.IsNullOrEmpty(filter);

            foreach (RecipeCatalog.Entry e in RecipeCatalog.Entries())
            {
                RecipeState state = RecipeVisibility.GetState(e.Key);
                if (!RecipeVisibility.InBook(state)) continue;
                any = true;

                // Selection is tracked against the whole book, not the filtered list, so a search never drops it.
                bool isSelected = e.Key == SelectedKey;
                if (isSelected)
                {
                    foundSelected = true;
                    SelectedMachine = e.Machine;
                    SelectedGroup = e.Group;
                    SelectedState = state;
                }

                if (filtering && (state == RecipeState.Crafted ? e.Group.Key.DisplayName : RecipeBookLayout.UnknownName).IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                shown++;

                if (e.Machine != lastMachine)
                {
                    lastMachine = e.Machine;
                    AddRow(new RecipeBookHeaderRow(e.Machine), y);
                    y += RecipeBookLayout.HeaderRowHeight;
                }

                var entry = new RecipeBookEntry(e.Key, e.Machine, e.Group, state);
                entry.OnChosen = key => Select(key, false);
                entry.Selected = isSelected;
                entryTop[e.Key] = y;
                AddRow(entry, y);
                y += RecipeBookLayout.EntryRowHeight;
            }

            IsEmpty = !any;
            NoResults = any && shown == 0;
            if (!foundSelected) ClearSelection();

            totalHeight = y;
            float viewH = RecipeBookLayout.ListArea.Height;
            scrollbar.SetView(viewH, Math.Max(totalHeight, viewH));
            ApplyScroll(true);
        }

        // ------------------------------------------------------------------------------------------
        // Search
        // ------------------------------------------------------------------------------------------
        void BuildSearchBar()
        {
            if (panel == null) return;
            if (searchBar != null) panel.RemoveChild(searchBar);

            Rectangle s = RecipeBookLayout.SearchArea;
            searchBar = new RecipeSearchBar(text =>
            {
                searchFilter = text ?? "";
                filterDirty = true;
            });
            searchBar.Left.Set(s.X, 0f);
            searchBar.Top.Set(s.Y, 0f);
            searchBar.Width.Set(s.Width, 0f);
            searchBar.Height.Set(s.Height, 0f);
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

        void AddRow(UIElement row, float y)
        {
            row.Top.Set(y, 0f);
            listArea.Append(row);
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
            listArea.Recalculate();
        }

        public void ScrollBy(float wheelValue)
        {
            if (scrollbar != null) scrollbar.ViewPosition -= wheelValue;
        }

        // ------------------------------------------------------------------------------------------
        // Selection
        // ------------------------------------------------------------------------------------------
        void ClearSelection()
        {
            SelectedKey = null;
            SelectedMachine = null;
            SelectedGroup = null;
            SelectedState = RecipeState.Hidden;
        }

        /// <summary>Selects a recipe by catalog key. Ignored if it is not in the book list.</summary>
        public void Select(string key, bool scrollTo)
        {
            if (key == null || !entryTop.ContainsKey(key)) return;

            SelectedKey = key;
            foreach (UIElement row in rows)
            {
                if (row is not RecipeBookEntry entry) continue;
                entry.Selected = entry.Key == key;
                if (entry.Selected)
                {
                    SelectedMachine = entry.Machine;
                    SelectedGroup = entry.Group;
                    SelectedState = entry.State;
                }
            }
            if (scrollTo) ScrollToSelected();
        }

        void ScrollToSelected()
        {
            if (SelectedKey == null || !entryTop.TryGetValue(SelectedKey, out float top)) return;
            float viewH = RecipeBookLayout.ListArea.Height;
            float view = scrollbar.ViewPosition;
            if (top < view) scrollbar.ViewPosition = top - RecipeBookLayout.HeaderRowHeight;
            else if (top + RecipeBookLayout.EntryRowHeight > view + viewH) scrollbar.ViewPosition = top + RecipeBookLayout.EntryRowHeight - viewH;
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
                sb.Draw(RecipeBookArt.Background.Value, r, Color.White);
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