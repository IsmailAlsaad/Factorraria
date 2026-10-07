using Factorraria.Common.Knowledge;
using Factorraria.Common.Machines;
using Factorraria.Common.Systems;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Factorraria.Content.Items.Discovery
{
    /// <summary>
    /// Carries a scroll POOL (a name, null = whole book), not a recipe. The recipe is rolled when the parchment is
    /// READ: from the pool's recipes first, then from the whole book. Right click: consumed, that recipe is unlocked
    /// for the whole world. Phase 4 will also open the recipe book on that page.
    /// Throwing an unread parchment away loses nothing: no recipe is reserved before reading.
    /// </summary>
    public class RecipeParchmentItem : ModItem
    {
        // Placeholder art: borrow a vanilla icon until we have our own sprite.
        // When RecipeParchmentItem.png exists next to this file, delete this override.
        //public override string Texture => "Terraria/Images/Item_" + ItemID.Book;

        /// <summary>Name of the scroll pool this parchment rolls from (see ScrollPoolDefinitions). Null = the whole book.</summary>
        public string PoolName;

        bool keepOnThisClick;

        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 28;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.sellPrice(silver: 75);
        }

        public override bool CanRightClick() => true;

        public override void RightClick(Player player)
        {
            // The recipe is chosen now, at read time: pool first, then the whole book.
            if (!ScrollPools.TryRoll(PoolName, Main.rand, out string key))
            {
                keepOnThisClick = true;
                Main.NewText("The ink fades, but there is nothing left in the world for this parchment to teach.", 200, 200, 200);
                return;
            }

            if (!RecipeCatalog.TryResolve(key, out string machine, out RecipeOutputGroup group))
            {
                keepOnThisClick = true;   // should not happen (the roll only returns catalog keys); never lose the item over it
                Main.NewText("The writing on this parchment has faded beyond reading.", 200, 200, 200);
                return;
            }

            RecipeKnowledgeSystem.Unlock(key);

            // Open the recipe book on the page that was just unlocked (client only).
            if (!Main.dedServ) RecipeBookSystem.OpenAt(key);

            // TODO (Phase 6): in multiplayer the roll and the unlock must run on the server.
        }

        public override bool ConsumeItem(Player player)
        {
            bool consume = !keepOnThisClick;
            keepOnThisClick = false;
            return consume;
        }

        // Parchments of different pools never merge into one stack.
        public override bool CanStack(Item source) =>
            source.ModItem is RecipeParchmentItem other && other.PoolName == PoolName;

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            string hint = ScrollPools.HintFor(PoolName);
            if (hint != null) tooltips.Add(new TooltipLine(Mod, "PoolHint", hint));
        }

        // Older Phase 2 parchments saved a "Key" tag; it is ignored, so they become ordinary whole-book parchments.
        public override void SaveData(TagCompound tag)
        {
            if (PoolName != null) tag["Pool"] = PoolName;
        }

        public override void LoadData(TagCompound tag)
        {
            PoolName = tag.ContainsKey("Pool") ? tag.GetString("Pool") : null;
        }

        public override void NetSend(BinaryWriter writer) => writer.Write(PoolName ?? "");

        public override void NetReceive(BinaryReader reader)
        {
            string pool = reader.ReadString();
            PoolName = pool.Length == 0 ? null : pool;
        }
    }
}