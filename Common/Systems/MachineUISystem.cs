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

        // Patch 6.1: the open machine's own position (its entity's Position, the top-left corner). A multiplayer client re-resolves the
        // live replica from it every frame, because a replica can be replaced by a later sync.
        Point16 openEntityPosition;

        // Patch 6.1: a multiplayer client right-clicked a machine tile whose replica has not arrived yet: the UI opens when it does.
        bool hasPendingOpen;
        Point16 pendingOpenTile;
        uint pendingOpenUntil;

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
            openEntityPosition = entity != null ? entity.Position : clickedPosition;
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

        // Walking away closes like clicking the machine again, plus the inventory (patches 5.3b and 5.3c). The silent closes (killed,
        // dead, refused, Esc) use CloseUI directly.
        void CloseFull(bool notifyServer)
        {
            SoundEngine.PlaySound(SoundID.MenuClose);
            Main.playerInventory = false;
            CloseUI(notifyServer);
        }

        // Test aid (temporary, patch 5.3c): which path closed or tried to close the machine UI on a multiplayer client.
        void LogClosed(string why)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                FactorrariaNet.Log("[Detail] machine UI close on the client: " + why);
        }

        // Patch 6.1: a multiplayer client with no replica for the machine tile at i, j asks the server (an open request makes the server
        // create and share the entity) and opens the UI as soon as the replica is here. Gives up after 3 seconds.
        public void RequestOpenWhenReplicaArrives(int i, int j)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient || (hasPendingOpen && Main.GameUpdateCount <= pendingOpenUntil))
                return;

            hasPendingOpen = true;
            pendingOpenTile = new Point16((short)i, (short)j);
            pendingOpenUntil = Main.GameUpdateCount + 180;
            FactorrariaNet.Log("[Detail] open request for the machine tile at (" + i + ", " + j + "): no replica here yet, asked the server");
            MachineDetail.SendSubscribe(pendingOpenTile, true);
        }

        public void NotifyMachineKilled(BaseMachine machine)
        {
            if (openState != null && openState.CurrentEntity == machine)
            {
                LogClosed("OnKill ran on the client for the open machine");
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

            if (closeFromServer)
            {
                if (openState?.CurrentEntity != null && openState.CurrentEntity.Position == closePosition)
                {
                    LogClosed("server notice at (" + closePosition.X + ", " + closePosition.Y + "), " + (closeFull ? "full close" : "silent close"));
                    if (closeFull)
                        CloseFull(false);
                    else
                        CloseUI(false);
                }
                else
                {
                    LogClosed("server notice at (" + closePosition.X + ", " + closePosition.Y + ") ignored, open machine: "
                        + (openState?.CurrentEntity != null ? "(" + openState.CurrentEntity.Position.X + ", " + openState.CurrentEntity.Position.Y + ")" : "none"));
                }
            }

            // Patch 5.3c: a multiplayer client does not depend on the server notice alone. When its own living player is further away than
            // the server's keep-reach (the same formula the server uses), it does the full close itself: sound, inventory, panel.
            if (Main.netMode != NetmodeID.Server && Main.playerInventory && openState?.CurrentEntity is BaseMachine reachMachine
                && !Main.LocalPlayer.dead
                && !MachineDetailSystem.WithinReach(Main.LocalPlayer, reachMachine, ModContent.GetInstance<MachineDetailSystem>().KeepReachExtraTiles))
            {
                LogClosed("local player out of reach of (" + reachMachine.Position.X + ", " + reachMachine.Position.Y + ")");
                CloseFull(true);
            }

            if (!Main.playerInventory) // player closed their inventory (Esc, etc) — close ours too
            {
                if (openState != null)
                    LogClosed("inventory was closed");
                CloseUI();
            }

            // Patches 4.4 and 6.1: on a multiplayer client the replica can be replaced by a later sync, or disappear without our OnKill
            // running. The UI keeps the machine's position and re-resolves the live replica every frame: a replaced one is rebound (so the
            // UI and OpenMachine, which the arm's area editor reads, never touch a stale copy), a missing one closes the UI.
            if (Main.netMode == NetmodeID.MultiplayerClient && openState?.CurrentEntity != null)
            {
                if (TileEntity.ByPosition.TryGetValue(openEntityPosition, out TileEntity resolved) && resolved is BaseMachine live)
                {
                    if (!ReferenceEquals(live, openState.CurrentEntity))
                    {
                        FactorrariaNet.Log("[Detail] machine UI rebound to the live replica at (" + openEntityPosition.X + ", " + openEntityPosition.Y + ")");
                        openState.CurrentEntity = live;
                    }
                }
                else
                {
                    LogClosed("the open machine's replica was removed");
                    CloseUI();
                }
            }

            // Patch 6.1: finish an open that was waiting for its replica.
            if (hasPendingOpen)
            {
                if (Main.GameUpdateCount > pendingOpenUntil)
                {
                    hasPendingOpen = false;
                    FactorrariaNet.Log("[Detail] pending open at (" + pendingOpenTile.X + ", " + pendingOpenTile.Y + ") gave up: the replica never arrived");
                }
                else if (TileEntityHelper.TryGetEntityFromTile(pendingOpenTile.X, pendingOpenTile.Y, out TileEntity arrived, out Point16 arrivedCorner) && arrived is BaseMachine arrivedMachine)
                {
                    hasPendingOpen = false;
                    FactorrariaNet.Log("[Detail] replica arrived for the pending open at (" + arrivedCorner.X + ", " + arrivedCorner.Y + ")");
                    arrivedMachine.OnRightClick(arrivedCorner.X, arrivedCorner.Y);
                }
            }

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
