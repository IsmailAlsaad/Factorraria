using Factorraria.Common.Knowledge;
using Factorraria.Common.Machines;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Factorraria.Content.Items.Discovery
{
    /// <summary>
    /// Carries exactly one recipe (a RecipeCatalog key). Right click: consumed, the recipe is unlocked for
    /// the whole world. Phase 4 will also open the recipe book on that page.
    /// </summary>
    public class RecipeParchmentItem : ModItem
    {
        // Placeholder art: borrow a vanilla icon until we have our own sprite.
        // When RecipeParchmentItem.png exists next to this file, delete this override.
        //public override string Texture => "Terraria/Images/Item_" + ItemID.Book;

        /// <summary>RecipeCatalog key of the recipe on this parchment. Null only for cheated-in blank parchments.</summary>
        public string CatalogKey;

        bool keepOnThisClick;

        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 28;
            Item.maxStack = 20;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.sellPrice(silver: 75);
        }

        public override bool CanRightClick() => true;

        public override void RightClick(Player player)
        {
            // A blank parchment (e.g. from the creative menu) writes itself on first use.
            if (CatalogKey == null && !RecipeCatalog.TryRollLocked(Main.rand, out CatalogKey))
            {
                keepOnThisClick = true;
                Main.NewText("This parchment is blank and the world has nothing left to unlock.", 200, 200, 200);
                return;
            }

            if (!RecipeCatalog.TryResolve(CatalogKey, out string machine, out RecipeOutputGroup group))
            {
                keepOnThisClick = true;   // recipe was removed in a later version; keep the item, do not lose anything
                Main.NewText("The writing on this parchment has faded beyond reading.", 200, 200, 200);
                return;
            }

            string product = group.Key.DisplayName;
            if (RecipeKnowledgeSystem.Unlock(CatalogKey))
                Main.NewText($"Recipe unlocked: {product} ({machine})", 110, 220, 110);
            else
                Main.NewText($"You already knew how to make {product} in the {machine}.", 200, 200, 200);

            // TODO (Phase 4): open the recipe book UI on this recipe's page.
        }

        public override bool ConsumeItem(Player player)
        {
            bool consume = !keepOnThisClick;
            keepOnThisClick = false;
            return consume;
        }

        // Different recipes must never merge into one stack.
        public override bool CanStack(Item source) =>
            source.ModItem is RecipeParchmentItem other && other.CatalogKey == CatalogKey;

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            string machine = RecipeCatalog.MachineOf(CatalogKey);
            string text = machine == null ? "A blank parchment." : $"Describes a recipe for the {machine}.";
            tooltips.Add(new TooltipLine(Mod, "RecipeHint", text));
        }

        public override void SaveData(TagCompound tag)
        {
            if (CatalogKey != null) tag["Key"] = CatalogKey;
        }

        public override void LoadData(TagCompound tag)
        {
            CatalogKey = tag.ContainsKey("Key") ? tag.GetString("Key") : null;
        }

        public override void NetSend(BinaryWriter writer) => writer.Write(CatalogKey ?? "");

        public override void NetReceive(BinaryReader reader)
        {
            string key = reader.ReadString();
            CatalogKey = key.Length == 0 ? null : key;
        }
    }
}