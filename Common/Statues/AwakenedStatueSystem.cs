using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace Factorraria.Common.Statues
{
    // One statue currently awakened by an awakener machine. The spawn system keeps the enemy bookkeeping on it.
    public class AwakenedStatue
    {
        public Point16 Origin;          // top-left tile of the statue
        public AwakenedStatueEntry Entry;
        public Point16 OwnerOrigin;     // the awakener machine that claimed it
        public int Width;
        public int Height;

        public int LiveEnemies;
        public int LastLiveEnemies;
        public int SpawnCooldownTicks;

        public Vector2 Center => new Vector2(Origin.X * 16 + Width * 8, Origin.Y * 16 + Height * 8);
    }

    // Holds the awakened statues. There is no periodic scan. A machine rebuilds its own list (RescanFor) only when it
    // switches on/off or is removed, and when a statue is placed or broken every awakening machine within its radius
    // of that statue rebuilds its list (QueueStatueCheck, handled on the next world tick).
    public class AwakenedStatueSystem : ModSystem
    {
        readonly Dictionary<Point16, AwakenedStatue> awakened = new();
        readonly Dictionary<Point16, AwakenedStatue> released = new();
        readonly List<Point16> pendingStatueChecks = new();
        readonly HashSet<Point16> handledThisTick = new();
        readonly HashSet<Point16> droppedOrigins = new();

        public IReadOnlyDictionary<Point16, AwakenedStatue> Awakened => awakened;

        public override void OnWorldLoad()
        {
            awakened.Clear();
            pendingStatueChecks.Clear();
        }

        public override void OnWorldUnload()
        {
            awakened.Clear();
            pendingStatueChecks.Clear();
        }

        // Called when a statue tile is placed or broken (AwakenedStatueGlobalTile). Handled next tick, once the tile has settled.
        public void QueueStatueCheck(int i, int j)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            Point16 origin = TileObjectData.TopLeft(i, j);
            if (!pendingStatueChecks.Contains(origin))
                pendingStatueChecks.Add(origin);
        }

        public override void PostUpdateWorld()
        {
            if (pendingStatueChecks.Count == 0) return;

            handledThisTick.Clear();
            foreach (Point16 statuePos in pendingStatueChecks)
            {
                foreach (TileEntity entity in TileEntity.ByPosition.Values)
                {
                    if (entity is not IStatueAwakener awakener || !awakener.IsAwakening) continue;
                    if (handledThisTick.Contains(awakener.AwakenerOrigin)) continue;

                    int radius = awakener.AwakenRadiusTiles;
                    if (Math.Abs(awakener.AwakenerOrigin.X - statuePos.X) > radius || Math.Abs(awakener.AwakenerOrigin.Y - statuePos.Y) > radius) continue;

                    handledThisTick.Add(awakener.AwakenerOrigin);
                    RescanFor(awakener);
                }
            }

            pendingStatueChecks.Clear();
        }

        // True for ANY tile of an awakened statue's footprint.
        public bool IsAwakenedTile(int i, int j)
        {
            if (awakened.Count == 0) return false;

            Point16 origin = TileObjectData.TopLeft(i, j);
            return awakened.TryGetValue(origin, out AwakenedStatue statue) && IsStillAwakened(statue);
        }

        // Cheap live check, so a mined statue or a machine that just ran dry reverts at once instead of waiting for the rescan.
        public bool IsStillAwakened(AwakenedStatue statue)
        {
            Tile tile = Framing.GetTileSafely(statue.Origin.X, statue.Origin.Y);
            if (!tile.HasTile || tile.TileType != statue.Entry.TileType) return false;

            return TileEntity.ByPosition.TryGetValue(statue.OwnerOrigin, out TileEntity entity)
                && entity is IStatueAwakener awakener && awakener.IsAwakening;
        }

        // Rebuilds one machine's statue list: releases what it owned, claims the free statues in its radius if it is
        // awakening, then lets nearby awakening machines claim anything it just released.
        public void RescanFor(IStatueAwakener awakener)
        {
            Point16 owner = awakener.AwakenerOrigin;

            released.Clear();
            foreach (var pair in awakened)
            {
                if (pair.Value.OwnerOrigin == owner)
                    released.Add(pair.Key, pair.Value);
            }
            foreach (Point16 key in released.Keys)
                awakened.Remove(key);

            awakener.AwakenedStatueCount = 0;
            if (awakener.IsAwakening)
                Claim(awakener);

            int reach = awakener.AwakenRadiusTiles * 2;
            foreach (TileEntity entity in TileEntity.ByPosition.Values)
            {
                if (entity is not IStatueAwakener other || !other.IsAwakening || other.AwakenerOrigin == owner) continue;
                if (Math.Abs(other.AwakenerOrigin.X - owner.X) > reach || Math.Abs(other.AwakenerOrigin.Y - owner.Y) > reach) continue;

                Claim(other);
            }

            if (awakener.AwakenedStatueCount == 0)
                awakener.AwakenedEnemyCount = 0;

            // Statues this machine held that nobody holds now (or that are a different statue now) stopped being awakened,
            // so their enemies turn back into normal ones. A statue a neighbouring machine took over keeps its enemies' tags.
            droppedOrigins.Clear();
            foreach (var pair in released)
            {
                if (!awakened.TryGetValue(pair.Key, out AwakenedStatue current) || current.Entry != pair.Value.Entry)
                    droppedOrigins.Add(pair.Key);
            }
            ClearTagsFor(droppedOrigins);
        }

        // Removes the spawn tag from every enemy that belongs to one of these statue origins (one pass over the NPC array).
        public void ClearTagsFor(HashSet<Point16> origins)
        {
            if (origins.Count == 0) return;

            for (int n = 0; n < Main.maxNPCs; n++)
            {
                NPC npc = Main.npc[n];
                if (!npc.active) continue;

                AwakenedSpawnGlobalNPC tag = npc.GetGlobalNPC<AwakenedSpawnGlobalNPC>();
                if (tag.Tagged && origins.Contains(tag.Origin))
                    tag.Release();
            }
        }

        // Adds every free registered statue within the machine's radius to that machine.
        void Claim(IStatueAwakener awakener)
        {
            var registry = ModContent.GetInstance<AwakenedStatueRegistry>();

            int radius = awakener.AwakenRadiusTiles;
            int minX = Math.Max(1, awakener.AwakenerOrigin.X - radius);
            int maxX = Math.Min(Main.maxTilesX - 2, awakener.AwakenerOrigin.X + radius);
            int minY = Math.Max(1, awakener.AwakenerOrigin.Y - radius);
            int maxY = Math.Min(Main.maxTilesY - 2, awakener.AwakenerOrigin.Y + radius);

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    Tile tile = Main.tile[x, y];
                    if (!tile.HasTile || !registry.IsStatueTile(tile.TileType)) continue;

                    // Statues are keyed by their top-left tile, so two machines can't claim one statue twice.
                    Point16 origin = TileObjectData.TopLeft(x, y);
                    if (origin.X != x || origin.Y != y) continue;
                    if (awakened.ContainsKey(origin)) continue;

                    if (!registry.TryGet(tile.TileType, TileObjectData.GetTileStyle(tile), out AwakenedStatueEntry entry)) continue;

                    TileObjectData data = TileObjectData.GetTileData(tile);
                    if (data == null) continue;

                    // Reuse the object a machine just released (keeps its cooldown and enemy counts).
                    if (!released.TryGetValue(origin, out AwakenedStatue statue) || statue.Entry != entry)
                        statue = new AwakenedStatue { Origin = origin, Entry = entry, Width = data.Width, Height = data.Height };

                    statue.OwnerOrigin = awakener.AwakenerOrigin;
                    awakened.Add(origin, statue);
                    awakener.AwakenedStatueCount++;
                }
            }
        }
    }
}