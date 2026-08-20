using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Items.Conveyors
{
    public class CounterClockwisePriorityConveyor : ModItem
    {
        public override void SetDefaults()
        {
            // Item inventory hitboxes
            Item.width = 16;
            Item.height = 16;
            Item.maxStack = Item.CommonMaxStack;

            // Placement animation settings
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;

            // Economy and rarity
            Item.value = Item.buyPrice(copper: 50);
            Item.rare = ItemRarityID.Yellow;

            // Binds this item to place your custom tile
            Item.createTile = ModContent.TileType<Tiles.Conveyors.CounterClockwisePriorityConveyorTile>();
        }

        // 1. Allow right-clicking this item inside the inventory
        public override bool CanRightClick()
        {
            return Main.keyState.IsKeyDown(Keys.LeftShift) || Main.keyState.IsKeyDown(Keys.RightShift);
        }

        // 2. Perform the transformation
        public override void RightClick(Player player)
        {
            int currentStack = Item.stack;

            // Swap to the counter-clockwise item type
            Item.SetDefaults(ModContent.ItemType<ClockwisePriorityConveyor>());
            Item.stack = currentStack;

            // Tactile audio feedback
            Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick);
        }

        // 3. Prevent tModLoader from consuming 1 item from the stack upon right-click
        public override bool ConsumeItem(Player player) => false;
    }
}