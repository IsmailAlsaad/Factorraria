using Microsoft.Xna.Framework;
using ModLiquidLib.ID;
using ModLiquidLib.ModLoader;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.Liquid;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Liquids.Fuel
{
    public class FuelLiquid : ModLiquid
    {
        public override void SetStaticDefaults()
        {
            LiquidRenderer.VISCOSITY_MASK[Type] = 200;

            LiquidRenderer.WATERFALL_LENGTH[Type] = 20;

            LiquidRenderer.DEFAULT_OPACITY[Type] = 0.7f;
            SlopeOpacity = 0.7f;
            LiquidfallOpacityMultiplier = 0.5f;

            WaterRippleMultiplier = 0.3f;

            SplashDustType = DustID.SandstormInABottle;

            SplashSound = SoundID.Splash;

            FallDelay = 4;
            ChecksForDrowning = true;
            AllowEmitBreathBubbles = false;

            PlayerMovementMultiplier = 0.6f;
            StopWatchMPHMultiplier = PlayerMovementMultiplier;
            NPCMovementMultiplierDefault = PlayerMovementMultiplier;
            ProjectileMovementMultiplier = PlayerMovementMultiplier;

            LiquidID_TLmod.Sets.CanBeAbsorbedBy[Type].Remove(ItemID.UltraAbsorbantSponge);

            AddMapEntry(new Color(134, 104, 78));

            VanillaFallbackOnModDeletion = (ushort)LiquidID.Water;
        }

        public override int LiquidMerge(int i, int j, int otherLiquid)
        {
            if (otherLiquid == LiquidID.Water)
            {
                return TileID.TeamBlockBlue; //When the liquid collides with water. Blue team block is created
            }
            else if (otherLiquid == LiquidID.Lava)
            {
                return TileID.TeamBlockRed; //When the liquid collides with lava. Red team block is created
            }
            else if (otherLiquid == LiquidID.Honey)
            {
                return TileID.TeamBlockYellow; //When the liquid collides with honey. Yellow team block is created
            }
            else if (otherLiquid == LiquidID.Shimmer)
            {
                return TileID.TeamBlockPink; //When the liquid collides with shimmer. Pink team block is created
            }

            //The base return is what the liquid generates by default. This is useful for when this liquid collides with another modded liquids that this liquid has no support for.
            //usually by default, this method return TIleID.Stone, and generates a stone tile if it cannot recognise any predetermined tile type to generate with
            return TileID.TeamBlockWhite;

            //NOTE: for custom collisions/tile creation, please see PreLiquidMerge to determine whether the liquid should do its normal tile merging,
            //or if you want to do other effects when this liquid merges with another liquid.
        }
        public override int ChooseWaterfallStyle(int i, int j)
        {
            return ModContent.GetInstance<FuelLiquidFall>().Slot;
        }

        public override bool OnPlayerSplash(Player player, bool isEnter)
        {
            SoundEngine.PlaySound(SplashSound, player.position);
            return true;
        }

        public override bool OnNPCSplash(NPC npc, bool isEnter)
        {
            SoundEngine.PlaySound(SplashSound, npc.position);
            return true;
        }

        public override bool OnProjectileSplash(Projectile proj, bool isEnter)
        {
            SoundEngine.PlaySound(SplashSound, proj.position);
            return true;
        }

        public override bool OnItemSplash(Item item, bool isEnter)
        {
            SoundEngine.PlaySound(SplashSound, item.position);
            return true;
        }
    }

    public class FuelLiquidFall : ModLiquidFall
    {

    }
}
