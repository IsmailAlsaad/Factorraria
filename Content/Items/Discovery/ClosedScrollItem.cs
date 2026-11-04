using Factorraria.Common.Knowledge;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Factorraria.Content.Items.Discovery
{
    /// <summary>
    /// Found in chests. Right click: the seal breaks and the scroll turns into a Recipe Parchment that
    /// inherits this scroll's pool. No recipe is chosen here; the parchment rolls when it is read.
    /// One ModItem serves every pool: the pool name is per-instance data (see ScrollPoolDefinitions).
    /// </summary>
    public class ClosedScrollItem : ModItem
    {
        // Placeholder art: borrow the vanilla Book icon until we have our own sprite.
        // When ClosedScrollItem.png exists next to this file, delete this override.
        //public override string Texture => "Terraria/Images/Item_" + ItemID.Book;

        /// <summary>Name of the recipe pool this scroll teaches from (see ScrollPoolDefinitions). Null = the whole book.</summary>
        public string PoolName;

        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 28;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Blue;
            Item.value = Item.sellPrice(silver: 50);
        }

        public override bool CanRightClick() => true;

        public override void RightClick(Player player)
        {
            // The scroll never picks a recipe. It hands its pool to the parchment; the parchment rolls when it is read.
            Item parchment = new Item();
            parchment.SetDefaults(ModContent.ItemType<RecipeParchmentItem>());
            ((RecipeParchmentItem)parchment.ModItem).PoolName = PoolName;
            player.QuickSpawnItem(player.GetSource_OpenItem(Type), parchment);
        }

        // Scrolls of different pools never merge into one stack.
        public override bool CanStack(Item source) =>
            source.ModItem is ClosedScrollItem other && other.PoolName == PoolName;

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            string hint = ScrollPools.HintFor(PoolName);
            if (hint == null) return;

            // The "Right click to break the seal..." text is the item's own tooltip (line "Tooltip0"): put the notes right before it.
            var line = new TooltipLine(Mod, "PoolHint", hint);
            int at = tooltips.FindIndex(t => t.Name == "Tooltip0");
            if (at >= 0) tooltips.Insert(at, line);
            else tooltips.Add(line);
        }

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