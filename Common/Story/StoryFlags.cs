using System;

namespace Factorraria.Common.Story
{
    /// <summary>
    /// Every NPC story flag key (NPC roadmap, docs/npcs/ROADMAP.md section 3). A flag is a named world fact, saved with the world and
    /// synced to every client by StorySystem. Add new keys here AND in All(), so /fstory can catch typos.
    /// Dialogue completion flags are generated: StoryFlags.DoneKey("Dryad.RescueMechanic") = "Done.Dryad.RescueMechanic".
    /// </summary>
    public static class StoryFlags
    {
        public static string EyeKillSeen = "EyeKillSeen";                 // the Eye of Cthulhu was killed once (decides the Dryad drop)
        public static string DryadSeen = "DryadSeen";                     // a Dryad has existed in this world at least once
        public static string MechanicPlaced = "MechanicPlaced";           // worldgen placed the hidden Mechanic and the Backpack
        public static string BackpackPlaced = "BackpackPlaced";           // Her Backpack exists in the dungeon
        public static string BackpackTaken = "BackpackTaken";             // a player took the Mechanic's Bag from the Backpack
        public static string MechanicRescued = "MechanicRescued";         // NPC.savedMech turned true
        public static string MechanicShopUnlocked = "MechanicShopUnlocked"; // the bag was given (or she was rescued in the dungeon fork)
        public static string ScrollsGiven = "ScrollsGiven";               // the Mechanic handed out the three super weapon scrolls
        public static string DungeonFork = "DungeonFork";                 // Skeletron fell before the Eye of Cthulhu (the dungeon route)

        public static string DonePrefix = "Done.";

        public static string DoneKey(string dialogueId) => DonePrefix + dialogueId;

        public static string[] All() => new[]
        {
            EyeKillSeen, DryadSeen, MechanicPlaced, BackpackPlaced, BackpackTaken,
            MechanicRescued, MechanicShopUnlocked, ScrollsGiven, DungeonFork
        };

        /// <summary>True for a fixed key above or for any "Done.&lt;id&gt;" key.</summary>
        public static bool IsKnown(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            if (key.StartsWith(DonePrefix, StringComparison.Ordinal))
                return key.Length > DonePrefix.Length;

            foreach (string known in All())
                if (known == key)
                    return true;

            return false;
        }
    }
}