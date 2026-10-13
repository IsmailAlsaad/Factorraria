using Factorraria.Content.Tiles.Machines.GeneralMachines.MagicMaterializer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using Terraria.ModLoader;

namespace Factorraria.Common.Statues
{
    // One awakenable statue: which statue (tile type + style), what it spawns, and its two hand-drawn sprites.
    public class AwakenedStatueEntry
    {
        public int TileType;
        public int Style;
        public int NpcType;
        public Asset<Texture2D> Sprite;    // saturated statue, same pixel size as the statue footprint
        public Asset<Texture2D> Outline;   // same canvas size as Sprite
        public Vector2 DrawOffset;         // pixels, added to the footprint center when drawing
    }

    // The list of awakenable statues. You fill it in AwakenedStatueEntries.Register (Magic Materializer folder).
    public class AwakenedStatueRegistry : ModSystem
    {
        readonly Dictionary<(int, int), AwakenedStatueEntry> entries = new();
        readonly HashSet<int> statueTileTypes = new();

        public bool IsStatueTile(int tileType) => statueTileTypes.Contains(tileType);
        public bool TryGet(int tileType, int style, out AwakenedStatueEntry entry) => entries.TryGetValue((tileType, style), out entry);

        public void Add(int tileType, int style, int npcType, string spritePath, string outlinePath, Vector2 drawOffset = default)
        {
            var entry = new AwakenedStatueEntry
            {
                TileType = tileType,
                Style = style,
                NpcType = npcType,
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