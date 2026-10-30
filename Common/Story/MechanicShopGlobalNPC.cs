using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Story
{
    /// <summary>
    /// The Mechanic's shop (NPC roadmap patch N3.1). Every vanilla entry gets the extra condition "MechanicShopUnlocked", so the shop
    /// is empty of vanilla stock until the story unlocks it (N5.1 sets the flag). Two entries are added with no condition:
    /// the Clockwise and Counter Clockwise conveyor belts, which are her default stock.
    ///  - CLIENT ONLY in effect: ModifyShop runs once at load, the conditions are evaluated on the client when the shop opens, and they only read the synced story flag.
    ///  - No packet: the flag travels with the story flag sync (N1.1).
    ///  - Entries are gated, not deleted, so turning the flag on (/fstory flag MechanicShopUnlocked on) brings the vanilla list back.
    /// </summary>
    public class MechanicShopGlobalNPC : GlobalNPC
    {
        public override void ModifyShop(NPCShop shop)
        {
            if (shop.NpcType != NPCID.Mechanic || shop.Name != "Shop")
                return;

            // The key only names the condition (it is not shown in a shop), the predicate reads the synced flag when the shop opens.
            Condition unlocked = new Condition("Mods.Factorraria.Story.MechanicShopUnlocked", () => StorySystem.Get(StoryFlags.MechanicShopUnlocked));

            foreach (NPCShop.Entry entry in shop.Entries)
            {
                int type = entry.Item.type;
                if (type == ItemID.ConveyorBeltLeft || type == ItemID.ConveyorBeltRight)
                    continue;

                entry.AddCondition(unlocked);
            }

            // If vanilla ever sells a belt itself, that entry is left alone (ungated) instead of being added twice.
            if (!shop.TryGetEntry(ItemID.ConveyorBeltLeft, out _))
                shop.Add(ItemID.ConveyorBeltLeft);

            if (!shop.TryGetEntry(ItemID.ConveyorBeltRight, out _))
                shop.Add(ItemID.ConveyorBeltRight);
        }
    }
}