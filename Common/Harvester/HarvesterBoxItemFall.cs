using System;
using System.Linq;
using Terraria;
using Terraria.ModLoader;

namespace Factorraria.Common.Harvester
{
    // Real items land on solid-top tiles, and no tile flag can make them ignore one while the player still stands on it. So while a
    // single Item.UpdateItem runs, every harvester box type is made non-solid, then restored. Players, NPCs and projectiles never see it
    // (they are not updated inside this call). Runs on every machine (server and clients), like vanilla item physics, so nothing to sync.
    public class HarvesterBoxItemFall : ModSystem
    {
        int[] boxTypes = Array.Empty<int>();
        bool[] wasSolid = Array.Empty<bool>();
        bool[] wasSolidTop = Array.Empty<bool>();

        public override void Load()
        {
            On_Item.UpdateItem += LetItemsFall;
        }

        public override void PostSetupContent()
        {
            // Boxes register their tile types in SetStaticDefaults, so the registry is complete here.
            boxTypes = HarvesterBoxRegistry.BoxHerbs.Keys.ToArray();
            wasSolid = new bool[boxTypes.Length];
            wasSolidTop = new bool[boxTypes.Length];
        }

        public override void Unload()
        {
            boxTypes = Array.Empty<int>();
            wasSolid = Array.Empty<bool>();
            wasSolidTop = Array.Empty<bool>();
        }

        void LetItemsFall(On_Item.orig_UpdateItem orig, Item self, int i)
        {
            if (boxTypes.Length == 0)
            {
                orig(self, i);
                return;
            }

            for (int k = 0; k < boxTypes.Length; k++)
            {
                int type = boxTypes[k];
                wasSolid[k] = Main.tileSolid[type];
                wasSolidTop[k] = Main.tileSolidTop[type];
                Main.tileSolid[type] = false;
                Main.tileSolidTop[type] = false;
            }

            try
            {
                orig(self, i);
            }
            finally
            {
                for (int k = 0; k < boxTypes.Length; k++)
                {
                    Main.tileSolid[boxTypes[k]] = wasSolid[k];
                    Main.tileSolidTop[boxTypes[k]] = wasSolidTop[k];
                }
            }
        }
    }
}