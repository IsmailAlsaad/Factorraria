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

            foreach (NPCShop.Entry entry in shop.Entries)
            {
                if (entry.Item.createTile == TileID.PlanterBox)
                    entry.Disable();
            }

            shop.Add<EmptyPlanterBoxItem>();
        }
    }
}