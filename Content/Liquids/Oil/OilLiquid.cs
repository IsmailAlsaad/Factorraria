using Microsoft.Xna.Framework;
using ModLiquidLib.ID;
using ModLiquidLib.ModLoader;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.Liquid;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Liquids.Oil
{
    public class OilLiquid : ModLiquid
    {
        public override void SetStaticDefaults()
        {
            LiquidRenderer.VISCOSITY_MASK[Type] = 240;

            LiquidRenderer.WATERFALL_LENGTH[Type] = 20;

            LiquidRenderer.DEFAULT_OPACITY[Type] = 1f;
            SlopeOpacity = 1f;
            LiquidfallOpacityMultiplier = 0.5f;

            WaterRippleMultiplier = 0.3f;

            SplashDustType = 36; //This is the dust ID for the oil splash dust, which is a black smoke effect.

            SplashSound = SoundID.Splash;

            FallDelay = 10;
            ChecksForDrowning = true;
            AllowEmitBreathBubbles = false;

            PlayerMovementMultiplier = 0.2f;
            StopWatchMPHMultiplier = PlayerMovementMultiplier; //We set stopwatch to the same multiplier as we don't want a different between whats felt and what the player can read their movement as.
            NPCMovementMultiplierDefault = PlayerMovementMultiplier; //NPCs have a similar modifier but as a field, here we set the default value as some other NPCs set this multiplier to 0. We set this to PlayerMovementMultiplier as we need them to all be the same.
            ProjectileMovementMultiplier = PlayerMovementMultiplier; //Simiarly to Players, Projectiles have this property for easy editing of a projectile velocity multiplier without needing to reimplement all of the projectile liquid movement code.

            LiquidID_TLmod.Sets.CanBeAbsorbedBy[Type].Remove(ItemID.UltraAbsorbantSponge);

            AddMapEntry(new Color(40, 40, 40));

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
            return ModContent.GetInstance<OilLiquidFall>().Slot;
        }

        public override bool OnPlayerSplash(Player player, bool isEnter)
        {
            SoundEngine.PlaySound(SplashSound, player.position);
            return false;
        }

        public override bool OnNPCSplash(NPC npc, bool isEnter)
        {
            SoundEngine.PlaySound(SplashSound, npc.position);
            return false;
        }

        public override bool OnProjectileSplash(Projectile proj, bool isEnter)
        {
            SoundEngine.PlaySound(SplashSound, proj.position);
            return false;
        }

        public override bool OnItemSplash(Item item, bool isEnter)
        {
            SoundEngine.PlaySound(SplashSound, item.position);
            return false;
        }
    }

    public class OilLiquidFall : ModLiquidFall
    {

    }
}
