using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Story
{
    /// <summary>
    /// The Mechanic has no combat wrench until she has her backpack (NPC roadmap patch N10.4, polish note 6). "Has her backpack" is exactly the world flag
    /// MechanicShopUnlocked (the Bag was handed in; on the Skeletron-first route her rescue sets it).
    ///  - AUTHORITY / SYNC: none and no packet. This ModSystem runs on the server and on every client and only reads the synced story flag, so every side
    ///    agrees within a tick of the flag changing. The server runs the attack decisions and the thrown projectile, the clients the animation.
    ///  - ATTACK: while the flag is off, NPCID.Sets.AttackType[NPCID.Mechanic] is -1 (tModLoader documents -1 as "this NPC won't attack"); when it is on,
    ///    it is the vanilla value (0 = she throws a projectile), captured at PostSetupContent. The vanilla value is put back on unload.
    ///  - ART: none. Her vanilla NPC_124 sheet has no wrench; vanilla draws it in code. It is hidden by MechanicWrenchGlobalNPC.PreDraw (N10.6) while the flag is off.
    /// </summary>
    public class MechanicWrenchSystem : ModSystem
    {
        static bool captured;
        static int vanillaAttackType;

        /// <summary>True when the Mechanic may fight and show her wrench (the Bag was given, or the dungeon fork rescued her).</summary>
        public static bool WrenchAllowed => StorySystem.Get(StoryFlags.MechanicShopUnlocked);

        public override void PostSetupContent()
        {
            vanillaAttackType = NPCID.Sets.AttackType[NPCID.Mechanic];
            captured = true;
        }

        public override void Unload()
        {
            if (captured)
                NPCID.Sets.AttackType[NPCID.Mechanic] = vanillaAttackType;

            captured = false;
        }

        // Before the NPCs run their AI, so this tick's town NPC AI already sees the right value. Server and clients.
        public override void PreUpdateNPCs()
        {
            if (!captured)
                return;

            int wanted = WrenchAllowed ? vanillaAttackType : -1;
            if (NPCID.Sets.AttackType[NPCID.Mechanic] != wanted)
                NPCID.Sets.AttackType[NPCID.Mechanic] = wanted;
        }
    }
}