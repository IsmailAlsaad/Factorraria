using Factorraria.Common.Knowledge;
using Factorraria.Content.Items.Discovery;
using Terraria;
using Terraria.ModLoader;

namespace Factorraria.Common.Story
{
    /// <summary>
    /// The three Sealed Scrolls the Mechanic hands out (NPC roadmap patch N7.1): one per part of super weapon #1, the missile (drill, tank, thruster).
    /// Each scroll carries its own one-page pool (names and contents live in ScrollPoolDefinitions). Server / single-player only: this puts real items
    /// into the world next to the player, the same way StoryNet gives the Bag. The "once per world" rule is the ScrollsGiven flag, set by the caller.
    /// </summary>
    public static class StoryScrolls
    {
        // Tunable (not const, so hot reload can change it): scrolls handed out per part.
        public static int ScrollsPerPart = 1;

        /// <summary>The pool names of the three part scrolls, in the order drill, tank, thruster.</summary>
        public static string[] PoolNames() => new[]
        {
            ScrollPoolDefinitions.MissileDrillPool,
            ScrollPoolDefinitions.MissileTankPool,
            ScrollPoolDefinitions.MissileThrusterPool
        };

        /// <summary>Server / single-player: spawns the scrolls next to the player (QuickSpawnItem, which broadcasts by itself). Does not touch any flag.</summary>
        public static void Give(Player player, string sourceName)
        {
            if (player == null)
                return;

            int closedScroll = ModContent.ItemType<ClosedScrollItem>();
            foreach (string pool in PoolNames())
            {
                for (int i = 0; i < System.Math.Max(1, ScrollsPerPart); i++)
                {
                    Item scroll = new Item();
                    scroll.SetDefaults(closedScroll);
                    if (scroll.ModItem is ClosedScrollItem closed)
                        closed.PoolName = pool;

                    player.QuickSpawnItem(player.GetSource_Misc(sourceName), scroll, 1);
                }
            }
        }
    }
}