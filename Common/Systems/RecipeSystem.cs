using Factorraria.Common.Machines;
using Factorraria.Content.Items.Hooks;
using Factorraria.Content.Items.Machines;
using Factorraria.Content.Items.Materials;
using Factorraria.Content.Items.Wires;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Systems
{
    public class RecipeSystem : ModSystem
    {
        public override void AddRecipes()
        {
            // Decay Chamber
            new RecipeBook().DisableVanillaRecipe(ItemID.LesionStation, true);
            Recipe.Create(ItemID.LesionStation, 1)
                .AddIngredient(ItemID.Glass, 15)
                .AddIngredient(ItemID.RottenChunk, 10)
                .AddIngredient(ItemID.ShadowScale, 3)
                .AddTile(TileID.DemonAltar)
                .Register();
            Recipe.Create(ItemID.LesionStation, 1)
                .AddIngredient(ItemID.Glass, 15)
                .AddIngredient(ItemID.Vertebrae, 10)
                .AddIngredient(ItemID.TissueSample, 3)
                .AddTile(TileID.DemonAltar)
                .Register();

            // Deer Thing (replacement)
            new RecipeBook().DisableVanillaRecipe(ItemID.DeerThing, true);
            new RecipeBook().DisableVanillaRecipe(ItemID.DeerThing, true);
            Recipe.Create(ItemID.DeerThing, 1)
                .AddIngredient(ItemID.FlinxFur, 3)
                .AddIngredient(ItemID.EbonstoneBlock, 5)
                .AddIngredient(ItemID.Lens, 1)
                .AddTile(TileID.DemonAltar)
                .Register(); 
            Recipe.Create(ItemID.DeerThing, 1)
                .AddIngredient(ItemID.FlinxFur, 3)
                .AddIngredient(ItemID.CrimstoneBlock, 5)
                .AddIngredient(ItemID.Lens, 1)
                .AddTile(TileID.DemonAltar)
                .Register();
        }
    }
}
