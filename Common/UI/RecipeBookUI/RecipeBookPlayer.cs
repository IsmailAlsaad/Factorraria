using Factorraria.Common.Knowledge;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Factorraria.Common.UI.RecipeBookUI
{
    /// <summary>Remembers where this character dragged the recipe book button (offset from the defense icon). Null = default placement.</summary>
    public class RecipeBookPlayer : ModPlayer
    {
        public Vector2? ButtonOffset;

        // Favorite recipes (catalog keys). Per character and client-only: a favorite is a personal UI preference, so nothing is synced.
        // Only explicit choices are stored (true = starred, false = unstarred on purpose). With no entry, a recipe is a favorite
        // exactly when a parchment unlocked it in this world, so a recipe the player unstarred stays unstarred when later unlocked.
        readonly Dictionary<string, bool> favoriteOverrides = new();

        /// <summary>Bumped on every toggle. RecipeBookState compares it each frame to know when to re-sort the list.</summary>
        public int FavoritesVersion { get; private set; }

        // R7: the left page's layout (false = Rows, true = Grid). Per character and client-only: a personal UI choice, nothing is synced.
        public bool GridMode { get; private set; }

        public void SetGridMode(bool grid) => GridMode = grid;

        public bool IsFavorite(string catalogKey)
        {
            if (string.IsNullOrEmpty(catalogKey)) return false;
            return favoriteOverrides.TryGetValue(catalogKey, out bool on) ? on : RecipeKnowledgeSystem.IsUnlocked(catalogKey);
        }

        public void ToggleFavorite(string catalogKey)
        {
            if (string.IsNullOrEmpty(catalogKey)) return;
            favoriteOverrides[catalogKey] = !IsFavorite(catalogKey);
            FavoritesVersion++;
        }

        // R2: keys of recipes and tabs ("tab|Name") this character has already looked at. Per character and client-only, nothing is synced.
        // A listed key that is not in here is NEW (highlight + "!"). Seeded once per world, see RecipeBookNew.TrySeed.
        readonly HashSet<string> viewed = new();
        readonly HashSet<string> seededWorlds = new();

        /// <summary>False until the seeding for the current world ran. IsNew is always false before that, so nothing flashes while the join sync is still arriving.</summary>
        public bool ViewedReady { get; private set; }

        /// <summary>Bumped whenever the viewed set changes (R6 reads it to know when to look again at what is new).</summary>
        public int ViewedVersion { get; private set; }

        public bool IsNew(string key) => ViewedReady && !string.IsNullOrEmpty(key) && !viewed.Contains(key);

        public void MarkViewed(string key)
        {
            if (!string.IsNullOrEmpty(key) && viewed.Add(key)) ViewedVersion++;
        }

        /// <summary>Everything the book lists right now counts as viewed (called when the book closes).</summary>
        public void MarkAllViewed()
        {
            if (!ViewedReady) return;
            foreach (string key in RecipeBookNew.BookKeys()) viewed.Add(key);
            ViewedVersion++;
        }

        public bool IsWorldSeeded(string worldKey) => seededWorlds.Contains(worldKey);

        /// <summary>keys == null: this world was seeded on an earlier visit. Otherwise those keys become viewed and the world is remembered.</summary>
        public void FinishSeeding(string worldKey, List<string> keys)
        {
            if (keys != null)
            {
                foreach (string key in keys) viewed.Add(key);
                seededWorlds.Add(worldKey);
            }
            ViewedReady = true;
            ViewedVersion++;
        }

        public override void OnEnterWorld()
        {
            ViewedReady = false;
            rankWorld = null;   // the unlock sequence is per world: rebuild the lookup for this one
        }

        // R1.8: the order the tabs appeared in the book, per world. Per character and client-only like the viewed set, nothing is synced:
        // the book is a personal UI and every player sees the tabs in the order THEY found them. A character that already has tabs gets them
        // in catalog order the first time (the real order was never recorded), everything found afterwards is appended.
        readonly Dictionary<string, List<string>> tabOrders = new();
        Dictionary<string, int> ranks = new();
        string rankWorld;

        Dictionary<string, int> Ranks()
        {
            string world = RecipeBookNew.WorldKey();
            if (rankWorld == world) return ranks;
            rankWorld = world;
            ranks = new Dictionary<string, int>();
            if (tabOrders.TryGetValue(world, out List<string> order))
                for (int i = 0; i < order.Count; i++) ranks[order[i]] = i;
            return ranks;
        }

        /// <summary>Position of a tab in this world's unlock sequence (smaller = found earlier), int.MaxValue if not recorded yet. A merged tab takes the earliest position of the stations that were its own tabs before the merge.</summary>
        public int TabRank(string tab)
        {
            if (string.IsNullOrEmpty(tab)) return int.MaxValue;
            Dictionary<string, int> r = Ranks();
            int best = r.TryGetValue(tab, out int own) ? own : int.MaxValue;
            foreach (string station in RecipeCatalog.SplitStationsOf(tab))
                if (r.TryGetValue(station, out int rank) && rank < best) best = rank;
            return best;
        }

        public bool RecordTab(string tab)
        {
            Dictionary<string, int> r = Ranks();
            if (string.IsNullOrEmpty(tab) || r.ContainsKey(tab)) return false;
            if (!tabOrders.TryGetValue(rankWorld, out List<string> order)) tabOrders[rankWorld] = order = new List<string>();
            r[tab] = order.Count;
            order.Add(tab);
            return true;
        }

        /// <summary>Local player only: notes the biomes the player stands in, which reveals the liquids found there (see LiquidBiomeVisits).</summary>
        public override void PostUpdate()
        {
            if (Main.dedServ || Player.whoAmI != Main.myPlayer) return;
            RecipeBookNew.TrySeed(this);
            if (Main.GameUpdateCount % 60 == 0) RecipeBookNew.PollTabOrder(this);
            if (Main.GameUpdateCount % (uint)System.Math.Max(1, LiquidBiomeVisits.PollTicks) != 0) return;
            LiquidBiomeVisits.Poll(Player);
        }

        public override void SaveData(TagCompound tag)
        {
            if (ButtonOffset.HasValue)
            {
                // New key names: the old "buttonColOffset*" values were relative to the accessory column top and would be wrong now.
                tag["buttonDefOffsetX"] = ButtonOffset.Value.X;
                tag["buttonDefOffsetY"] = ButtonOffset.Value.Y;
            }

            var starred = new List<string>();
            var unstarred = new List<string>();
            foreach (KeyValuePair<string, bool> kv in favoriteOverrides) (kv.Value ? starred : unstarred).Add(kv.Key);
            if (starred.Count > 0) tag["favoriteOn"] = starred;
            if (unstarred.Count > 0) tag["favoriteOff"] = unstarred;

            if (GridMode) tag["bookGridMode"] = true;

            if (viewed.Count > 0) tag["viewedKeys"] = new List<string>(viewed);
            if (seededWorlds.Count > 0) tag["viewedWorlds"] = new List<string>(seededWorlds);

            var orders = new List<TagCompound>();
            foreach (KeyValuePair<string, List<string>> kv in tabOrders)
                if (kv.Value.Count > 0) orders.Add(new TagCompound { ["world"] = kv.Key, ["tabs"] = new List<string>(kv.Value) });
            if (orders.Count > 0) tag["tabOrders"] = orders;
        }

        public override void LoadData(TagCompound tag)
        {
            ButtonOffset = tag.ContainsKey("buttonDefOffsetX")
                ? new Vector2(tag.GetFloat("buttonDefOffsetX"), tag.GetFloat("buttonDefOffsetY"))
                : null;

            favoriteOverrides.Clear();
            if (tag.ContainsKey("favoriteOn")) foreach (string key in tag.Get<List<string>>("favoriteOn")) favoriteOverrides[key] = true;
            if (tag.ContainsKey("favoriteOff")) foreach (string key in tag.Get<List<string>>("favoriteOff")) favoriteOverrides[key] = false;

            GridMode = tag.ContainsKey("bookGridMode") && tag.GetBool("bookGridMode");

            viewed.Clear();
            seededWorlds.Clear();
            ViewedReady = false;
            if (tag.ContainsKey("viewedKeys")) foreach (string key in tag.Get<List<string>>("viewedKeys")) viewed.Add(key);
            if (tag.ContainsKey("viewedWorlds")) foreach (string key in tag.Get<List<string>>("viewedWorlds")) seededWorlds.Add(key);
            tabOrders.Clear();
            rankWorld = null;
            if (tag.ContainsKey("tabOrders"))
                foreach (TagCompound entry in tag.GetList<TagCompound>("tabOrders"))
                    tabOrders[entry.GetString("world")] = new List<string>(entry.GetList<string>("tabs"));
            FavoritesVersion++;
        }
    }
}