using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Factorraria.Common.Net;
using Factorraria.Content.Tiles.Conveyors;

namespace Factorraria.Common.Systems
{
    public class ConveyorPhysicsSystem : ModSystem
    {
        // 2.5f matches vanilla Terraria conveyor belt speed (pixels per frame)
        public const float ConveyorSpeed = 2.5f;

        // Dedicated server: a carried NPC or item is sent to the clients (vanilla SyncNPC / SyncItem) every this many ticks,
        // staggered per entity, so clients see it slide. Not const so hot reload can tweak them.
        public static int NpcSyncTicks = 3;
        public static int ItemSyncTicks = 3;

        // TEMPORARY diagnostics (remove in Phase 10): the server logs what it pushed and sent every 5 seconds, only when it was not zero.
        public static bool LogStats = true;
        static int npcsPushed;
        static int itemsPushed;
        static int syncsSent;

        static bool SyncDue(int index, int everyTicks)
        {
            return (Main.GameUpdateCount + (uint)index) % (uint)Math.Max(1, everyTicks) == 0;
        }

        public override void PostUpdateNPCs()
        {
            // 1. Push the player this process controls, and nobody else. Remote players are moved by their own client and
            //    reach us through vanilla's position packets, so pushing them here too would push them twice and make them jitter.
            //    A dedicated server has no local player (HasLocalClient is false), so it pushes no one.
            if (FactorrariaNet.HasLocalClient && Main.myPlayer >= 0 && Main.myPlayer < Main.maxPlayers)
            {
                Player player = Main.player[Main.myPlayer];
                if (player.active && !player.dead && TryApplyConveyorForce(player))
                {
                    // The belt moves the owner without a key press and vanilla only sends the player when a key changes.
                    // Owner + multiplayer client only (the helper checks both), so single-player is untouched.
                    FactorrariaNet.SyncPlayerNow(player);
                }
            }

            // 2. Push Enemies / NPCs. Only the server (or single-player) pushes them; a client never does, because its own copy
            //    of the NPC would run ahead of or behind the server's. The server sends the carried NPC to every client
            //    (vanilla SyncNPC, position and velocity) a few times a second.
            if (FactorrariaNet.IsServerSide)
            {
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC npc = Main.npc[i];
                    if (npc.active && TryApplyConveyorForce(npc))
                    {
                        npcsPushed++;

                        if (Main.netMode == NetmodeID.Server && SyncDue(i, NpcSyncTicks))
                        {
                            NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, i);
                            syncsSent++;
                        }
                    }
                }
            }

            // 3. Push Dropped Vanilla Items: same rule as NPCs. The server pushes and sends them (vanilla SyncItem), the
            //    clients do not push, so a client can never show an item somewhere the server has not put it.
            if (FactorrariaNet.IsServerSide)
            {
                for (int i = 0; i < Main.maxItems; i++)
                {
                    Item item = Main.item[i];
                    if (item.active && item.stack > 0 && TryApplyConveyorForce(item))
                    {
                        itemsPushed++;

                        if (Main.netMode == NetmodeID.Server && !item.instanced && SyncDue(i, ItemSyncTicks))
                        {
                            NetMessage.SendData(MessageID.SyncItem, -1, -1, null, i);
                            syncsSent++;
                        }
                    }
                }
            }

            if (Main.netMode == NetmodeID.Server && Main.GameUpdateCount % 300 == 0)
            {
                if (LogStats && (npcsPushed > 0 || itemsPushed > 0))
                    FactorrariaNet.Log("[Conveyor] last 5 s: NPC pushes " + npcsPushed + ", item pushes " + itemsPushed + ", sync packets sent " + syncsSent);

                npcsPushed = 0;
                itemsPushed = 0;
                syncsSent = 0;
            }
        }

        /// <summary>Moves the entity along the belt under it. Returns true when it actually moved.</summary>
        private static bool TryApplyConveyorForce(Entity entity)
        {
            // Only push entities when they are grounded
            if (Math.Abs(entity.velocity.Y) > 0.2f)
                return false;

            // Find tile coordinates directly underneath the entity's feet
            int startX = (int)(entity.position.X / 16f);
            int endX = (int)((entity.position.X + entity.width - 1) / 16f);
            int tileY = (int)((entity.position.Y + entity.height + 1) / 16f);

            float pushSpeed = 0f;

            // Check every tile underneath the entity's width
            for (int x = startX; x <= endX; x++)
            {
                if (!WorldGen.InWorld(x, tileY))
                    continue;

                Tile tile = Main.tile[x, tileY];
                if (tile == null || !tile.HasTile)
                    continue;

                if (tile.TileType == ModContent.TileType<ClockwisePriorityConveyorTile>())
                {
                    pushSpeed = ConveyorSpeed;
                    break;
                }
                else if (tile.TileType == ModContent.TileType<CounterClockwisePriorityConveyorTile>())
                {
                    pushSpeed = -ConveyorSpeed;
                    break;
                }

                // Vanilla moves a player standing on a vanilla belt by itself, but not NPCs or dropped items, so the mod pushes those.
                // (Before patch 2.5c this check stopped every non-item here, which left NPCs on vanilla belts alone.)
                if (entity is Player)
                {
                    break;
                }

                if (tile.TileType == TileID.ConveyorBeltRight)
                {
                    pushSpeed = ConveyorSpeed;
                    break;
                }
                else if (tile.TileType == TileID.ConveyorBeltLeft)
                {
                    pushSpeed = -ConveyorSpeed;
                    break;
                }
            }

            if (pushSpeed != 0f)
            {
                // Collision check ensures entities are moved smoothly without clipping into walls
                Vector2 requestedMove = new Vector2(pushSpeed, 0f);
                Vector2 actualMove = Collision.TileCollision(entity.position, requestedMove, entity.width, entity.height, false, false);

                entity.position.X += actualMove.X;
                return actualMove.X != 0f;
            }

            return false;
        }
    }
}