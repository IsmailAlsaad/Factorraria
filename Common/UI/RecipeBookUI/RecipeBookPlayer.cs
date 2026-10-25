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

        /// <summary>Local player only: notes the biomes the player stands in, which reveals the liquids found there (see LiquidBiomeVisits).</summary>
        public override void PostUpdate()
        {
            if (Main.dedServ || Player.whoAmI != Main.myPlayer) return;
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
        }

        public override void LoadData(TagCompound tag)
        {
            ButtonOffset = tag.ContainsKey("buttonDefOffsetX")
                ? new Vector2(tag.GetFloat("buttonDefOffsetX"), tag.GetFloat("buttonDefOffsetY"))
                : null;

            favoriteOverrides.Clear();
            if (tag.ContainsKey("favoriteOn")) foreach (string key in tag.Get<List<string>>("favoriteOn")) favoriteOverrides[key] = true;
            if (tag.ContainsKey("favoriteOff")) foreach (string key in tag.Get<List<string>>("favoriteOff")) favoriteOverrides[key] = false;
            FavoritesVersion++;
        }
    }
}