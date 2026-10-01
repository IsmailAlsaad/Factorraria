using Factorraria.Common.Machines;
using Factorraria.Content.Configs;
using Factorraria.Content.UI;
using Microsoft.Xna.Framework;
using System;
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

        protected virtual void UpdateLayout() { }

        public void SetZoomScale(float zoomScale)
        {
            if (elements == null) return;

            UpdateLayout();

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

        protected const float SlotSize = 54f;

        protected MachineUIElementEntry InputSlotEntry(int slotIndex, Vector2 pos, Func<bool> isVisible = null) =>
            new MachineUIElementEntry(
                new UIItemSlotWrapper(ItemSlot.Context.ChestItem,
                    () => CurrentEntity.InputSlots[slotIndex],
                    v => CurrentEntity.InputSlots[slotIndex] = v,
                    isVisible),
                pos, new Vector2(SlotSize, SlotSize));

        protected MachineUIElementEntry OutputSlotEntry(int slotIndex, Vector2 pos) =>
            new MachineUIElementEntry(
                new UIItemSlotWrapper(ItemSlot.Context.ChestItem,
                    () => CurrentEntity.OutputSlots[slotIndex],
                    v => CurrentEntity.OutputSlots[slotIndex] = v),
                pos, new Vector2(SlotSize, SlotSize));

        protected MachineUIElementEntry FuelSlotEntry(int fuelIndex, Vector2 pos) => InputSlotEntry(BaseMachine.FuelSlotIndex + fuelIndex, pos);
        protected MachineUIElementEntry FuelSlotEntry(Vector2 pos) => FuelSlotEntry(0, pos);

        protected MachineUIElementEntry InputTankEntry(int tankIndex, Vector2 pos) =>
            new MachineUIElementEntry(new LiquidTankUIElement(() => CurrentEntity.InputLiquids[tankIndex]), pos, new Vector2(16, 84));

        protected MachineUIElementEntry OutputTankEntry(int tankIndex, Vector2 pos) =>
            new MachineUIElementEntry(new LiquidTankUIElement(() => CurrentEntity.OutputLiquids[tankIndex]), pos, new Vector2(16, 84));

        protected void AddRecipePicker(List<MachineUIElementEntry> list, RecipeBook book, Vector2 buttonPos, Vector2 browserPos)
        {
            var browser = new RecipeBrowserPanel(book.Groups, () => CurrentEntity.ManualGroup, g => CurrentEntity.SetManualGroup(g));
            list.Add(new MachineUIElementEntry(browser, browserPos, new Vector2(155, 200)));
            list.Add(new MachineUIElementEntry(new RecipeSelectHammerIcon(browser), buttonPos, new Vector2(24, 24)));
        }
    }
}
