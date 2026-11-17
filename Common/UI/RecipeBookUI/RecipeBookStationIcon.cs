using Factorraria.Common.Knowledge;
using Factorraria.Common.Machines;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.UI.RecipeBookUI
{
    /// <summary>
    /// The little sprite next to a station name (tab header and the station rows of the detail page). Normally an item that places the
    /// station (cycling when there are several). A station with no item (the Demon Altar) uses Ismail's own PNG instead:
    ///   Common/UI/CustomUIElements/StationIcons/StationIcon_&lt;TileID name&gt;.png   (Demon Altar: StationIcon_DemonAltar.png)
    /// Drawn centered, at most `size` pixels (bigger art is scaled down, smaller stays native). No PNG = no sprite, never a crash.
    /// </summary>
    public static class RecipeBookStationIcon
    {
        public static string Folder = "Factorraria/Common/UI/CustomUIElements/StationIcons/StationIcon_";

        static readonly Dictionary<string, Asset<Texture2D>[]> custom = new Dictionary<string, Asset<Texture2D>[]>();
        static readonly Asset<Texture2D>[] NoArt = new Asset<Texture2D>[0];

        /// <summary>
        /// Stations that cycle between several PNGs, by TileID name: the file names (without 'StationIcon_' and '.png') in cycle order.
        /// The Demon Altar and the Crimson Altar are the same tile, so the one tab shows both. Any station not listed uses just its own file.
        /// Not readonly, so hot reload can change it.
        /// </summary>
        public static Dictionary<string, string[]> Variants = new Dictionary<string, string[]>
        {
            { "DemonAltar", new[] { "DemonAltar", "CrimsonAltar" } },
        };

        /// <summary>Every custom PNG that exists for a vanilla station tab name ('VanillaDemonAltar'), in cycle order. Empty if none.</summary>
        public static Asset<Texture2D>[] CustomAll(string stationName)
        {
            if (stationName == null) return NoArt;
            if (custom.TryGetValue(stationName, out Asset<Texture2D>[] known)) return known;

            var found = new List<Asset<Texture2D>>();
            int tile = VanillaStationBooks.TileOf(stationName);
            string tileName = tile >= 0 ? TileID.Search.GetName(tile) : null;
            if (!string.IsNullOrEmpty(tileName))
            {
                string[] files = Variants.TryGetValue(tileName, out string[] list) ? list : new[] { tileName };
                foreach (string file in files)
                    if (ModContent.HasAsset(Folder + file)) found.Add(ModContent.Request<Texture2D>(Folder + file));
            }
            Asset<Texture2D>[] result = found.Count > 0 ? found.ToArray() : NoArt;
            custom[stationName] = result;
            return result;
        }

        /// <summary>The first custom PNG of a station, or null.</summary>
        public static Asset<Texture2D> Custom(string stationName)
        {
            Asset<Texture2D>[] all = CustomAll(stationName);
            return all.Length > 0 ? all[0] : null;
        }

        public static bool Has(string stationName) => RecipeTabDiscovery.ItemsOf(stationName).Length > 0 || Custom(stationName) != null;

        /// <summary>Draws the sprite centered on `center`. Returns false (and draws nothing) when the station has neither an item nor a PNG.</summary>
        public static bool Draw(SpriteBatch sb, string stationName, Vector2 center, float size)
        {
            int[] items = RecipeTabDiscovery.ItemsOf(stationName);
            if (items.Length > 0)
            {
                int type = items[(int)(Main.GlobalTimeWrappedHourly / Math.Max(0.1f, RecipeBookHeaderRow.IconSeconds)) % items.Length];
                RecipeIcon.Draw(sb, RecipeOutputKey.ForItem(type), center, size);
                return true;
            }

            Asset<Texture2D>[] arts = CustomAll(stationName);
            if (arts.Length == 0) return false;
            Texture2D tex = arts[(int)(Main.GlobalTimeWrappedHourly / Math.Max(0.1f, RecipeBookHeaderRow.IconSeconds)) % arts.Length].Value;
            float scale = Math.Min(1f, size / Math.Max(tex.Width, tex.Height));
            sb.Draw(tex, center, null, Color.White, 0f, tex.Size() / 2f, scale, SpriteEffects.None, 0f);
            return true;
        }
    }
}