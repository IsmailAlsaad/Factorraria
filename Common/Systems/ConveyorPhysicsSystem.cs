using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Factorraria.Content.Tiles.Conveyors;

namespace Factorraria.Common.Systems
{
    public class ConveyorPhysicsSystem : ModSystem
    {
        // 2.5f matches vanilla Terraria conveyor belt speed (pixels per frame)
        public const float ConveyorSpeed = 2.5f;

        public override void PostUpdateNPCs()
        {
            // 1. Push Players
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];
                if (player.active && !player.dead)
                {
                    TryApplyConveyorForce(player);
                }
            }

            // 2. Push Enemies / NPCs
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active)
                {
                    TryApplyConveyorForce(npc);
                }
            }

            // 3. Push Dropped Vanilla Items
            for (int i = 0; i < Main.maxItems; i++)
            {
                Item item = Main.item[i];
                if (item.active && item.stack > 0)
                {
                    TryApplyConveyorForce(item);
                }
            }
        }

        private static void TryApplyConveyorForce(Entity entity)
        {
            // Only push entities when they are grounded
            if (Math.Abs(entity.velocity.Y) > 0.2f)
                return;

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
            }
        }
    }
}