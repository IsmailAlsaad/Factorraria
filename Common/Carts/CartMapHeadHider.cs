using Factorraria.Content.Configs;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace Factorraria.Common.Carts
{
    /// <summary>
    /// Hides the vanilla town NPC head icon on the map while the NPC is carried by a cart (the cart's own icon shows it instead).
    /// Done by making TownNPCProfiles.GetHeadIndexSafe report "no head" for a carried NPC, only while the map is up or the inventory is
    /// closed so the housing menu still lists it. Client-only; the carried flag is the one the NPC sync already carries.
    /// </summary>
    public class CartMapHeadHider : ModSystem
    {
        public override void Load()
        {
            if (Main.dedServ)
                return;

            On_TownNPCProfiles.GetHeadIndexSafe += HideCarried;
        }

        public override void Unload()
        {
            On_TownNPCProfiles.GetHeadIndexSafe -= HideCarried;
        }

        static int HideCarried(On_TownNPCProfiles.orig_GetHeadIndexSafe orig, NPC npc)
        {
            if (npc != null && npc.active && (Main.mapFullscreen || !Main.playerInventory)
                && CartMapConfig.Current.HideCarriedNpcHead
                && npc.GetGlobalNPC<CartPassengerGlobalNPC>().IsCarried)
            {
                return -1;
            }

            return orig(npc);
        }
    }
}