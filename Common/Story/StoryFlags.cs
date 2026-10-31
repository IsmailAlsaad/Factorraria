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
        public static string BackpackTaken = "BackpackTaken";             // a player took the Mechanic's Bag from Her Backpack (cleared again by the bag-loss watcher while the Bag is lost, so the Backpack can spawn again)
        public static string MechanicRescued = "MechanicRescued";         // NPC.savedMech turned true
        public static string MechanicShopUnlocked = "MechanicShopUnlocked"; // the bag was given (or she was rescued in the dungeon fork)
        public static string ScrollsGiven = "ScrollsGiven";               // the Mechanic handed out the three super weapon scrolls
        public static string DungeonFork = "DungeonFork";                 // Skeletron fell before the Eye of Cthulhu (the dungeon route)
        public static string DryadFromEye = "DryadFromEye";               // the Dryad arrived because OnEyeKilled spawned her (first Eye kill, none before; patch N10.1); only then does Dryad dialogue 1 show
        public static string DryadTalked = "DryadTalked";                 // a player had a real conversation with a Dryad (patch N8.2); the Mechanic hands out the scrolls only after this
        public static string TestDialogueExit = "TestDialogueExit";       // TEMPORARY (patch N2.1): the exit switch of the Test.Guide dialogue; removed in N9

        public static string DonePrefix = "Done.";

        public static string DoneKey(string dialogueId) => DonePrefix + dialogueId;

        public static string[] All() => new[]
        {
            EyeKillSeen, DryadSeen, BackpackTaken,
            MechanicRescued, MechanicShopUnlocked, ScrollsGiven, DungeonFork, DryadFromEye, DryadTalked, TestDialogueExit
        };

        /// <summary>
        /// Cleans a typed key (drops spaces, control and invisible characters, which chat copy/paste can add) and matches it
        /// ignoring case. Returns the canonical key, or null when it is unknown. "done.x" becomes "Done.x".
        /// </summary>
        public static string Resolve(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (char c in key)
            {
                if (char.IsWhiteSpace(c) || char.IsControl(c) || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format)
                    continue;
                sb.Append(c);
            }

            string clean = sb.ToString();
            if (clean.Length == 0)
                return null;

            if (clean.StartsWith(DonePrefix, StringComparison.OrdinalIgnoreCase))
                return clean.Length > DonePrefix.Length ? DonePrefix + clean.Substring(DonePrefix.Length) : null;

            foreach (string known in All())
                if (string.Equals(known, clean, StringComparison.OrdinalIgnoreCase))
                    return known;

            return null;
        }

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