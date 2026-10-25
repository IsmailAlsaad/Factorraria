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

            /// <summary>The PNG, or null if it does not exist (the book page then says the art is missing). Client only.</summary>
            public Texture2D Texture
            {
                get
                {
                    if (!requested)
                    {
                        requested = true;
                        if (ModContent.HasAsset(TexturePath)) asset = ModContent.Request<Texture2D>(TexturePath);
                    }
                    return asset?.Value;
                }
            }
        }

        static readonly List<Entry> entries = new List<Entry>();
        static readonly Dictionary<string, Entry> byKey = new Dictionary<string, Entry>();
        static readonly List<string> problems = new List<string>();

        public static IReadOnlyList<Entry> All => entries;

        /// <summary>Registration mistakes (duplicate names, bad paths), logged by ScrollPools.Validate.</summary>
        public static IReadOnlyList<string> Problems => problems;

        public static string KeyFor(string name) => Machine + "|" + name;

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