using Factorraria.Common.Machines;
using Factorraria.Common.Net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.UI;

namespace Factorraria.Content.UI
{
    public class UIItemSlotWrapper : UIElement
    {
        int context;

        Func<Item> getItem;
        Action<Item> setItem;
        Func<bool> isVisible;
        Func<Item, bool> canAcceptItem;
        Func<bool> favoriteLook; // optional: draw the slot with the vanilla "favorited" look
        Action<SpriteBatch, CalculatedStyle, Item> customDraw; // optional: replaces the default ItemSlot.Draw (animated output slots)
        MachineSlotBinding machineSlot; // optional (patch 6.2): this slot is a machine slot, so on a multiplayer client its clicks are sent to the server
        private static readonly Item[] favoriteDrawArray = new Item[11];

        public UIItemSlotWrapper(int _context, Func<Item> _getItem, Action<Item> _setItem, Func<bool> _isVisible = null, Func<Item, bool> _canAcceptItem = null, Func<bool> _favoriteLook = null, Action<SpriteBatch, CalculatedStyle, Item> _customDraw = null, MachineSlotBinding _machineSlot = null)
        {
            context = _context;
            getItem = _getItem;
            setItem = _setItem;
            isVisible = _isVisible;
            canAcceptItem = _canAcceptItem;
            favoriteLook = _favoriteLook;
            customDraw = _customDraw;
            machineSlot = _machineSlot;
        }

        // Runs vanilla's slot click. The ChestItem context makes vanilla send a chest-slot sync for Player.chest, which is -1 for a machine
        // slot, and that throws IndexOutOfRangeException inside ItemSlot.Handle on a multiplayer client (survey U8, verification item 5). It
        // cannot be avoided any other way, so the click runs with netMode shown as single-player, for this one call only. Machine slots
        // (patch 6.2) send their own SlotEdit request afterwards; the cart slots still only edit this client's copy until Phase 9.
        void RunVanillaClick(ref Item item)
        {
            int realNetMode = Main.netMode;
            if (realNetMode == NetmodeID.MultiplayerClient)
                Main.netMode = NetmodeID.SinglePlayer;

            try
            {
                ItemSlot.Handle(ref item, context);
            }
            finally
            {
                Main.netMode = realNetMode;
            }
        }

        // Multiplayer client, machine slot (patch 6.2): vanilla predicts the click, the server decides (see MachineSlotEdit).
        void ClickMachineSlot(ref Item item)
        {
            BaseMachine machine = machineSlot.Machine();
            if (machine == null)
                return;

            // One edit in flight at a time: while the server's answer is on its way, a press on a machine slot does nothing (hovering
            // still shows the tooltip). The answer comes back within a round trip.
            if (MachineSlotEdit.IsBusy)
            {
                if (!Main.mouseLeft && !Main.mouseRight)
                    RunVanillaClick(ref item);
                return;
            }

            // Shift-click moves the stack into the player's inventory, which cannot be undone if the server refuses. So it is not
            // predicted: the request goes first and the items are handed over when the server accepts.
            if (!item.IsAir && Main.mouseLeft && Main.mouseLeftRelease && Main.keyState.PressingShift())
            {
                if (Main.LocalPlayer.ItemSpace(item).CanTakeItem)
                {
                    Main.mouseLeftRelease = false;
                    MachineSlotEdit.SendQuickTake(machine, machineSlot.Kind, machineSlot.Index, item);
                }
                return;
            }

            Item slotBefore = item.Clone();
            Item cursorBefore = Main.mouseItem.Clone();

            RunVanillaClick(ref item);

            if (!MachineSlotEdit.SameStack(slotBefore, item))
                MachineSlotEdit.SendEdit(machine, machineSlot.Kind, machineSlot.Index, slotBefore, item, cursorBefore, Main.mouseItem);
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            if (isVisible != null && !isVisible()) return;

            Item item = getItem();
            if (item == null)
            {
                item = new Item();
                setItem(item);
            }

            CalculatedStyle dimesions = GetDimensions();
            Vector2 drawPosition = new Vector2(dimesions.X, dimesions.Y);

            // NEW: work out the current scale from our actual on-screen width vs. the
            // real vanilla slot-background texture width — same idea as FireUIElement above.
            float scale = dimesions.Width / TextureAssets.InventoryBack.Value.Width;

            float oldScale = Main.inventoryScale;
            Main.inventoryScale = scale;
            // Favorited look: the inventory context draws a favorited item with the favorited slot background.
            bool useFavoriteLook = favoriteLook != null && !item.IsAir && favoriteLook();
            bool wasFavorited = item.favorited;
            if (useFavoriteLook)
            {
                item.favorited = true;
            }

            if (useFavoriteLook)
            {
                // Slot index 10, not 0: vanilla draws the hotbar number (1-0) on inventory-context slots below 10.
                favoriteDrawArray[10] = item;
                ItemSlot.Draw(spriteBatch, favoriteDrawArray, ItemSlot.Context.InventoryItem, 10, drawPosition);
                favoriteDrawArray[10] = null;
            }
            else
            {
                if (customDraw != null) customDraw(spriteBatch, dimesions, item);
                else ItemSlot.Draw(spriteBatch, ref item, context, drawPosition);
            }

            item.favorited = wasFavorited;
            Main.inventoryScale = oldScale;

            if (!IsMouseHovering)
            {
                return;
            }

            Main.LocalPlayer.mouseInterface = true;

            bool allowInteraction = true;
            if (!Main.mouseItem.IsAir && canAcceptItem != null)
            {
                allowInteraction = canAcceptItem(Main.mouseItem);   // optional filter: a slot can refuse items placed by the cursor
            }

            if (allowInteraction || (Main.mouseItem.IsAir && !item.IsAir))
            {
                if (machineSlot != null && Main.netMode == NetmodeID.MultiplayerClient)
                    ClickMachineSlot(ref item);   // patch 6.2: predicted here, decided by the server
                else
                    RunVanillaClick(ref item);    // single-player and the host edit the real slot; cart slots stay local until Phase 9

                setItem(item);
            }

            //ItemSlot.Handle(ref item, context);
            //setItem(item);
        }
    }
}