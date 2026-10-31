using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Story
{
    /// <summary>
    /// Hides the wrench vanilla draws behind the Mechanic until she has her backpack (NPC roadmap patch N10.6, polish note 6).
    /// VANILLA (decompiled tModLoader 2026.08.3.0): Main.DrawNPCDirect draws the wrench texture (projectile 582) behind her when type == 124 and
    /// localAI[0] == 0; NPC.UpdateNPC recomputes localAI[0] every update (1 only while her wrench projectile is alive); her attack code also treats
    /// localAI[0] == 1 as "do not throw". So while the flag is off the draw sees localAI[0] == 1 and skips the wrench.
    ///  - AUTHORITY / SYNC: none and no packet. localAI is client-local and never synced, and vanilla rewrites it every update, so this only changes what
    ///    this client draws for one frame. It reads the synced flag MechanicShopUnlocked (via MechanicWrenchSystem.WrenchAllowed); with the flag on nothing is touched.
    ///  - The server never draws, so it never runs this. The attack itself is switched off by MechanicWrenchSystem (AttackType -1).
    /// </summary>
    public class MechanicWrenchGlobalNPC : GlobalNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.Mechanic;

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (Main.dedServ)
                return true;

            if (!MechanicWrenchSystem.WrenchAllowed)
                npc.localAI[0] = 1f;

            return true;
        }
    }
}