using Factorraria.Common.Machines;
using Factorraria.Common.Net;
using Factorraria.Common.UI;
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

        // The machine whose UI is open right now (null when none). Read-only for other systems.
        public BaseMachine OpenMachine => openState?.CurrentEntity;

        public override void Load()
        {
            if (Main.dedServ) return; // server has no screen, skip UI setup entirely

            machineInterface = new UserInterface();
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

            RecipeBookSystem.Close(false); // the recipe book and a machine UI must never overlap
            global::Factorraria.Common.Carts.CartChestUISystem.Close(false); // nor a cart inventory

            openPosition = clickedPosition;
            state.CurrentEntity = entity; // tell the UI which specific machine to display
            openState = state;

            Main.playerInventory = true; // opens the normal inventory too, same as vanilla chests/furnaces

            machineInterface.SetState(null);  // clear first...
            machineInterface.SetState(state); // ...then set, forces a clean re-initialize

            // Patch 5.3: a multiplayer client asks the server for this machine's detail (slots, tanks, exact progress). Single-player
            // and the host read the real entity, so nothing is sent there.
            if (Main.netMode == NetmodeID.MultiplayerClient && entity != null)
                MachineDetail.SendSubscribe(entity.Position, true);
        }

        public void CloseUI() => CloseUI(true);

        // notifyServer: a multiplayer client tells the server it no longer has this machine open (patch 5.3). False when the server
        // already ended the subscription (it killed the machine or sent a close notice).
        void CloseUI(bool notifyServer)
        {
            if (notifyServer && openState?.CurrentEntity != null && Main.netMode == NetmodeID.MultiplayerClient)
                MachineDetail.SendSubscribe(openState.CurrentEntity.Position, false);

            machineInterface.SetState(null);
            openState = null;
        }

        public void NotifyMachineKilled(BaseMachine machine)
        {
            if (openState != null && openState.CurrentEntity == machine)
            {
                CloseUI(false);   // a killed machine's subscription is ended by the server
            }
        }

        // Patch 5.3: the server ended this player's subscription (machine killed, out of reach, dead, refused). The packet handler may run
        // on the network thread, so it only records the request; UpdateUI (main thread) closes the UI.
        readonly object pendingCloseLock = new object();
        bool hasPendingClose;
        bool pendingCloseFull;
        Point16 pendingClosePosition;

        public void RequestCloseFromServer(int x, int y, bool full = false)
        {
            lock (pendingCloseLock)
            {
                hasPendingClose = true;
                pendingCloseFull = full;
                pendingClosePosition = new Point16((short)x, (short)y);
            }
        }

        // tModLoader calls this every frame, automatically.
        public override void UpdateUI(GameTime gameTime)
        {
            bool closeFromServer = false;
            bool closeFull = false;
            Point16 closePosition;
            lock (pendingCloseLock)
            {
                closePosition = pendingClosePosition;
                closeFromServer = hasPendingClose;
                closeFull = pendingCloseFull;
                hasPendingClose = false;
            }

            if (closeFromServer && openState?.CurrentEntity != null && openState.CurrentEntity.Position == closePosition)
            {
                if (closeFull)
                {
                    SoundEngine.PlaySound(SoundID.MenuClose);
                    Main.playerInventory = false;
                }
                CloseUI(false);
            }

            if (!Main.playerInventory) // player closed their inventory (Esc, etc) — close ours too
                CloseUI();

            // Patch 4.4: on a multiplayer client the replica can disappear without our OnKill running; close the UI when the open machine is gone.
            if (Main.netMode == NetmodeID.MultiplayerClient && openState?.CurrentEntity is BaseMachine open
                && !(TileEntity.ByID.TryGetValue(open.ID, out TileEntity live) && ReferenceEquals(live, open)))
                CloseUI();

            machineInterface?.Update(gameTime);
        }

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
            //Main.NewText("layer inserted");
            layers.Insert(inventoryLayerIndex + 1, customLayer); // draw ours right after the inventory
        }

        // Works out where on screen the panel should sit, based on the machine's world position,
        // the camera, and current zoom — then tells the open UI to reposition/resize.
        void UpdateUIPosition()
        {
            if (openState == null) return;

            float zoomScale = Main.GameViewMatrix.ZoomMatrix.M11 / Main.UIScale;

            // Must run first: it recomputes the bounds that the offset below depends on.
            openState.SetZoomScale(zoomScale);

            Vector2 worldPosition = openPosition.ToVector2() * 16;
            Vector2 screenPosition = worldPosition - Main.screenPosition;
            screenPosition += openState.BasePanelOffset + openState.ContentOrigin;
            screenPosition = Vector2.Transform(screenPosition, Main.GameViewMatrix.ZoomMatrix);
            screenPosition /= Main.UIScale;

            openState.Panel.Left.Set(screenPosition.X, 0);
            openState.Panel.Top.Set(screenPosition.Y, 0);

            openState.Recalculate();
        }
    }
}
