using Factorraria.Common.Knowledge;
using Factorraria.Common.Machines;
using Factorraria.Common.Net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace Factorraria.Common.UI.RecipeBookUI
{
    /// <summary>
    /// R2: which recipes and tabs are NEW for this character. Everything here is client-only and per character (RecipeBookPlayer keeps the
    /// "viewed" set); nothing is synced. A recipe key or a tab key ("tab|Name") is NEW when it is listed in the book and not viewed yet.
    /// </summary>
    public static class RecipeBookNew
    {
        // Tunables (not const, so hot reload can change them).
        public static float PulseSeconds = 1.8f;
        public static float PulseMin = 0.18f;
        public static float PulseMax = 0.38f;
        public static Color RowTint = new Color(214, 178, 48);   // same gold as the favorite row, but it pulses
        public static int MarkGap = 4;                           // pixels between the end of the name and the "!"
        public static int PlaceholderMarkWidth = 7;              // width reserved for the code-drawn "!" until RecipeBookNew.png exists
        public static Vector2 EntryMarkOffset = Vector2.Zero;    // extra shift of the "!" next to a recipe name, in pixels: +x right, +y down (MarkGap is the base gap)
        public static Vector2 TabMarkOffset = new Vector2(6f,0f);      // the same for the "!" next to a machine tab name

        public static string TabKey(string tab) => "tab|" + tab;

        static RecipeBookPlayer Me => Main.LocalPlayer?.GetModPlayer<RecipeBookPlayer>();

        public static bool IsNew(string key) => Me?.IsNew(key) ?? false;

        /// <summary>A tab is new only if it can be discovered at all (By Hand and Notes never are).</summary>
        public static bool IsTabNew(string tab) => RecipeTabDiscovery.IsDiscoverable(tab) && IsNew(TabKey(tab));

        public static bool AnyNew(IList<string> keys)
        {
            if (keys == null) return false;
            for (int i = 0; i < keys.Count; i++)
                if (IsNew(keys[i])) return true;
            return false;
        }

        /// <summary>Soft gold background that fades in and out, drawn behind a new row.</summary>
        public static Color RowColor()
        {
            float t = (float)(Math.Sin(Main.GlobalTimeWrappedHourly * MathHelper.TwoPi / Math.Max(0.2f, PulseSeconds)) * 0.5 + 0.5);
            return RowTint * MathHelper.Lerp(PulseMin, PulseMax, t);
        }

        public static int MarkWidth => RecipeBookArt.NewMark?.Value.Width ?? PlaceholderMarkWidth;

        /// <summary>Draws the "!" with its left edge at x, centered on centerY. Native size art, or a small code-drawn placeholder.</summary>
        public static void DrawMark(SpriteBatch sb, float x, float centerY, Vector2 offset)
        {
            x += offset.X;
            centerY += offset.Y;
            var art = RecipeBookArt.NewMark;
            if (art != null)
            {
                Texture2D tex = art.Value;
                sb.Draw(tex, new Vector2((int)x, (int)(centerY - tex.Height / 2f)), Color.White);
                return;
            }
            Color c = new Color(190, 40, 30);
            BookDraw.Rect(sb, new Rectangle((int)x + 2, (int)centerY - 7, 3, 9), c);
            BookDraw.Rect(sb, new Rectangle((int)x + 2, (int)centerY + 4, 3, 3), c);
        }

        /// <summary>Every key the book lists right now: recipe keys of discovered tabs that are in the book, plus one tab key per such tab.</summary>
        public static List<string> BookKeys()
        {
            var keys = new List<string>();
            var tabs = new HashSet<string>();
            foreach (RecipeCatalog.Entry cat in RecipeCatalog.EntriesWithArt())
            {
                string tab = RecipeCatalog.TabOf(cat.Machine);
                if (!RecipeTabDiscovery.IsDiscovered(tab)) continue;
                if (!RecipeVisibility.InBook(RecipeVisibility.GetState(cat.Key))) continue;
                keys.Add(cat.Key);
                if (tabs.Add(tab)) keys.Add(TabKey(tab));
            }
            return keys;
        }

        static string WorldKey()
        {
            Guid id = Main.ActiveWorldFileData != null ? Main.ActiveWorldFileData.UniqueId : Guid.Empty;
            return id != Guid.Empty ? id.ToString() : "world" + Main.worldID;
        }

        /// <summary>
        /// Runs every frame from RecipeBookPlayer.PostUpdate (local player) until it succeeds. Waits for the full-state sync (JoinSync.Completed,
        /// always true in single-player), then, the first time this character sees this world, marks everything already in the book as viewed,
        /// so an old save or a freshly joined world does not light up. Later visits to the same world skip the seeding.
        /// </summary>
        public static void TrySeed(RecipeBookPlayer p)
        {
            if (p.ViewedReady || !JoinSync.Completed) return;
            string world = WorldKey();
            p.FinishSeeding(world, p.IsWorldSeeded(world) ? null : BookKeys());
        }
    }
}