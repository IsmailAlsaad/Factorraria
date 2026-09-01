using Factorraria.Content.Configs;
using Factorraria.Content.Tiles.Machines.Furnace;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using tModPorter;

namespace Factorraria.Common.Systems
{
    public class FurnaceUISystem : ModSystem
    {
        UserInterface furnaceInterface;

        FurnaceUIState furnaceUIState;

        public Point16 OpenFurnacePosition;

        public override void Load()
        {
            if (Main.dedServ)
            {
                return;
            }

            furnaceInterface = new UserInterface();
            furnaceUIState = new FurnaceUIState();
        }

        public void OpenFurnaceUI(int i, int j)
        {
            TileEntityHelper.TryGetEntityFromTile(i, j, out TileEntity entity, out Point16 TopLeftPosition);

            if (furnaceInterface.CurrentState == furnaceUIState && OpenFurnacePosition == TopLeftPosition)
            {
                SoundEngine.PlaySound(SoundID.MenuClose);
                CloseFurnaceUI();
                return;
            }

            SoundEngine.PlaySound(SoundID.MenuOpen);

            OpenFurnacePosition = TopLeftPosition;
            furnaceUIState.currentTileEntity = TileEntityHelper.GetOrCreateEntity<FurnaceTileEntity>(OpenFurnacePosition.X,OpenFurnacePosition.Y);

            Main.playerInventory = true;

            furnaceInterface.Recalculate();
            furnaceInterface.SetState(null);
            furnaceInterface.SetState(furnaceUIState);
        }

        public void CloseFurnaceUI()
        {
            furnaceInterface.SetState(null);
        }

        public override void UpdateUI(Microsoft.Xna.Framework.GameTime gameTime)
        {
            //
            //UpdateUIPosition();
            //
            if (!Main.playerInventory)
            {
                CloseFurnaceUI();
            }

            furnaceInterface?.Update(gameTime);
        }

        public override void ModifyInterfaceLayers(System.Collections.Generic.List<GameInterfaceLayer> layers)
        {
            var customLayer = new LegacyGameInterfaceLayer(
                "Factorraria: Furnace UI",
                delegate
                {
                    if (furnaceInterface?.CurrentState != null)
                    {
                        UpdateUIPosition();
                        furnaceInterface.Draw(Main.spriteBatch, new GameTime());
                    }
                    return true;
                },
                InterfaceScaleType.UI);

            int InventoryLayerIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Inventory"));

            if(InventoryLayerIndex == -1)
            {
                return;
            }

            layers.Insert(InventoryLayerIndex + 1, customLayer);
        }

        public override void PostAddRecipes()
        {
            FurnaceRecipeRegistry.BuildFromExistingRecipes();
        }

        public void UpdateUIPosition()
        {
            Vector2 furnaceWorldPosition = OpenFurnacePosition.ToVector2() * 16;

            Vector2 furnaceScreenPosition = furnaceWorldPosition - Main.screenPosition;

            //
            furnaceScreenPosition = Vector2.Transform(furnaceScreenPosition, Main.GameViewMatrix.ZoomMatrix);
            furnaceScreenPosition /= Main.UIScale;
            //

            if (furnaceUIState.panel == null)
            {
                return;
            }

            //
            float zoomScale = Main.GameViewMatrix.ZoomMatrix.M11 / Main.UIScale;
            //

            FurnaceOffsetConfig config = ModContent.GetInstance<FurnaceOffsetConfig>();

            Vector2 offset = new Vector2(config.OffsetX, config.OffsetY) * zoomScale;
            UIElement panel = furnaceUIState.panel;
            panel.Left.Set(furnaceScreenPosition.X + offset.X, 0);
            panel.Top.Set(furnaceScreenPosition.Y + offset.Y, 0);

            furnaceUIState.SetZoomScale(zoomScale);
            furnaceUIState.Recalculate();
        }
    }
}
