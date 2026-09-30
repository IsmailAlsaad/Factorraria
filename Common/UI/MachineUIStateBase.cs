using Factorraria.Common.Machines;
using Factorraria.Content.Configs;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace Factorraria.Common.UI
{
    public interface IZoomScalable { void SetZoomScale(float zoom); }

    public abstract class MachineUIStateBase : UIState
    {
        public BaseMachine CurrentEntity;
        public UIElement Panel;
        List<MachineUIElementEntry> elements;

        protected abstract Vector2 BasePanelSize { get; }
        public virtual Vector2 BasePanelOffset => Vector2.Zero;

        protected abstract List<MachineUIElementEntry> BuildElements();

        public override void OnInitialize()
        {
            Panel = new UIElement();
            Panel.Width.Set(BasePanelSize.X, 0);
            Panel.Height.Set(BasePanelSize.Y, 0);
            Append(Panel);

            InitializeUIState();
        }

        public void InitializeUIState()
        {
            Panel.RemoveAllChildren();

            elements = BuildElements();
            foreach (var entry in elements)
            {
                if (entry.Element == null)
                {
                    Main.NewText("Entry null detected");
                    continue;
                }

                Panel.Append(entry.Element);
            }
        }

        public override void OnActivate()
        {
            InitializeUIState();
        }

        public void SetZoomScale(float zoomScale)
        {
            if (elements == null) return;

            Panel.Width.Set(BasePanelSize.X * zoomScale, 0);
            Panel.Height.Set(BasePanelSize.Y * zoomScale, 0);

            foreach (var entry in elements)
            {
                if(entry.Element == null)
                {
                    continue;
                }

                entry.Element.Top.Set(entry.BasePosition.Y * zoomScale, 0);
                entry.Element.Left.Set(entry.BasePosition.X * zoomScale, 0);
                entry.Element.Width.Set(entry.BaseSize.X * zoomScale, 0);
                entry.Element.Height.Set(entry.BaseSize.Y * zoomScale, 0);

                if (entry.Element is IZoomScalable scalable)
                    scalable.SetZoomScale(zoomScale);
            }

            Panel.Recalculate();
        }
    }
}
