using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using Terraria.ModLoader;

namespace Factorraria.Common.Knowledge
{
    /// <summary>
    /// Recipe book "art entries": a PNG that a parchment unlocks INSTEAD of a recipe. Registered like a recipe is
    /// (RecipeArtDefinitions.Register), put into scroll pools like a recipe (ScrollPool.AddArt), unlocked and synced
    /// exactly like a recipe (the same string-key sets in RecipeKnowledgeSystem, same parchment packets).
    ///
    /// Name = the PNG's file name without extension. It is the list label in the book and the unique id, so two art
    /// entries cannot share a file name. Catalog key = "Art|&lt;name&gt;".
    /// Art has no state beyond unlocked: it is never "crafted", never faded, has no icon.
    /// Built identically on every side (server included) in PostAddRecipes; textures are only loaded when drawn.
    /// </summary>
    public static class RecipeArtRegistry
    {
        /// <summary>Pseudo machine name art entries are filed under. The book header text is RecipeBook.Machines.Art in the hjson.</summary>
        public const string Machine = "Art";

        public class Entry
        {
            public string Name;          // file name, no extension
            public string TexturePath;   // "Factorraria/.../Name", no extension
            public string Key => KeyFor(Name);

            Asset<Texture2D> asset;
            bool requested;
            bool alphaChecked;

            /// <summary>The PNG, or null if it does not exist (the book page then says the art is missing). Client only.</summary>
            public Texture2D Texture
            {
                get
                {
                    if (!requested)
                    {
                        requested = true;
                        if (ModContent.HasAsset(TexturePath)) asset = ModContent.Request<Texture2D>(TexturePath, AssetRequestMode.ImmediateLoad);
                    }
                    Texture2D tex = asset?.Value;
                    if (tex != null && !alphaChecked)
                    {
                        alphaChecked = true;
                        EnsurePremultiplied(Name, tex);
                    }
                    return tex;
                }
            }
        }

        /// <summary>
        /// The batch blends with premultiplied alpha (One, InverseSourceAlpha), so a texture holding STRAIGHT alpha draws its
        /// semi-transparent pixels too bright, which reads as opaque. If any pixel has a channel above its alpha the data cannot
        /// be premultiplied, so it is converted once here. Logs what it found (Logs\client.log, search "[Art]").
        /// </summary>
        static void EnsurePremultiplied(string name, Texture2D tex)
        {
            try
            {
                var px = new Microsoft.Xna.Framework.Color[tex.Width * tex.Height];
                tex.GetData(px);
                int semi = 0;
                bool straight = false;
                for (int i = 0; i < px.Length; i++)
                {
                    Microsoft.Xna.Framework.Color c = px[i];
                    if (c.A > 0 && c.A < 255) semi++;
                    if (c.R > c.A || c.G > c.A || c.B > c.A) straight = true;
                }

                if (straight)
                {
                    for (int i = 0; i < px.Length; i++)
                    {
                        Microsoft.Xna.Framework.Color c = px[i];
                        px[i] = new Microsoft.Xna.Framework.Color(c.R * c.A / 255, c.G * c.A / 255, c.B * c.A / 255, (int)c.A);
                    }
                    tex.SetData(px);
                }

                ModContent.GetInstance<Factorraria>()?.Logger.Info("[Art] '" + name + "': " + semi + " semi-transparent pixels, " + (straight ? "data was straight alpha, premultiplied now" : "data already premultiplied"));
            }
            catch (System.Exception exception)
            {
                ModContent.GetInstance<Factorraria>()?.Logger.Warn("[Art] could not check the alpha of '" + name + "': " + exception.Message);
            }
        }

        static readonly List<Entry> entries = new List<Entry>();
        static readonly Dictionary<string, Entry> byKey = new Dictionary<string, Entry>(System.StringComparer.OrdinalIgnoreCase);   // "Thunder Cloud" finds "Thunder cloud"
        static readonly List<string> problems = new List<string>();

        public static IReadOnlyList<Entry> All => entries;

        /// <summary>Registration mistakes (duplicate names, bad paths), logged by ScrollPools.Validate.</summary>
        public static IReadOnlyList<string> Problems => problems;

        public static string KeyFor(string name) => Machine + "|" + name;

        /// <summary>The key as REGISTERED (the PNG's real spelling), whatever case the caller typed. Unknown names fall back to KeyFor.</summary>
        public static string CanonicalKey(string name) => byKey.TryGetValue(KeyFor(name), out Entry e) ? e.Key : KeyFor(name);

        public static bool Contains(string catalogKey) => catalogKey != null && byKey.ContainsKey(catalogKey);

        public static bool TryGet(string catalogKey, out Entry entry)
        {
            entry = null;
            return catalogKey != null && byKey.TryGetValue(catalogKey, out entry);
        }

        /// <summary>Registers one PNG by its mod texture path (no extension). Returns its name, or null if it was rejected.</summary>
        public static string Register(string texturePath)
        {
            if (string.IsNullOrWhiteSpace(texturePath))
            {
                problems.Add("[Art] Register was called with an empty path. Ignored.");
                return null;
            }

            string name = texturePath.Substring(texturePath.LastIndexOfAny(new[] { '/', '\\' }) + 1);
            if (name.Length == 0 || name.Contains('|'))
            {
                problems.Add("[Art] '" + texturePath + "' has no usable file name (empty, or it contains '|'). Ignored.");
                return null;
            }

            if (byKey.ContainsKey(KeyFor(name)))
            {
                problems.Add("[Art] '" + name + "' is registered twice. Names are the PNG file names and must be unique. The second one is ignored.");
                return null;
            }

            var entry = new Entry { Name = name, TexturePath = texturePath };
            entries.Add(entry);
            byKey[entry.Key] = entry;
            return name;
        }

        public static void Clear()
        {
            entries.Clear();
            byKey.Clear();
            problems.Clear();
        }
    }
}