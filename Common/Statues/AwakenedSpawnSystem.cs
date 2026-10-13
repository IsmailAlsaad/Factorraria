using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Statues
{
    // Keeps up to MaxEnemiesPerStatue live enemies for every awakened statue. Runs on the host / singleplayer, with no
    // player-distance check: a farm keeps producing while you are far away.
    public class AwakenedSpawnSystem : ModSystem
    {
        public int MaxEnemiesPerStatue = 3;
        public int SpawnPassTicks = 60;       // how often statues are checked (also the pace of the initial fill)
        public int RefillDelayTicks = 180;    // wait after an enemy dies before its replacement
        public int NpcSlotMargin = 10;        // stop spawning when this close to the NPC cap
        public Vector2 SpawnOffset = new Vector2(0f, 32f);   // pixels added to every spawn position (+x right, +y down)

        int passTimer;
        readonly Dictionary<Point16, int> liveCounts = new();
        readonly Dictionary<Point16, int> ownerTotals = new();

        // Where to try placing a spawn, in tiles relative to the statue's top-center (feet position). First free one wins.
        readonly Point[] spawnOffsets = { new Point(0, 0), new Point(-2, 0), new Point(2, 0), new Point(0, -2), new Point(0, -4) };

        public override void PostUpdateNPCs()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            passTimer++;
            if (passTimer < SpawnPassTicks) return;
            passTimer = 0;

            var statues = ModContent.GetInstance<AwakenedStatueSystem>();

            // One pass over the NPC array: count each statue's live enemies and the total of active NPCs.
            liveCounts.Clear();
            int activeNpcs = 0;
            for (int n = 0; n < Main.maxNPCs; n++)
            {
                NPC npc = Main.npc[n];
                if (!npc.active) continue;
                activeNpcs++;

                AwakenedSpawnGlobalNPC tag = npc.GetGlobalNPC<AwakenedSpawnGlobalNPC>();
                if (!tag.Tagged) continue;
                if (!statues.Awakened.TryGetValue(tag.Origin, out AwakenedStatue owner) || !owner.Entry.Spawns(npc.type)) continue;

                liveCounts[tag.Origin] = liveCounts.GetValueOrDefault(tag.Origin) + 1;
            }

            ownerTotals.Clear();
            foreach (AwakenedStatue statue in statues.Awakened.Values)
            {
                statue.LiveEnemies = liveCounts.GetValueOrDefault(statue.Origin);
                statue.SpawnCooldownTicks = Math.Max(0, statue.SpawnCooldownTicks - SpawnPassTicks);

                // An enemy died or despawned since the last pass: wait before the replacement.
                if (statue.LiveEnemies < statue.LastLiveEnemies)
                    statue.SpawnCooldownTicks = Math.Max(statue.SpawnCooldownTicks, RefillDelayTicks);
                statue.LastLiveEnemies = statue.LiveEnemies;

                if (statue.LiveEnemies < MaxEnemiesPerStatue
                    && statue.SpawnCooldownTicks <= 0
                    && activeNpcs < Main.maxNPCs - NpcSlotMargin
                    && statues.IsStillAwakened(statue)
                    && TrySpawn(statue))
                {
                    statue.LiveEnemies++;
                    statue.LastLiveEnemies = statue.LiveEnemies;
                    activeNpcs++;
                }

                ownerTotals[statue.OwnerOrigin] = ownerTotals.GetValueOrDefault(statue.OwnerOrigin) + statue.LiveEnemies;
            }

            foreach (var pair in ownerTotals)
            {
                if (TileEntity.ByPosition.TryGetValue(pair.Key, out TileEntity entity) && entity is IStatueAwakener awakener)
                    awakener.AwakenedEnemyCount = pair.Value;
            }
        }

        bool TrySpawn(AwakenedStatue statue)
        {
            int npcType = statue.Entry.PickNpcType();
            if (!ContentSamples.NpcsByNetId.TryGetValue(npcType, out NPC sample)) return false;

            foreach (Point offset in spawnOffsets)
            {
                Vector2 feet = new Vector2(statue.Origin.X * 16 + statue.Width * 8 + offset.X * 16, statue.Origin.Y * 16 + offset.Y * 16) + SpawnOffset;
                Vector2 topLeft = new Vector2(feet.X - sample.width / 2f, feet.Y - sample.height);
                if (Collision.SolidCollision(topLeft, sample.width, sample.height)) continue;

                // SpawnedFromStatue is never set, so the enemy drops normal loot and coins.
                int index = NPC.NewNPC(new EntitySource_WorldEvent("AwakenedStatue"), (int)feet.X, (int)feet.Y, npcType);
                if (index < 0 || index >= Main.maxNPCs) return false;

                NPC npc = Main.npc[index];
                npc.npcSlots = 0f;   // awakened enemies must not suppress natural spawns around the player

                AwakenedSpawnGlobalNPC tag = npc.GetGlobalNPC<AwakenedSpawnGlobalNPC>();
                tag.Tagged = true;
                tag.Origin = statue.Origin;
                tag.Entry = statue.Entry;
                return true;
            }

            return false;
        }
    }
}