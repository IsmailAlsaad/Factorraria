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
            Recipe.Create(ModContent.ItemType<CopperWire>(), 10)
                .AddIngredient(ItemID.CopperBar, 1)
                .AddTile(TileID.Anvils)
                .Register();

            // Tin Wire
            Recipe.Create(ModContent.ItemType<TinWire>(), 10)
                .AddIngredient(ItemID.TinBar, 1)
                .AddTile(TileID.Anvils)
                .Register();
        }

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
