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

        public bool IsNew(string key) => ViewedReady && !string.IsNullOrEmpty(key) && !viewed.Contains(key);

        public void MarkViewed(string key)
        {
            if (!string.IsNullOrEmpty(key)) viewed.Add(key);
        }

        /// <summary>Everything the book lists right now counts as viewed (called when the book closes).</summary>
        public void MarkAllViewed()
        {
            if (!ViewedReady) return;
            foreach (string key in RecipeBookNew.BookKeys()) viewed.Add(key);
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
        }

        public override void OnEnterWorld() => ViewedReady = false;

        /// <summary>Local player only: notes the biomes the player stands in, which reveals the liquids found there (see LiquidBiomeVisits).</summary>
        public override void PostUpdate()
        {
            if (Main.dedServ || Player.whoAmI != Main.myPlayer) return;
            RecipeBookNew.TrySeed(this);
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

            if (viewed.Count > 0) tag["viewedKeys"] = new List<string>(viewed);
            if (seededWorlds.Count > 0) tag["viewedWorlds"] = new List<string>(seededWorlds);
        }

        public override void LoadData(TagCompound tag)
        {
            ButtonOffset = tag.ContainsKey("buttonDefOffsetX")
                ? new Vector2(tag.GetFloat("buttonDefOffsetX"), tag.GetFloat("buttonDefOffsetY"))
                : null;

            favoriteOverrides.Clear();
            if (tag.ContainsKey("favoriteOn")) foreach (string key in tag.Get<List<string>>("favoriteOn")) favoriteOverrides[key] = true;
            if (tag.ContainsKey("favoriteOff")) foreach (string key in tag.Get<List<string>>("favoriteOff")) favoriteOverrides[key] = false;

            viewed.Clear();
            seededWorlds.Clear();
            ViewedReady = false;
            if (tag.ContainsKey("viewedKeys")) foreach (string key in tag.Get<List<string>>("viewedKeys")) viewed.Add(key);
            if (tag.ContainsKey("viewedWorlds")) foreach (string key in tag.Get<List<string>>("viewedWorlds")) seededWorlds.Add(key);
            FavoritesVersion++;
        }
    }
}