using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Factorraria.Content.Items.Conveyors;
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

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.Autohammer
{
    public class AutohammerRecipeRegistry
    {
        public static readonly RecipeBook Book = new();

        public static void BuildRecipes()
        {
            Book.Clear();

            RegisterManualRecipes();
            Book.ImportVanillaRecipes(TileID.Anvils,null,false);

            Book.Rebuild();
        }

        static void RegisterManualRecipes()
        {
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

            Book.Add(new CustomRecipe()
                .WithInput(ModContent.ItemType<ByriteAlloyItem>(), 5)
                .WithInput(ModContent.ItemType<SimpleCircuitItem>(), 3)
                .WithInput(ItemID.SkeletronHand, 1)
                .WithInput(ItemID.Cog, 8)
                .WithOutput(ModContent.ItemType<MechanicalArmItem>(), 1));

            Book.Add(new CustomRecipe()
                .WithInput(ModContent.ItemType<MechanicalArmItem>(), 2)
                .WithInput(ModContent.ItemType<SimpleCircuitItem>(), 10)
                .WithInput(ModContent.ItemType<GoldWireItem>(), 15)
                .WithInput(ItemID.HeavyWorkBench, 1)
                .WithOutput(ModContent.ItemType<MechanicalAssemblerItem>(), 1));

            // register the vanilla recipes for the anvil

            // Skeletron Hand
            Recipe.Create(ItemID.SkeletronHand, 1)
                .AddIngredient(ItemID.Bone, 15)
                .AddIngredient(ItemID.ZombieArm, 1)
                .AddTile(TileID.Anvils)
                .Register();

            // Extractinator
            Recipe.Create(ItemID.Extractinator, 1)
                .AddIngredient(ModContent.ItemType<SimpleCircuitItem>(), 3)
                .AddIngredient(ItemID.Chain, 5)
                .AddIngredient(ItemID.LeadBar, 12)
                .AddTile(TileID.Anvils)
                .Register();
            Recipe.Create(ItemID.Extractinator, 1)
                .AddIngredient(ModContent.ItemType<SimpleCircuitItem>(), 3)
                .AddIngredient(ItemID.Chain, 5)
                .AddIngredient(ItemID.IronBar, 12)
                .AddTile(TileID.Anvils)
                .Register();

            // Priority Conveyors
            Recipe.Create(ModContent.ItemType<ClockwisePriorityConveyor>(), 8)
                .AddIngredient(ModContent.ItemType<SimpleCircuitItem>(), 1)
                .AddIngredient(ItemID.ConveyorBeltRight, 8)
                .AddTile(TileID.Anvils)
                .Register();
            Recipe.Create(ModContent.ItemType<CounterClockwisePriorityConveyor>(), 8)
                .AddIngredient(ModContent.ItemType<SimpleCircuitItem>(), 1)
                .AddIngredient(ItemID.ConveyorBeltLeft, 8)
                .AddTile(TileID.Anvils)
                .Register();

            // Ice Machine
            Recipe.Create(ItemID.IceMachine, 1)
                .AddIngredient(ItemID.TungstenBar, 8)
                .AddIngredient(ItemID.IceBlock, 15)
                .AddIngredient(ItemID.Shiverthorn, 2)
                .AddTile(TileID.Anvils)
                .Register();
            Recipe.Create(ItemID.IceMachine, 1)
                .AddIngredient(ItemID.SilverBar, 8)
                .AddIngredient(ItemID.IceBlock, 15)
                .AddIngredient(ItemID.Shiverthorn, 2)
                .AddTile(TileID.Anvils)
                .Register();

            // Mechanical Hammer (Autohammer)
            Recipe.Create(ItemID.Autohammer, 1)
                .AddIngredient(ItemID.LeadHammer, 1)
                .AddRecipeGroup(RecipeGroupID.IronBar, 10)
                .AddIngredient(ItemID.TinBar, 5)
                .AddIngredient(ItemID.Cog, 3)
                .AddTile(TileID.Anvils)
                .Register();
            Recipe.Create(ItemID.Autohammer, 1)
                .AddIngredient(ItemID.LeadHammer, 1)
                .AddRecipeGroup(RecipeGroupID.IronBar, 10)
                .AddIngredient(ItemID.CopperBar, 5)
                .AddIngredient(ItemID.Cog, 3)
                .AddTile(TileID.Anvils)
                .Register();
            Recipe.Create(ItemID.Autohammer, 1)
                .AddIngredient(ItemID.IronHammer, 1)
                .AddRecipeGroup(RecipeGroupID.IronBar, 10)
                .AddIngredient(ItemID.TinBar, 5)
                .AddIngredient(ItemID.Cog, 3)
                .AddTile(TileID.Anvils)
                .Register();
            Recipe.Create(ItemID.Autohammer, 1)
                .AddIngredient(ItemID.IronHammer, 1)
                .AddRecipeGroup(RecipeGroupID.IronBar, 10)
                .AddIngredient(ItemID.CopperBar, 5)
                .AddIngredient(ItemID.Cog, 3)
                .AddTile(TileID.Anvils)
                .Register();

            // Gel Burner (Steampunk boiler)
            Recipe.Create(ItemID.SteampunkBoiler, 1)
                .AddRecipeGroup(RecipeGroupID.IronBar, 10)
                .AddIngredient(ItemID.Cog, 5)
                .AddIngredient(ItemID.Torch, 3)
                .AddTile(TileID.Anvils)
                .Register();

            // Cog
            Recipe.Create(ItemID.Cog, 5)
                .AddIngredient(ItemID.TinPlating, 1)
                .AddTile(TileID.Anvils)
                .Register();
            Recipe.Create(ItemID.Cog, 5)
                .AddIngredient(ItemID.CopperPlating, 1)
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

            // Solidifier
            Recipe.Create(ItemID.Solidifier, 1)
                .AddRecipeGroup(RecipeGroupID.IronBar, 12)
                .AddIngredient(ItemID.Glass, 6)
                .AddIngredient(ModContent.ItemType<CopperWireItem>(), 8)
                .AddTile(TileID.Anvils)
                .Register();
            Recipe.Create(ItemID.Solidifier, 1)
                .AddRecipeGroup(RecipeGroupID.IronBar, 12)
                .AddIngredient(ItemID.Glass, 6)
                .AddIngredient(ModContent.ItemType<TinWireItem>(), 8)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}
