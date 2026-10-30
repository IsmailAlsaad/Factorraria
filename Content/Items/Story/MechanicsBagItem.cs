using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace Factorraria.Content.Items.Story
{
    /// <summary>
    /// The Mechanic's Bag (NPC roadmap patch N4.2): the quest item a player takes from Her Backpack in the dungeon and later gives to the Mechanic
    /// (N5.1). One at a time, shown with the quest tooltip. It does not burn in lava (V9: vanilla only burns white-rarity items, and the
    /// quest rarity is not white; the set below makes it explicit). If the Bag is lost anyway, the bag-loss watcher (RareNpcSystem) lets the Backpack spawn again.
    /// </summary>
    public class MechanicsBagItem : ModItem
    {
        // Until MechanicsBagItem.png exists next to this file the item borrows a vanilla icon, so the mod loads without the art.
        string artPath = "Factorraria/Content/Items/Story/MechanicsBagItem";
        public override string Texture => ModContent.HasAsset(artPath) ? artPath : "Terraria/Images/Item_" + ItemID.Cog;

        // Placeholder texts until the localization pass (N9).
        public override LocalizedText DisplayName => Language.GetOrRegister("Mods.Factorraria.Story.MechanicsBagName", () => "The Mechanic's Bag");
        public override LocalizedText Tooltip => Language.GetOrRegister("Mods.Factorraria.Story.MechanicsBagTooltip", () => "Filled with tools and tangled wire. Someone is missing this.");

        public override void SetStaticDefaults()
        {
            ItemID.Sets.IsLavaImmuneRegardlessOfRarity[Type] = true;
        }

        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 32;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Quest;
            Item.questItem = true;
            Item.value = 0;
        }
    }
}