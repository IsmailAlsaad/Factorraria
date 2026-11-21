using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Drawing;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Harvester
{
    // Puts HerbGrowthRules in place of vanilla's herb growth and makes a herb's looks and drops follow the stage tile.
    //  - GrowAlch: every random update of a herb tile (82, 83, 84) goes to HerbGrowthRules.Step. Other tiles still run vanilla.
    //  - Vanilla shows a MATURE herb (83) as blooming, and gives it seeds, whenever vanilla's own condition holds (Daybloom by day and so on), which would
    //    disagree with our rules. So a mature herb never looks blooming and never drops seeds; only the blooming tile (84) does.
    // GrowAlch only ever runs on the server and in single-player, so the growth needs no packet (see HerbGrowthRules).
    public class HerbGrowthHooks : ModSystem
    {
        public override void Load()
        {
            On_WorldGen.GrowAlch += GrowAlchHook;
            On_WorldGen.IsHarvestableHerbWithSeed += IsHarvestableHerbWithSeedHook;
            On_TileDrawing.IsAlchemyPlantHarvestable += IsAlchemyPlantHarvestableHook;
        }

        public override void Unload()
        {
            On_WorldGen.GrowAlch -= GrowAlchHook;
            On_WorldGen.IsHarvestableHerbWithSeed -= IsHarvestableHerbWithSeedHook;
            On_TileDrawing.IsAlchemyPlantHarvestable -= IsAlchemyPlantHarvestableHook;
        }

        static void GrowAlchHook(On_WorldGen.orig_GrowAlch orig, int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 2))
            {
                orig(x, y);
                return;
            }

            Tile plant = Main.tile[x, y];
            if (!plant.HasTile || (plant.TileType != TileID.ImmatureHerbs && plant.TileType != TileID.MatureHerbs && plant.TileType != TileID.BloomingHerbs))
            {
                orig(x, y);
                return;
            }

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;   // vanilla never grows herbs on a client; the server's tile changes arrive through the normal tile sync

            HerbGrowthRules.Step(x, y);
        }

        static bool IsHarvestableHerbWithSeedHook(On_WorldGen.orig_IsHarvestableHerbWithSeed orig, int type, int style)
        {
            if (type == TileID.MatureHerbs)
                return false;

            return orig(type, style);
        }

        static bool IsAlchemyPlantHarvestableHook(On_TileDrawing.orig_IsAlchemyPlantHarvestable orig, TileDrawing self, int style)
        {
            return false;   // a mature herb is drawn as mature; the blooming look belongs to the blooming tile
        }
    }

    // The flavour line on every herb and herb seed (item names are vanilla's, so the line is inserted right under the name).
    public class HerbTooltipGlobalItem : GlobalItem
    {
        static bool TryGetStyle(int type, out HerbStyle style)
        {
            switch (type)
            {
                case ItemID.Daybloom:
                case ItemID.DaybloomSeeds:
                    style = HerbStyle.Daybloom;
                    return true;
                case ItemID.Moonglow:
                case ItemID.MoonglowSeeds:
                    style = HerbStyle.Moonglow;
                    return true;
                case ItemID.Blinkroot:
                case ItemID.BlinkrootSeeds:
                    style = HerbStyle.Blinkroot;
                    return true;
                case ItemID.Deathweed:
                case ItemID.DeathweedSeeds:
                    style = HerbStyle.Deathweed;
                    return true;
                case ItemID.Waterleaf:
                case ItemID.WaterleafSeeds:
                    style = HerbStyle.Waterleaf;
                    return true;
                case ItemID.Fireblossom:
                case ItemID.FireblossomSeeds:
                    style = HerbStyle.Fireblossom;
                    return true;
                case ItemID.Shiverthorn:
                case ItemID.ShiverthornSeeds:
                    style = HerbStyle.Shiverthorn;
                    return true;
                default:
                    style = default;
                    return false;
            }
        }

        static string Flavour(HerbStyle style) => style switch
        {
            HerbStyle.Daybloom => "A happy flower that loves the sun and forests",
            HerbStyle.Moonglow => "A weeping plant that prefers its natural habitat exposed to moonlight",
            HerbStyle.Blinkroot => "A shy plant that loves hiding below the surface and away from brightness",
            HerbStyle.Waterleaf => "A water thirsty herb adapted to survive the harsh sandy soils, it loves raining clouds",
            HerbStyle.Deathweed => "The only remaining species that can survive the vile landscape",
            HerbStyle.Fireblossom => "A thermophilic species that thrives in scorching heat",
            _ => "A flower adapted to the cold and barren terrain"
        };

        static Color FlavourColor(HerbStyle style) => style switch
        {
            HerbStyle.Daybloom => new Color(255, 221, 64),      // yellow
            HerbStyle.Moonglow => new Color(80, 230, 255),      // cyan
            HerbStyle.Blinkroot => new Color(160, 110, 60),     // brown
            HerbStyle.Waterleaf => new Color(0, 190, 170),      // teal
            HerbStyle.Deathweed => new Color(175, 95, 230),     // purple
            HerbStyle.Fireblossom => new Color(235, 60, 50),    // red
            _ => new Color(205, 228, 255)                       // bluish white
        };

        public override bool AppliesToEntity(Item entity, bool lateInstantiation)
        {
            return TryGetStyle(entity.type, out _);
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
        {
            if (!TryGetStyle(item.type, out HerbStyle style))
                return;

            int insertAt = tooltips.FindLastIndex(t => t.Mod == "Terraria" && t.Name == "ItemName") + 1;
            tooltips.Insert(insertAt, new TooltipLine(Mod, "FactorrariaHerbFlavour", Flavour(style)) { OverrideColor = FlavourColor(style) });
        }
    }
}