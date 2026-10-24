using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Statues
{
    // Loot for every Meteor Head (natural, awakened). Wired-statue ones are already blocked by
    // NPCID.Sets.StatueSpawnedDropRarity in MeteorHeadStatueTile, which vanilla checks before any loot is rolled.
    public class MeteorHeadStatueGlobalNPC : GlobalNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.MeteorHead;

        public override void SetDefaults(NPC npc)
        {
            npc.value = 80f;   // coins on death, in copper (100 = 1 silver)
        }

        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            // Common(item, chanceDenominator, min, max): 1 = always, 3 = 1 in 3
            npcLoot.Add(ItemDropRule.Common(ItemID.Meteorite, 4, 1, 4)); // 25% 1-4 Meteorite
        }
    }

    // Vanilla NPCLoot() starts with "type == 23 && Main.hardMode -> return" (23 is the Meteor Head), so in Hardmode no Meteor Head
    // reaches ModifyNPCLoot, PreKill or OnKill (coins, the Meteorite rule and the awakened statue loot list all get skipped).
    // This hook runs the original with Hardmode switched off for that one call. The wired-statue gate (SpawnedFromStatue +
    // StatueSpawnedDropRarity) is a separate check inside the same method and still blocks those. NPCLoot only runs on the server
    // and in single-player (it returns on a multiplayer client), so nothing needs syncing: the dropped items sync like any other.
    public class MeteorHeadHardmodeLootSystem : ModSystem
    {
        public override void Load()
        {
            On_NPC.NPCLoot += AllowHardmodeLoot;
        }

        public override void Unload()
        {
            On_NPC.NPCLoot -= AllowHardmodeLoot;
        }

        void AllowHardmodeLoot(On_NPC.orig_NPCLoot orig, NPC self)
        {
            if (self.type != NPCID.MeteorHead || !Main.hardMode)
            {
                orig(self);
                return;
            }

            Main.hardMode = false;
            try { orig(self); }
            finally { Main.hardMode = true; }
        }
    }
}