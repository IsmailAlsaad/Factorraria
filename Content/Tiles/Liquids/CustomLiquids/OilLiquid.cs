using Microsoft.Xna.Framework;
using ModLiquidLib.ID;
using ModLiquidLib.ModLoader;
using Terraria.GameContent.Liquid;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Liquids.CustomLiquids
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
    }
}
