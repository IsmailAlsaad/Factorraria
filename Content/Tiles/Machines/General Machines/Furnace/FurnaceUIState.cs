using Factorraria.Content.Configs;
using Factorraria.Content.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.UI;
using tModPorter;

namespace Factorraria.Content.Tiles.Machines.Furnace
{
    public class FurnaceUIState : UIState
    {
        public FurnaceTileEntity currentTileEntity;
        public UIElement panel;

        UIItemSlotWrapper OreSlot;
        public Item GetOreItem() { return currentTileEntity.OreItem; }
        public void SetOreItem(Item newItem) { currentTileEntity.OreItem = newItem; }
        public bool CanAcceptOre(Item InputItem) { return FurnaceRecipeRegistry.SmeltingRecipes.ContainsKey(InputItem.type); }

        FireUIElement fireUI;
        public float GetProgress() { return currentTileEntity.GetSmeltPercent(); }

        UIItemSlotWrapper FuelSlot;
        public Item GetFuelItem() { return currentTileEntity.FuelItem; }
        public void SetFuelItem(Item newItem) { currentTileEntity.FuelItem = newItem; }
        public bool CanAcceptFuel(Item InputItem) { return FurnaceRecipeRegistry.ValidFuels.ContainsKey(InputItem.type); }

        public override void OnInitialize()
        {
            panel = new UIElement();

            panel.Height.Set(200, 0);
            panel.Width.Set(100, 0);
            Append(panel);

            FurnaceOffsetConfig config = ModContent.GetInstance<FurnaceOffsetConfig>();

            OreSlot = new UIItemSlotWrapper(ItemSlot.Context.ChestItem, 1f, GetOreItem, SetOreItem, CanAcceptOre);
            panel.Append(OreSlot);

            fireUI = new FireUIElement(GetProgress);
            panel.Append(fireUI);

            FuelSlot = new UIItemSlotWrapper(ItemSlot.Context.ChestItem, 1f, GetFuelItem, SetFuelItem, CanAcceptFuel);
            panel.Append(FuelSlot);
        }

        public void SetZoomScale(float zoomScale)
        {
            if(OreSlot == null || FuelSlot == null)
            {
                return;
            }

            panel.Height.Set(200 * zoomScale, 0);
            panel.Width.Set(100 * zoomScale, 0);

            OreSlot.setScale(zoomScale);
            OreSlot.Top.Set(0, 0);
            OreSlot.Left.Set(0, 0);
            fireUI.setScale(zoomScale * 0.84375f); // 54 slot size / 64 fire icon size
            fireUI.Top.Set(50 * zoomScale, 0);
            fireUI.Left.Set(0, 0);
            FuelSlot.setScale(zoomScale);
            FuelSlot.Top.Set(100 * zoomScale, 0);
            FuelSlot.Left.Set(0, 0);

            panel.Recalculate();
        }
    }
}
