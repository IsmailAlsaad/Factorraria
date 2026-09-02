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
        Func<Item, bool> canAcceptItem;

        public UIItemSlotWrapper(int _context, Func<Item> _getItem, Action<Item> _setItem, Func<Item, bool> _canAcceptItem)
        {
            context = _context;
            getItem = _getItem;
            setItem = _setItem;
            canAcceptItem = _canAcceptItem;

        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
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
            ItemSlot.Draw(spriteBatch, ref item, context, drawPosition);
            Main.inventoryScale = oldScale;

            if (!IsMouseHovering)
            {
                return;
            }

            Main.LocalPlayer.mouseInterface = true;

            bool allowInteraction = true;
            if (!Main.mouseItem.IsAir && canAcceptItem != null)
            {
                allowInteraction = canAcceptItem(Main.mouseItem);
            }

            if (allowInteraction || (Main.mouseItem.IsAir && !item.IsAir))
            {
                ItemSlot.Handle(ref item, context);
                setItem(item);
            }
        }
    }
}