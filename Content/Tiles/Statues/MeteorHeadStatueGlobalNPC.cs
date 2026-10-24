using Factorraria.Common.Statues;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Statues
{
    // Meteor Heads that come from the Meteor Head Statue (wired spawn or awakened by a Magic Materializer) die without any loot:
    // no coins and no meteorite (PreKill returning false skips the whole loot step, the same way vanilla skips it for some
    // statue-spawned enemies). Natural Meteor Heads, and ones released from an awakened statue, drop their normal loot.
    // Server / singleplayer only: loot is decided there, so none of this is synced.
    public class MeteorHeadStatueGlobalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public bool FromStatue;   // set by MeteorHeadStatueTile.HitWire

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.MeteorHead;

        public override bool PreKill(NPC npc)
        {
            if (FromStatue) return false;

            AwakenedSpawnGlobalNPC tag = npc.GetGlobalNPC<AwakenedSpawnGlobalNPC>();
            bool fromAwakenedStatue = tag.Tagged && tag.Entry != null && tag.Entry.TileType == ModContent.TileType<MeteorHeadStatueTile>();
            return !fromAwakenedStatue;
        }
    }
}