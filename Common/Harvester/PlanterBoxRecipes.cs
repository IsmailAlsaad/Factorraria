using Factorraria.Content.Tiles.Farming.PlanterBoxes;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Harvester
{
    // Every herb planter box is crafted from the Empty Planter Box at a work bench: 5 empty + the extras = 5 boxes (the Mechanical Assembler inherits the
    // work bench recipes, so it makes them too). Called from RecipeSystem.AddRecipes. docs/planter_boxes/ROADMAP.md, Phase 1.
    public static class PlanterBoxRecipes
    {
        // The eight vanilla herb planter boxes (looked up by name through HarvesterBoxKinds.PlanterItemName). Listed here and not enumerated from
        // HarvesterBoxKind on purpose: later kinds (Mushroom, Hallowed, Stone) are our own items with their own recipes.
        static readonly HarvesterBoxKind[] VanillaKinds =
        {
            HarvesterBoxKind.Daybloom, HarvesterBoxKind.Moonglow, HarvesterBoxKind.Corrupt, HarvesterBoxKind.Crimson,
            HarvesterBoxKind.Blinkroot, HarvesterBoxKind.Waterleaf, HarvesterBoxKind.Shiverthorn, HarvesterBoxKind.Fireblossom
        };

        public static void AddRecipes()
        {
            // The boxes are only made from the empty one, so any vanilla recipe for them is switched off first (before ours exist, so ours are never touched).
            foreach (HarvesterBoxKind kind in VanillaKinds)
            {
                if (ItemID.Search.TryGetId(HarvesterBoxKinds.PlanterItemName(kind), out int box))
                    DisableVanillaRecipes(box);
            }

            Make(HarvesterBoxKind.Daybloom, (ItemID.DirtBlock, 2), (ItemID.GrassSeeds, 1));
            Make(HarvesterBoxKind.Moonglow, (ItemID.MudBlock, 2), (ItemID.JungleGrassSeeds, 1));
            Make(HarvesterBoxKind.Blinkroot, (ItemID.DirtBlock, 2));
            Make(HarvesterBoxKind.Waterleaf, (ItemID.SandBlock, 2));
            Make(HarvesterBoxKind.Shiverthorn, (ItemID.SnowBlock, 2));
            Make(HarvesterBoxKind.Fireblossom, (ItemID.AshBlock, 2));

            // Deathweed: two ways each, the evil sand or dirt plus the evil grass seeds.
            Make(HarvesterBoxKind.Corrupt, (ItemID.EbonsandBlock, 2));
            Make(HarvesterBoxKind.Corrupt, (ItemID.DirtBlock, 2), (ItemID.CorruptSeeds, 1));
            Make(HarvesterBoxKind.Crimson, (ItemID.CrimsandBlock, 2));
            Make(HarvesterBoxKind.Crimson, (ItemID.DirtBlock, 2), (ItemID.CrimsonSeeds, 1));
        }

        static void DisableVanillaRecipes(int resultItem)
        {
            for (int i = 0; i < Recipe.numRecipes; i++)
            {
                Recipe recipe = Main.recipe[i];
                if (recipe != null && recipe.createItem.type == resultItem)
                    recipe.DisableRecipe();
            }
        }

        static void Make(HarvesterBoxKind kind, params (int item, int count)[] extras)
        {
            if (!ItemID.Search.TryGetId(HarvesterBoxKinds.PlanterItemName(kind), out int box))
                return;

            Recipe recipe = Recipe.Create(box, 5).AddIngredient(ModContent.ItemType<EmptyPlanterBoxItem>(), 5);
            foreach ((int item, int count) in extras)
                recipe.AddIngredient(item, count);

            recipe.AddTile(TileID.WorkBenches).Register();
        }
    }
}