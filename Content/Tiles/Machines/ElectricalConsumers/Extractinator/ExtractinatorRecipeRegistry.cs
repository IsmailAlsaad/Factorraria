using Factorraria.Content.Items.Materials;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.Extractinator
{
    /// <summary>One possible result of an extraction: Percent chance (0-100), stack size rolled in [Min, Max].</summary>
    public class ExtractinatorDrop
    {
        public int ItemType;
        public double Percent;
        public int Min;
        public int Max;
    }

    /// <summary>
    /// One input item and its weighted drops. A craft rolls ONE number in [0,100) and walks the drops in order:
    /// if the roll lands inside a drop's slice that drop is produced, otherwise the craft produces nothing
    /// (the input is still consumed). Percentages that sum to 100 never miss; 100 on a single drop = guaranteed conversion.
    /// </summary>
    public class ExtractinatorRecipe
    {
        public int Input;
        public readonly List<ExtractinatorDrop> Drops = new();

        public ExtractinatorRecipe Drop(int itemType, double percent, int min = 1, int max = 1)
        {
            if (itemType <= ItemID.None) return this;   // unresolved / placeholder item: skip
            Drops.Add(new ExtractinatorDrop { ItemType = itemType, Percent = percent, Min = min, Max = max });
            return this;
        }

        public double TotalPercent
        {
            get { double sum = 0; foreach (var d in Drops) sum += d.Percent; return sum; }
        }

        public bool TryRoll(out int itemType, out int stack)
        {
            double roll = Main.rand.NextDouble() * 100.0;
            double cumulative = 0;
            foreach (ExtractinatorDrop d in Drops)
            {
                cumulative += d.Percent;
                if (roll < cumulative)
                {
                    itemType = d.ItemType;
                    stack = Main.rand.Next(d.Min, d.Max + 1);
                    return true;
                }
            }
            itemType = 0; stack = 0;
            return false;
        }
    }

    public static class ExtractinatorRecipeRegistry
    {
        static readonly Dictionary<int, ExtractinatorRecipe> recipes = new();
        public static IReadOnlyDictionary<int, ExtractinatorRecipe> Recipes => recipes;

        public static bool TryGet(int inputType, out ExtractinatorRecipe recipe) => recipes.TryGetValue(inputType, out recipe);

        static ExtractinatorRecipe Define(int input)
        {
            var r = new ExtractinatorRecipe { Input = input };
            recipes[input] = r;
            return r;
        }

        public static void BuildRecipes()
        {
            recipes.Clear();

            // ---- Desert Fossil ----
            Define(ItemID.DesertFossil)
                .Drop(ItemID.FossilOre, 10, 1, 7)
                .Drop(ItemID.CopperOre, 4, 1, 16)
                .Drop(ItemID.GoldOre, 4, 1, 16)
                .Drop(ItemID.AmberMosquito, 0.01, 1, 1)
                .Drop(ItemID.Topaz, 0.5, 1, 8)
                .Drop(ItemID.Amber, 0.5, 1, 8)
                .Drop(ItemID.CopperCoin, 50, 1, 100)
                .Drop(ItemID.GoldCoin, 0.1, 1, 10);

            // ---- Silt ----
            Define(ItemID.SiltBlock)
                .Drop(ModContent.ItemType<SiliconOreItem>(), 4, 1, 16)
                .Drop(ItemID.TinOre, 4, 1, 16)
                .Drop(ItemID.IronOre, 4, 1, 16)
                .Drop(ItemID.SilverOre, 4, 1, 16)
                .Drop(ItemID.Ruby, 0.5, 1, 4)
                .Drop(ItemID.Diamond, 0.5, 1, 4)
                .Drop(ItemID.Amethyst, 0.5, 1, 8)
                .Drop(ItemID.Sapphire, 0.5, 1, 8)
                .Drop(ItemID.Emerald, 0.5, 1, 4);

            // ---- Slush ----
            Define(ItemID.SlushBlock)
                .Drop(ItemID.LeadOre, 4, 1, 16)
                .Drop(ItemID.TungstenOre, 4, 1, 16)
                .Drop(ItemID.PlatinumOre, 4, 1, 16)
                .Drop(ItemID.SilverCoin, 2, 1, 100)
                .Drop(ItemID.PlatinumCoin, 0.01, 1, 1);

            // ---- Vanilla conversions kept as they are (terraria.wiki.gg/wiki/Extractinator) ----

            // Glowing mosses -> one of the five regular mosses (20% each)
            foreach (int glowingMoss in new[] { ItemID.LavaMoss, ItemID.ArgonMoss, ItemID.KryptonMoss, ItemID.XenonMoss, ItemID.VioletMoss, ItemID.RainbowMoss })
            {
                Define(glowingMoss)
                    .Drop(ItemID.GreenMoss, 20)
                    .Drop(ItemID.BrownMoss, 20)
                    .Drop(ItemID.RedMoss, 20)
                    .Drop(ItemID.BlueMoss, 20)
                    .Drop(ItemID.PurpleMoss, 20);
            }

            // Fishing junk -> low level bait
            foreach (int junk in new[] { ItemID.OldShoe, ItemID.FishingSeaweed, ItemID.TinCan })
            {
                Define(junk)
                    .Drop(ItemID.ApprenticeBait, 75)
                    .Drop(ItemID.Snail, 16.67)
                    .Drop(ItemID.Worm, 5.56)
                    .Drop(ItemID.JourneymanBait, 2.78);
            }

            // Poo -> dirt (+ rare grass seeds)
            Define(ItemID.PoopBlock)
                .Drop(ItemID.DirtBlock, 98)
                .Drop(ItemID.GrassSeeds, 0.67)
                .Drop(ItemID.JungleGrassSeeds, 0.67)
                .Drop(ItemID.MushroomGrassSeeds, 0.67);

            // Guaranteed conversions
            Define(ItemID.Hive).Drop(ItemID.HoneyBlock, 100);
            Define(ItemID.Obsidian).Drop(ItemID.SandBlock, 100);
            Define(ItemID.ShellPileBlock).Drop(ItemID.SandBlock, 100);

            recipes.Remove(ItemID.None);   // an unresolved Named() input above
        }
    }
}