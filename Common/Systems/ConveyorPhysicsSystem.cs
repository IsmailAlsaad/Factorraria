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

        // Dedicated server / Host & Play server: an NPC being carried is re-sent every this many ticks (staggered per NPC)
        // so clients see it slide instead of jumping. Not const so hot reload can tweak it.
        public static int NpcSyncTicks = 4;

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

            // 2. Push Enemies / NPCs: the server (or single-player) is the only side that does it. Clients get the new
            //    position from the server, which flags the NPC for a vanilla NPC sync (netUpdate) while it is being carried.
            if (FactorrariaNet.IsServerSide)
            {
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC npc = Main.npc[i];
                    if (npc.active && TryApplyConveyorForce(npc))
                    {
                        if (Main.netMode == NetmodeID.Server && (Main.GameUpdateCount + (uint)i) % (uint)Math.Max(1, NpcSyncTicks) == 0)
                            npc.netUpdate = true;
                    }
                }
            }

            // 3. Push Dropped Vanilla Items: unchanged on purpose. Item.Update runs on the server and on every client and
            //    vanilla's own belts move items on both sides, so doing the same here keeps both copies in step.
            //    (Do not restrict this to the server: the clients would then show items stuck until the next position packet.)
            // Note: the check below is the same on every side, so the result is the same on every side.
            for (int i = 0; i < Main.maxItems; i++)
            {
                Item item = Main.item[i];
                if (item.active && item.stack > 0)
                {
                    TryApplyConveyorForce(item);
                }
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

                if(!(entity is Item))
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