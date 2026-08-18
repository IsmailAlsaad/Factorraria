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
        float scale;

        Func<Item> getItem;
        Action<Item> setItem;
        Func<Item, bool> canAcceptItem;

        public UIItemSlotWrapper(int _context,float _scale,Func<Item> _getItem,Action<Item> _setItem,Func<Item, bool> _canAcceptItem)
        {
            context = _context;
            scale = _scale;
            getItem = _getItem;
            setItem = _setItem;
            canAcceptItem = _canAcceptItem;

            ApplyScale();
        }

        void ApplyScale()
        {
            float slotDimension = TextureAssets.InventoryBack.Value.Width * scale;
            Height.Set(slotDimension, 0f);
            Width.Set(slotDimension, 0f);
        }

        public void setScale(float _scale)
        {
            scale = _scale;
            ApplyScale();
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

            float oldScale = Main.inventoryScale;
            Main.inventoryScale = scale;
            ItemSlot.Draw(spriteBatch,ref item,context,drawPosition);
            Main.inventoryScale = oldScale;

            //
            //Main.NewText($"hovering={IsMouseHovering} dims={dimesions.X},{dimesions.Y},{dimesions.Width}x{dimesions.Height} mouse={Main.MouseScreen}");
            //

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
