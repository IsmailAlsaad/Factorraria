using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
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


        public UIItemSlotWrapper(int _context, Func<Item> _getItem, Action<Item> _setItem, Func<bool> _isVisible = null, Func<Item, bool> _canAcceptItem = null, Func<bool> _favoriteLook = null)
        {
            context = _context;
            getItem = _getItem;
            setItem = _setItem;
            isVisible = _isVisible;
            canAcceptItem = _canAcceptItem;
            favoriteLook = _favoriteLook;
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

            ItemSlot.Draw(spriteBatch, ref item, useFavoriteLook ? ItemSlot.Context.InventoryItem : context, drawPosition);
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
                ItemSlot.Handle(ref item, context);
                setItem(item);
            }

            //ItemSlot.Handle(ref item, context);
            //setItem(item);
        }
    }
}