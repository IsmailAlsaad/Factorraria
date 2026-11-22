using Factorraria.Content.Tiles.Farming.PlanterBoxes;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Harvester
{
    /// <summary>
    /// The Dryad's shop (docs/planter_boxes/ROADMAP.md, Phase 1). Every entry that places a planter box is disabled for good (Disable() is not a condition,
    /// so nothing brings them back), and the Empty Planter Box is added. All other planter boxes are crafted from it.
    ///  - CLIENT ONLY in effect, like the Mechanic shop (MechanicShopGlobalNPC): ModifyShop runs once at load. No condition reads any state, so no packet.
    ///  - Found by what the entry places (createTile), not by item id, so every vanilla planter box is caught, including any added later.
    /// </summary>
    public class DryadShopGlobalNPC : GlobalNPC
    {
        public override void ModifyShop(NPCShop shop)
        {
            if (shop.NpcType != NPCID.Dryad || shop.Name != "Shop")
                return;

            // The empty box takes the slot of the Daybloom Planter Box (the first herb box), so the shop order stays as vanilla has it. If vanilla ever
            // stops selling that box it goes last instead.
            int emptyBox = ModContent.ItemType<EmptyPlanterBoxItem>();
            if (ItemID.Search.TryGetId("DayBloomPlanterBox", out int daybloomBox) && shop.TryGetEntry(daybloomBox, out _))
                shop.InsertBefore(daybloomBox, emptyBox);
            else
                shop.Add(emptyBox);

            foreach (NPCShop.Entry entry in shop.Entries)
            {
                if (entry.Item.createTile == TileID.PlanterBox)
                    entry.Disable();
            }
        }
    }
}