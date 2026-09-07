using Factorraria.Content.Tiles.Liquids.CustomLiquids;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Items.Liquids.Buckets
{

    public class OilBucket : ModItem
    {
        public override void SetStaticDefaults()
        {
            // Registers this item as what an Empty Bucket turns into when scooped
            // from a pool of Oil — ModLiquidLib's bucket hook reads this set to
            // decide what to create, same as vanilla Water/Lava/Honey buckets.
            ModLiquidLib.ID.LiquidID_TLmod.Sets.CreateLiquidBucketItem[ModContent.GetInstance<OilLiquid>().Type] = Type;
        }

        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 9999;
            Item.consumable = true; // let tModLoader auto-decrement the stack by 1 on a successful UseItem
            Item.useStyle = ItemUseStyleID.Swing;
            Item.autoReuse = true;
            Item.useTurn = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.UseSound = SoundID.Item1;
            Item.value = 100;
            Item.rare = ItemRarityID.White;
        }

        public override bool? UseItem(Player player)
        {
            int x = Player.tileTargetX;
            int y = Player.tileTargetY;

            Tile tile = Main.tile[x, y];
            if (tile.LiquidAmount > 0 && tile.LiquidType != ModContent.GetInstance<OilLiquid>().Type)
                return false;

            WorldGen.PlaceLiquid(x, y, (byte)ModContent.GetInstance<OilLiquid>().Type, 255);

            if (Main.netMode != NetmodeID.Server)
                SoundEngine.PlaySound(SoundID.Splash, player.position);

            // Give back an Empty Bucket rather than transforming this stack into one —
            // QuickSpawnItem drops it into inventory if there's room, or spawns a
            // pickup on the ground next to the player if not.
            player.QuickSpawnItem(player.GetSource_FromThis(), ItemID.EmptyBucket, 1);

            return true;
        }
    }
}
