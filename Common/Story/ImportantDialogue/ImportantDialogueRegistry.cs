using System;
using System.Collections.Generic;
using System.Linq;

namespace Factorraria.Common.Story
{
    /// <summary>
    /// Every registered important dialogue, indexed by NPC type. Filled by ImportantDialogueDefinitions.Register() in ImportantDialogueSystem.Load().
    /// A bad registration (empty or duplicate id, no condition) throws, so a typo in the definitions file shows up at mod load.
    /// </summary>
    public static class ImportantDialogueRegistry
    {
        static readonly List<ImportantDialogueEntry> all = new List<ImportantDialogueEntry>();
        static readonly Dictionary<string, ImportantDialogueEntry> byId = new Dictionary<string, ImportantDialogueEntry>(StringComparer.Ordinal);
        static readonly Dictionary<int, List<ImportantDialogueEntry>> byNpc = new Dictionary<int, List<ImportantDialogueEntry>>();
        static readonly ImportantDialogueEntry[] none = new ImportantDialogueEntry[0];

        public static IReadOnlyList<ImportantDialogueEntry> All => all;

        public static void Register(ImportantDialogueEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            if (string.IsNullOrEmpty(entry.Id))
                throw new InvalidOperationException("Important dialogue without an Id");

            if (entry.NpcType <= 0)
                throw new InvalidOperationException("Important dialogue " + entry.Id + " has no NpcType");

            if (entry.Condition == null)
                throw new InvalidOperationException("Important dialogue " + entry.Id + " has no Condition");

            if (StoryFlags.DoneKey(entry.Id).Length > StorySystem.MaxKeyLength)
                throw new InvalidOperationException("Important dialogue id " + entry.Id + " is too long (the Done flag key must fit " + StorySystem.MaxKeyLength + " characters)");

            if (byId.ContainsKey(entry.Id))
                throw new InvalidOperationException("Important dialogue " + entry.Id + " is already registered");

            all.Add(entry);
            byId[entry.Id] = entry;

            if (!byNpc.TryGetValue(entry.NpcType, out List<ImportantDialogueEntry> list))
                byNpc[entry.NpcType] = list = new List<ImportantDialogueEntry>();

            list.Add(entry);

            // Highest Priority first; OrderByDescending is stable, so equal priorities keep registration order.
            byNpc[entry.NpcType] = list.OrderByDescending(e => e.Priority).ToList();
        }

        public static ImportantDialogueEntry ById(string id) => id != null && byId.TryGetValue(id, out ImportantDialogueEntry entry) ? entry : null;

        public static bool HasNpc(int npcType) => byNpc.ContainsKey(npcType);

        /// <summary>The entries of one NPC type, highest Priority first. Never null.</summary>
        public static IReadOnlyList<ImportantDialogueEntry> ForNpc(int npcType) => byNpc.TryGetValue(npcType, out List<ImportantDialogueEntry> list) ? list : (IReadOnlyList<ImportantDialogueEntry>)none;

        public static void Clear()
        {
            all.Clear();
            byId.Clear();
            byNpc.Clear();
        }
    }
}