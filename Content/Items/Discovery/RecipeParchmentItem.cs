using Factorraria.Common.Knowledge;
using Factorraria.Common.Machines;
using Factorraria.Common.Net;
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

        // While a read request is waiting for the server's answer (multiplayer client), a second click would send a second request.
        public override bool CanRightClick() => !RecipeKnowledgeNet.ReadPending;

        public override void RightClick(Player player)
        {
            // The roll and the unlock belong to the server (roadmap 3.2, finding I2). In single-player the request runs the server
            // code right here and the answer is known before it returns. On a multiplayer client the answer comes later and the
            // parchment is consumed then (RecipeKnowledgeNet.HandleReadResult), so keep it for now. A failed read keeps it too.
            keepOnThisClick = true;
            ParchmentReadResult result = RecipeKnowledgeNet.RequestReadParchment(PoolName);
            if (result == ParchmentReadResult.Read)
                keepOnThisClick = false;
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