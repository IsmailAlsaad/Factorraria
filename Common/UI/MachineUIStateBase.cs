using Factorraria.Common.Machines;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria.UI;

namespace Factorraria.Common.UI
{
    public abstract class MachineUIStateBase : UIState
    {
        // Which specific machine (this exact furnace, this exact autohammer) we're showing right now.
        // Swapped out every time the player opens a different machine of the same type.
        public BaseMachine CurrentEntity;

        // The container that holds every visual piece for this machine's UI.
        public UIElement Panel;

        // The list of pieces (slots, icons, etc) inside the panel — built once, the first time this UI opens.
        List<MachineUIElementEntry> elements;

        // Every specific machine UI (e.g. FurnaceUIState) must say how big its panel is at normal (1x) zoom.
        protected abstract Vector2 BasePanelSize { get; }

        // Every specific machine UI must say which pieces go in its panel, and where.
        protected abstract List<MachineUIElementEntry> BuildElements();

        // tModLoader calls this once automatically, the first time this UI is created.
        public override void OnInitialize()
        {
            Panel = new UIElement();
            Panel.Width.Set(BasePanelSize.X, 0);
            Panel.Height.Set(BasePanelSize.Y, 0);
            Append(Panel); // Panel is now a child of this UIState

            elements = BuildElements();
            foreach (var entry in elements)
                Panel.Append(entry.Element); // each piece becomes a child of the panel
        }

        // This is the ONE place resizing happens, for every element, for every machine, forever.
        // It just multiplies each element's base position/size by the zoom factor and applies it
        // to that element's built-in Left/Top/Width/Height. That's it — nothing element-specific.
        public void SetZoomScale(float zoomScale)
        {
            if (elements == null) return;

            Panel.Width.Set(BasePanelSize.X * zoomScale, 0);
            Panel.Height.Set(BasePanelSize.Y * zoomScale, 0);

            foreach (var entry in elements)
            {
                entry.Element.Left.Set(entry.BasePosition.X * zoomScale, 0);
                entry.Element.Top.Set(entry.BasePosition.Y * zoomScale, 0);
                entry.Element.Width.Set(entry.BaseSize.X * zoomScale, 0);
                entry.Element.Height.Set(entry.BaseSize.Y * zoomScale, 0);
            }

            Panel.Recalculate();
        }
    }
}
