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
        public bool FlipHorizontal;        // true for the right-facing style: the sprite and outline are drawn mirrored
        public int[] NpcTypes;             // one or more enemies; a random one is picked on every spawn
        public List<int> HardmodeNpcs = new();   // extra enemies that join the pool once hardmode is on (shared by the left and right entry)

        // Any enemy this statue can spawn (hardmode ones included, so enemies already alive stay valid if hardmode changes).
        public bool Spawns(int npcType) => System.Array.IndexOf(NpcTypes, npcType) >= 0 || HardmodeNpcs.Contains(npcType);

        public int PickNpcType()
        {
            int hardmodeCount = Main.hardMode ? HardmodeNpcs.Count : 0;
            int index = Main.rand.Next(NpcTypes.Length + hardmodeCount);
            return index < NpcTypes.Length ? NpcTypes[index] : HardmodeNpcs[index - NpcTypes.Length];
        }

        // Extra enemies that can spawn only while hardmode is on. Repeating an id makes it more likely, like in the normal list.
        public AwakenedStatueEntry AddHardmodeList(int[] npcTypes)
        {
            if (npcTypes != null) HardmodeNpcs.AddRange(npcTypes);
            return this;
        }
        public Asset<Texture2D> Sprite;    // saturated statue on a padded canvas (see AwakenedStatueDrawSystem.Pad*)
        public Asset<Texture2D> Outline;   // same canvas size as Sprite
        public Vector2 DrawOffset;         // pixels, added to the footprint center when drawing
        public List<AwakenedLootDrop> Loot = new();   // extra drops, rolled for every enemy this statue spawns (shared by the left and right entry)

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

        public int RightFacingStyleOffset = 165;   // the right-facing style of a statue is its left-facing style plus this

        public bool IsStatueTile(int tileType) => statueTileTypes.Contains(tileType);
        public int EntryCount => entries.Count;
        public bool TryGet(int tileType, int style, out AwakenedStatueEntry entry) => entries.TryGetValue((tileType, style), out entry);

        // Pass only the left-facing style. The right-facing style (style + RightFacingStyleOffset) is registered automatically
        // with the same enemies, loot, offset and art, drawn mirrored.
        // Single enemy: always spawns this one. Returns the entry so you can chain .AddLoot(...) (it applies to both).
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

            int rightStyle = style + RightFacingStyleOffset;
            bool addLeft = !entries.ContainsKey((tileType, style));
            bool addRight = !entries.ContainsKey((tileType, rightStyle));

            if (!addLeft)
                Mod.Logger.Warn($"Awakened statue (tile {tileType}): style {style} is already registered, keeping the first entry.");
            if (!addRight)
                Mod.Logger.Warn($"Awakened statue (tile {tileType}): right-facing style {rightStyle} (of style {style}) is already registered, keeping the first entry.");
            if (!addLeft && !addRight)
                return new AwakenedStatueEntry();   // throwaway, so a chained .AddLoot does not touch the existing entry

            Asset<Texture2D> sprite = null;
            if (ModContent.HasAsset(spritePath))
                sprite = ModContent.Request<Texture2D>(spritePath);
            else
                Mod.Logger.Warn($"Awakened statue (tile {tileType}, style {style}): sprite not found at {spritePath}. It will spawn enemies but keep its vanilla look.");

            Asset<Texture2D> outline = null;
            if (ModContent.HasAsset(outlinePath))
                outline = ModContent.Request<Texture2D>(outlinePath);
            else
                Mod.Logger.Warn($"Awakened statue (tile {tileType}, style {style}): outline not found at {outlinePath}.");

            int[] npcs = (int[])npcTypes.Clone();
            var loot = new List<AwakenedLootDrop>();   // one list, referenced by both entries
            var hardmodeNpcs = new List<int>();        // same for the hardmode additions

            AwakenedStatueEntry left = addLeft ? AddEntry(tileType, style, false, npcs, sprite, outline, drawOffset, loot, hardmodeNpcs) : null;
            AwakenedStatueEntry right = addRight ? AddEntry(tileType, rightStyle, true, npcs, sprite, outline, drawOffset, loot, hardmodeNpcs) : null;
            return left ?? right;
        }

        AwakenedStatueEntry AddEntry(int tileType, int style, bool flip, int[] npcs, Asset<Texture2D> sprite, Asset<Texture2D> outline, Vector2 drawOffset, List<AwakenedLootDrop> loot, List<int> hardmodeNpcs)
        {
            var entry = new AwakenedStatueEntry
            {
                TileType = tileType,
                Style = style,
                FlipHorizontal = flip,
                NpcTypes = npcs,
                Sprite = sprite,
                Outline = outline,
                DrawOffset = drawOffset,
                Loot = loot,
                HardmodeNpcs = hardmodeNpcs
            };

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