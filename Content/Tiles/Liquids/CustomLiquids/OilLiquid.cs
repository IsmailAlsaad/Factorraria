using Microsoft.Xna.Framework;
using ModLiquidLib.ModLoader;
using Terraria.ID;

namespace Factorraria.Content.Tiles.Liquids.CustomLiquids
{
    public class OilLiquid : ModLiquid
    {
        public override void SetStaticDefaults()
        {
            // Shown on the minimap when hovering over a pool of this liquid.
            AddMapEntry(new Color(60, 45, 30));

            // Slows things down passing through it — similar viscosity to Honey.
            PlayerMovementMultiplier = 0.6f;
            NPCMovementMultiplierDefault = 0.6f;
            ProjectileMovementMultiplier = 0.6f;

            // Honey's fall delay is 10 (the max allowed) — oil is viscous too.
            FallDelay = 10;

            SlopeOpacity = 0.6f;
            WaterRippleMultiplier = 0.3f;

            // If this mod is ever removed, existing world tiles fall back to Water
            // instead of leaving an invalid/undefined liquid type behind.
            VanillaFallbackOnModDeletion = (ushort)LiquidID.Water;
        }
    }
}
