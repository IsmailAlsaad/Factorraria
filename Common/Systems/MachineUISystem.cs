using Factorraria.Common.Machines;
using Factorraria.Common.UI;
using Factorraria.Content.Tiles.Machines.Furnace;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Factorraria.Common.Systems
{

    public class MachineUISystem : ModSystem
    {
        // tModLoader's container that actually displays whichever UI is "active" right now.
        UserInterface machineInterface;

        // Which UI is currently open (null = nothing open).
        MachineUIStateBase openState;

        // World tile position of the currently-open machine — needed to know where to draw the panel,
        // and to detect "clicked the same machine again" (close) vs "clicked a different one" (switch).
        Point16 openPosition;

        public override void Load()
        {
            if (Main.dedServ) return; // server has no screen, skip UI setup entirely

            machineInterface = new UserInterface();

            // --- Register every machine's UI here, once. This is the only place you add a line
            //     when you build a new machine's UI. Same spot/pattern as MachineVisualRegistry.Load(). ---
            MachineUIRegistry.Register(TileID.Furnaces, new FurnaceUIState());
            // MachineUIRegistry.Register(TileID.Autohammer, new AutohammerUIState()); // add later if it needs one
        }

        // Call this from a machine's right-click handler, e.g.:
        //   ModContent.GetInstance<MachineUISystem>().OpenUI(i, j, TileID.Furnaces, myFurnaceEntity);
        public void OpenUI(int i, int j, int tileType, BaseMachine entity)
        {
            // If this tile type has no UI registered, do nothing (e.g. autohammer, for now).
            if (!MachineUIRegistry.Definitions.TryGetValue(tileType, out MachineUIStateBase state))
                return;

            var clickedPosition = new Point16(i, j);

            // Clicking the SAME already-open machine again closes it (toggle behavior).
            if (openState == state && openPosition == clickedPosition)
            {
                SoundEngine.PlaySound(SoundID.MenuClose);
                CloseUI();
                return;
            }

            SoundEngine.PlaySound(SoundID.MenuOpen);

            openPosition = clickedPosition;
            state.CurrentEntity = entity; // tell the UI which specific machine to display
            openState = state;

            Main.playerInventory = true; // opens the normal inventory too, same as vanilla chests/furnaces

            machineInterface.SetState(null);  // clear first...
            machineInterface.SetState(state); // ...then set, forces a clean re-initialize
        }

        public void CloseUI()
        {
            machineInterface.SetState(null);
            openState = null;
        }

        // tModLoader calls this every frame, automatically.
        public override void UpdateUI(GameTime gameTime)
        {
            if (!Main.playerInventory) // player closed their inventory (Esc, etc) — close ours too
                CloseUI();

            machineInterface?.Update(gameTime);
        }

        // tModLoader calls this once at startup so we can insert our draw step into its layer list.
        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            var customLayer = new LegacyGameInterfaceLayer(
                "Factorraria: Machine UI",
                delegate
                {
                    if (machineInterface?.CurrentState != null)
                    {
                        UpdateUIPosition(); // reposition every frame in case player moves/zooms
                        machineInterface.Draw(Main.spriteBatch, new GameTime());
                    }
                    return true;
                },
                InterfaceScaleType.UI
            );

            int inventoryLayerIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Inventory"));
            if (inventoryLayerIndex == -1) return;
            layers.Insert(inventoryLayerIndex + 1, customLayer); // draw ours right after the inventory
        }

        // Works out where on screen the panel should sit, based on the machine's world position,
        // the camera, and current zoom — then tells the open UI to reposition/resize.
        void UpdateUIPosition()
        {
            if (openState == null) return;

            Vector2 worldPosition = openPosition.ToVector2() * 16;
            Vector2 screenPosition = worldPosition - Main.screenPosition;
            screenPosition = Vector2.Transform(screenPosition, Main.GameViewMatrix.ZoomMatrix);
            screenPosition /= Main.UIScale;

            float zoomScale = Main.GameViewMatrix.ZoomMatrix.M11 / Main.UIScale;

            openState.Panel.Left.Set(screenPosition.X, 0);
            openState.Panel.Top.Set(screenPosition.Y, 0);

            openState.SetZoomScale(zoomScale);
            openState.Recalculate();
        }

        public override void PostAddRecipes()
        {
            // --- Register every machine's Recipes here, once. This is the only place you add a line
            FurnaceRecipeRegistry.BuildFromExistingRecipes();
            // AutohammerRecipeRegistry.BuildFromExistingRecipes();
            // etc...
        }
    }
}
