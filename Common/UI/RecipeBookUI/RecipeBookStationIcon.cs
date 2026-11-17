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

        static readonly Dictionary<string, Asset<Texture2D>> custom = new Dictionary<string, Asset<Texture2D>>();

        /// <summary>The custom PNG for a vanilla station tab name ("VanillaDemonAltar"), or null.</summary>
        public static Asset<Texture2D> Custom(string stationName)
        {
            if (stationName == null) return null;
            if (custom.TryGetValue(stationName, out Asset<Texture2D> known)) return known;

            Asset<Texture2D> found = null;
            int tile = VanillaStationBooks.TileOf(stationName);
            string tileName = tile >= 0 ? TileID.Search.GetName(tile) : null;
            if (!string.IsNullOrEmpty(tileName) && ModContent.HasAsset(Folder + tileName))
                found = ModContent.Request<Texture2D>(Folder + tileName);
            custom[stationName] = found;
            return found;
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

            Asset<Texture2D> art = Custom(stationName);
            if (art == null) return false;
            Texture2D tex = art.Value;
            float scale = Math.Min(1f, size / Math.Max(tex.Width, tex.Height));
            sb.Draw(tex, center, null, Color.White, 0f, tex.Size() / 2f, scale, SpriteEffects.None, 0f);
            return true;
        }
    }
}