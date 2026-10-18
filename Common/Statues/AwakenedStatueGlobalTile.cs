using Terraria;
using Terraria.ModLoader;

namespace Factorraria.Common.Statues
{
    // Awakened statues ignore wire signals (no vanilla statue spawn from wiring while they are awakened), and placing or
    // breaking a registered statue tells the awakening machines around it to refresh their statue lists.
    public class AwakenedStatueGlobalTile : GlobalTile
    {
        public override bool PreHitWire(int i, int j, int type)
        {
            if (!ModContent.GetInstance<AwakenedStatueRegistry>().IsStatueTile(type))
                return true;

            return !ModContent.GetInstance<AwakenedStatueSystem>().IsAwakenedTile(i, j);
        }

        // Patch 3.3: placement is handled by AwakenedStatuePlacedConsumer (Common/Net/TilePlacedConsumers.cs) on the server.
        public override void KillTile(int i, int j, int type, ref bool fail, ref bool effectOnly, ref bool noItem)
        {
            if (fail || effectOnly) return;

            if (ModContent.GetInstance<AwakenedStatueRegistry>().IsStatueTile(type))
                ModContent.GetInstance<AwakenedStatueSystem>().QueueStatueCheck(i, j);
        }
    }
}