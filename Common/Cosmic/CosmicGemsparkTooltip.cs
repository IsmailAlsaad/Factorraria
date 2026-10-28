using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace Factorraria.Common.Cosmic
{
    // C9b: every vanilla gemspark block item tells the player which cosmic wavelength it absorbs (and that absorbing it feeds the next
    // step of the cascade, which is what makes a stronger ionization). Client side text only; nothing here touches the simulation.
    //
    // Which items: the ones whose tile is one of the 14 gemspark tiles (roadmap V1: 255 to 261 are the offline ones and 262 to 268 the lit
    // ones, in the same order, so an offline tile + 7 is its lit twin). The color is then found with the same lookup the simulation uses
    // (CosmicColors.TryFromGemspark), so the tooltip can never disagree with what the block really absorbs.
    public class CosmicGemsparkTooltip : GlobalItem
    {
        const int FirstGemsparkTile = 255;
        const int FirstLitGemsparkTile = 262;
        const int LastGemsparkTile = 268;

        // createTile is only set once the item's defaults have run, so this asks for late instantiation.
        public override bool AppliesToEntity(Item entity, bool lateInstantiation)
        {
            return lateInstantiation && entity.createTile >= FirstGemsparkTile && entity.createTile <= LastGemsparkTile;
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
        {
            int tile = item.createTile;
            if (tile < FirstLitGemsparkTile)
                tile += FirstLitGemsparkTile - FirstGemsparkTile;

            if (!CosmicColors.TryFromGemspark(tile, out CosmicColor color))
                return;

            string wavelength = color.ToString().ToLowerInvariant();
            tooltips.Add(new TooltipLine(Mod, "CosmicAbsorbs", "Absorbs " + wavelength + " wavelength particles to yield higher level ionization")
            {
                OverrideColor = CosmicColors.DrawColor(color)
            });
        }
    }
}