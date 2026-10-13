using Factorraria.Content.Tiles.Machines.GeneralMachines.MagicMaterializer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace Factorraria.Common.Statues
{
    public class AwakenedLootDrop
    {
        public int ItemType;
        public float Chance;
        public int Min;
        public int Max;
    }

    // One awakenable statue: which statue (tile type + style), what it spawns, and its two hand-drawn sprites.
    public class AwakenedStatueEntry
    {
        public int TileType;
        public int Style;
        public int[] NpcTypes;             // one or more enemies; a random one is picked on every spawn
        public bool Spawns(int npcType) => System.Array.IndexOf(NpcTypes, npcType) >= 0;
        public int PickNpcType() => NpcTypes.Length == 1 ? NpcTypes[0] : NpcTypes[Main.rand.Next(NpcTypes.Length)];
        public Asset<Texture2D> Sprite;    // saturated statue on a padded canvas (see AwakenedStatueDrawSystem.Pad*)
        public Asset<Texture2D> Outline;   // same canvas size as Sprite
        public Vector2 DrawOffset;         // pixels, added to the footprint center when drawing
        public readonly List<AwakenedLootDrop> Loot = new();   // extra drops, rolled for every enemy this statue spawns

        // Extra drop on top of the enemy's normal loot, for every enemy of this statue. chance is 0..1 (0.25 = 25%).
        // A drop gives a random stack from min to max (max defaults to min).
        public AwakenedStatueEntry AddLoot(int itemType, float chance, int min = 1, int max = -1)
        {
            if (max < min) max = min;
            Loot.Add(new AwakenedLootDrop { ItemType = itemType, Chance = chance, Min = min, Max = max });
            return this;
        }
    }

    // The list of awakenable statues. You fill it in AwakenedStatueEntries.Register (Magic Materializer folder).
    public class AwakenedStatueRegistry : ModSystem
    {
        readonly Dictionary<(int, int), AwakenedStatueEntry> entries = new();
        readonly HashSet<int> statueTileTypes = new();

        public bool IsStatueTile(int tileType) => statueTileTypes.Contains(tileType);
        public bool TryGet(int tileType, int style, out AwakenedStatueEntry entry) => entries.TryGetValue((tileType, style), out entry);

        // Single enemy: always spawns this one. Returns the entry so you can chain .AddLoot(...).
        public AwakenedStatueEntry Add(int tileType, int style, int npcType, string spritePath, string outlinePath, Vector2 drawOffset = default)
            => Add(tileType, style, new[] { npcType }, spritePath, outlinePath, drawOffset);

        // Several enemies: a random one from the list is picked on every spawn.
        public AwakenedStatueEntry Add(int tileType, int style, int[] npcTypes, string spritePath, string outlinePath, Vector2 drawOffset = default)
        {
            if (npcTypes == null || npcTypes.Length == 0)
            {
                Mod.Logger.Warn($"Awakened statue (tile {tileType}, style {style}): empty npcTypes list, statue not registered.");
                return new AwakenedStatueEntry();   // throwaway, so a chained .AddLoot does not crash
            }

            var entry = new AwakenedStatueEntry
            {
                TileType = tileType,
                Style = style,
                NpcTypes = (int[])npcTypes.Clone(),
                DrawOffset = drawOffset
            };

            if (ModContent.HasAsset(spritePath))
                entry.Sprite = ModContent.Request<Texture2D>(spritePath);
            else
                Mod.Logger.Warn($"Awakened statue (tile {tileType}, style {style}): sprite not found at {spritePath}. It will spawn enemies but keep its vanilla look.");

            if (ModContent.HasAsset(outlinePath))
                entry.Outline = ModContent.Request<Texture2D>(outlinePath);
            else
                Mod.Logger.Warn($"Awakened statue (tile {tileType}, style {style}): outline not found at {outlinePath}.");

            entries[(tileType, style)] = entry;
            statueTileTypes.Add(tileType);
            return entry;
        }

        public override void PostSetupContent()
        {
            entries.Clear();
            statueTileTypes.Clear();
            AwakenedStatueEntries.Register(this);
        }

        public override void Unload()
        {
            entries.Clear();
            statueTileTypes.Clear();
        }
    }
}