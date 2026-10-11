using Factorraria.Common.Machines;
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
            // Mechanical Hammer (Autohammer)
            Recipe.Create(ItemID.Autohammer, 1)
                .AddRecipeGroup(AnyIronHammerID, 1)
                .AddRecipeGroup(RecipeGroupID.IronBar, 10)
                .AddRecipeGroup(AnyCopperBarID, 5)
                .AddIngredient(ItemID.Cog, 3)
                .AddTile(TileID.Anvils)
                .Register();

            // Gel Burner (Steampunk boiler)
            Recipe.Create(ItemID.SteampunkBoiler, 1)
                .AddRecipeGroup(RecipeGroupID.IronBar, 10)
                .AddRecipeGroup(AnyCopperBarID, 5)
                .AddIngredient(ItemID.Cog, 5)
                .AddIngredient(ItemID.Torch, 3)
                .AddTile(TileID.Anvils)
                .Register();

            // Cog
            Recipe.Create(ItemID.Cog, 5)
                .AddRecipeGroup(AnyTinPlatingID, 1)
                .AddTile(TileID.Anvils)
                .Register();

            // Copper Wire
            Recipe.Create(ModContent.ItemType<CopperWireItem>(), 10)
                .AddIngredient(ItemID.CopperBar, 1)
                .AddTile(TileID.Anvils)
                .Register();

            // Tin Wire
            Recipe.Create(ModContent.ItemType<TinWireItem>(), 10)
                .AddIngredient(ItemID.TinBar, 1)
                .AddTile(TileID.Anvils)
                .Register();

            // Iron Cutter
            Recipe.Create(ModContent.ItemType<IronCutterItem>(), 1)
                .AddIngredient(ItemID.IronBar, 5)
                .AddTile(TileID.Anvils)
                .Register();

            // Lead Cutter
            Recipe.Create(ModContent.ItemType<LeadCutterItem>(), 1)
                .AddIngredient(ItemID.LeadBar, 5)
                .AddTile(TileID.Anvils)
                .Register();

            // Steampunk Minecart
            Recipe.Create(ItemID.SteampunkMinecart, 1)
                .AddIngredient(ItemID.SteampunkBoiler, 1)
                .AddIngredient(ItemID.Minecart, 1)
                .AddTile(TileID.Anvils)
                .Register();

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

            // Simple Circuit
            Recipe.Create(ModContent.ItemType<SimpleCircuitItem>(), 1)
                .AddIngredient(ModContent.ItemType<PureSiliconItem>(), 3)
                .AddIngredient(ModContent.ItemType<TinWireItem>(), 5)
                .AddTile(TileID.HeavyWorkBench)
                .Register();
            Recipe.Create(ModContent.ItemType<SimpleCircuitItem>(), 1)
                .AddIngredient(ModContent.ItemType<PureSiliconItem>(), 3)
                .AddIngredient(ModContent.ItemType<CopperWireItem>(), 5)
                .AddTile(TileID.HeavyWorkBench)
                .Register();

            // Gold Wire
            Recipe.Create(ModContent.ItemType<GoldWireItem>(), 2)
                .AddIngredient(ModContent.ItemType<GoldPlateItem>(), 1)
                .AddTile(TileID.HeavyWorkBench)
                .Register();

            // Steel Screw
            Recipe.Create(ModContent.ItemType<SteelScrewItem>(), 3)
                .AddIngredient(ModContent.ItemType<SteelBarItem>(), 1)
                .AddTile(TileID.HeavyWorkBench)
                .Register();
        }

        // Recipe Groups ID
        static int AnyIronHammerID;
        static int AnyCopperBarID;
        static int AnyTinPlatingID;

        public override void AddRecipeGroups()
        {
            RecipeGroup IronHammerGroup = new RecipeGroup(
                () => "Any Iron Hammer",
                ItemID.IronHammer,
                ItemID.LeadHammer
            );

            AnyIronHammerID = RecipeGroup.RegisterGroup("Factorraria:AnyIronHammer", IronHammerGroup);

            RecipeGroup CopperBarGroup = new RecipeGroup(
                () => "Any Copper Bar",
                ItemID.CopperBar,
                ItemID.LeadBar
            );

            AnyCopperBarID = RecipeGroup.RegisterGroup("Factorraria:AnyCopperBar", CopperBarGroup);

            RecipeGroup TinPlatingGroup = new RecipeGroup(
                () => "Any Tin Plating",
                ItemID.TinPlating,
                ItemID.CopperPlating
            );

            AnyTinPlatingID = RecipeGroup.RegisterGroup("Factorraria:AnyTinPlating", TinPlatingGroup);
        }
    }
}
