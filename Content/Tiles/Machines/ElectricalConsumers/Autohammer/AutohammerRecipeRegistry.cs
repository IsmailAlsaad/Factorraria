using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Factorraria.Content.Items.Materials;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.Autohammer
{
    public class AutohammerRecipeRegistry
    {
        public static readonly RecipeBook Book = new();

        public static void BuildRecipes()
        {
            Book.Clear();

            Book.Add(new CustomRecipe()
                .WithInput(ItemID.GoldBar, 1)
                .WithOutput(ModContent.ItemType<GoldPlateItem>(), 1)
                .TakesTicks(60));

            Book.Add(new CustomRecipe()
                .WithInput(ItemID.CrimtaneBar, 1)
                .WithOutput(ModContent.ItemType<CrimtaneSheetItem>(), 1)
                .TakesTicks(60));

            Book.Add(new CustomRecipe()
                .WithInput(ItemID.DemoniteBar, 1)
                .WithOutput(ModContent.ItemType<DemoniteSheetItem>(), 1)
                .TakesTicks(60));

            Book.Add(new CustomRecipe()
                .WithInput(ItemID.MeteoriteBar, 1)
                .WithOutput(ModContent.ItemType<MeteoritePanelItem>(), 1)
                .TakesTicks(60));

            Book.Add(new CustomRecipe()
                .WithInput(ModContent.ItemType<ByriteAlloyItem>(), 1)
                .WithOutput(ModContent.ItemType<ByriteAlloySlabItem>(), 1)
                .TakesTicks(60));

            Book.Rebuild();
        }
    }
}
