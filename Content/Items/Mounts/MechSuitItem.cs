using Factorraria.Content.Projectiles.Mounts;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Items.Mounts
{
    public class MechSuitItem : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 24;
            Item.maxStack = 1;
            Item.value = Item.buyPrice(gold: 5);
            Item.rare = ItemRarityID.LightRed;

            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.noMelee = true;
            Item.UseSound = SoundID.Item6;
            Item.autoReuse = false;
            Item.consumable = false; // it's a toggle tool, not consumed on use
        }

        public override bool? UseItem(Player player)
        {
            MechSuitPlayer mechPlayer = player.GetModPlayer<MechSuitPlayer>();

            if (mechPlayer.IsActive)
                mechPlayer.Deactivate();
            else
                mechPlayer.Activate();

            return true;
        }
    }
    public static class MechSuitTileSearch
    {
        // Nearest solid tile to centerWorld within radiusPixels, skipping anything in
        // excludeTiles (tiles other arms are already anchored to). Plain O(radius^2) tile
        // scan — fine at the throttled call rate MechSuitPlayer uses (cooldown-gated).
        public static bool TryFindNearestAnchorTile(Vector2 centerWorld, float radiusPixels, HashSet<Point> excludeTiles, out Point foundTile)
        {
            foundTile = default;
            float bestDistSq = float.MaxValue;
            bool found = false;

            int centerTileX = (int)(centerWorld.X / 16f);
            int centerTileY = (int)(centerWorld.Y / 16f);
            int radiusTiles = (int)System.MathF.Ceiling(radiusPixels / 16f);
            float radiusSq = radiusPixels * radiusPixels;

            for (int x = centerTileX - radiusTiles; x <= centerTileX + radiusTiles; x++)
            {
                for (int y = centerTileY - radiusTiles; y <= centerTileY + radiusTiles; y++)
                {
                    if (!WorldGen.InWorld(x, y)) continue;

                    Point tilePoint = new Point(x, y);
                    if (excludeTiles.Contains(tilePoint)) continue;

                    Tile tile = Main.tile[x, y];
                    if (!tile.HasTile || !Main.tileSolid[tile.TileType]) continue;

                    Vector2 tileCenter = new Vector2(x * 16 + 8, y * 16 + 8);
                    float distSq = Vector2.DistanceSquared(centerWorld, tileCenter);
                    if (distSq > radiusSq) continue;

                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        foundTile = tilePoint;
                        found = true;
                    }
                }
            }

            return found;
        }
    }

    struct MechArmSlot
    {
        public int ProjectileIndex;
        public Point AnchorTile;

        public bool IsActive =>
            ProjectileIndex >= 0 &&
            ProjectileIndex < Main.maxProjectiles &&
            Main.projectile[ProjectileIndex].active &&
            Main.projectile[ProjectileIndex].type == ModContent.ProjectileType<MechSuitArmProjectile>();
    }

    public class MechSuitPlayer : ModPlayer
    {
        public const int MaxArms = 3;

        // Tunables — adjust to taste.
        const float SearchRadiusTiles = 12f;
        const float CutoffRadiusTiles = 20f;
        const float MoveSpeed = 7f;
        const int SearchCooldownTicks = 10; // throttles the tile scan while drifting/waiting

        const float SearchRadiusPixels = SearchRadiusTiles * 16f;
        const float CutoffRadiusPixels = CutoffRadiusTiles * 16f;

        public bool IsActive { get; private set; }

        MechArmSlot[] arms = new MechArmSlot[MaxArms];
        int searchCooldown;

        public void Activate()
        {
            IsActive = true;
            searchCooldown = 0;
        }

        public void Deactivate()
        {
            IsActive = false;

            for (int i = 0; i < MaxArms; i++)
            {
                if (arms[i].IsActive)
                    Main.projectile[arms[i].ProjectileIndex].Kill();

                arms[i] = new MechArmSlot { ProjectileIndex = -1 };
            }
        }

        public override void PreUpdateMovement()
        {
            if (!IsActive) return;

            if (Player.dead)
            {
                Deactivate();
                return;
            }

            // Space (Jump) while active = full dismount, retracts everything.
            if (PlayerInput.Triggers.JustPressed.Jump)
            {
                Deactivate();
                return;
            }

            UpdateArms();
            ApplyFreeMovement();
        }

        void ApplyFreeMovement()
        {
            Player.gravity = 0f;

            Vector2 move = Vector2.Zero;
            if (Player.controlLeft) move.X -= 1f;
            if (Player.controlRight) move.X += 1f;
            if (Player.controlUp) move.Y -= 1f;
            if (Player.controlDown) move.Y += 1f;

            if (move != Vector2.Zero)
                move.Normalize();

            // Directly setting velocity (not position) keeps normal tile collision intact —
            // the vanilla movement step that follows still runs Collision.TileCollision on this.
            Player.velocity = move * MoveSpeed;
        }

        void UpdateArms()
        {
            if (searchCooldown > 0)
                searchCooldown--;

            int activeCount = 0;
            bool anyBeyondSearch = false;

            for (int i = 0; i < MaxArms; i++)
            {
                if (!arms[i].IsActive)
                {
                    arms[i] = new MechArmSlot { ProjectileIndex = -1 };
                    continue;
                }

                activeCount++;

                float dist = Vector2.Distance(Player.Center, AnchorWorldCenter(arms[i].AnchorTile));
                if (dist > SearchRadiusPixels)
                    anyBeyondSearch = true;
            }

            // Cutoff retraction — only ever drop an arm if at least one other stays attached,
            // so the player is never left with zero support mid-check.
            for (int i = 0; i < MaxArms; i++)
            {
                if (!arms[i].IsActive) continue;

                float dist = Vector2.Distance(Player.Center, AnchorWorldCenter(arms[i].AnchorTile));
                if (dist > CutoffRadiusPixels && activeCount > 1)
                {
                    Main.projectile[arms[i].ProjectileIndex].Kill();
                    arms[i] = new MechArmSlot { ProjectileIndex = -1 };
                    activeCount--;
                }
            }

            // Launch a new arm if we have zero (fresh activation / open-air fallback) or the
            // player has drifted past search radius on an existing one. Doesn't retract anything.
            bool needsSearch = activeCount == 0 || anyBeyondSearch;
            int freeSlot = FindFreeSlot();

            if (needsSearch && freeSlot != -1 && searchCooldown <= 0)
            {
                TryLaunchNewArm(freeSlot);
                searchCooldown = SearchCooldownTicks;
            }
        }

        int FindFreeSlot()
        {
            for (int i = 0; i < MaxArms; i++)
                if (!arms[i].IsActive) return i;
            return -1;
        }

        void TryLaunchNewArm(int slot)
        {
            HashSet<Point> exclude = new HashSet<Point>();
            for (int i = 0; i < MaxArms; i++)
                if (arms[i].IsActive) exclude.Add(arms[i].AnchorTile);

            if (!MechSuitTileSearch.TryFindNearestAnchorTile(Player.Center, SearchRadiusPixels, exclude, out Point tile))
                return; // nothing in range — will just retry next eligible tick (fallback to gravity is implicit: no arm means normal player physics elsewhere still hold)

            int projIndex = Projectile.NewProjectile(
                Player.GetSource_Misc("MechSuitArm"),
                Player.Center,
                Vector2.Zero,
                ModContent.ProjectileType<MechSuitArmProjectile>(),
                0, 0f,
                Player.whoAmI);

            if (Main.projectile[projIndex].ModProjectile is MechSuitArmProjectile armComp)
            {
                armComp.AnchorTile = tile;
                armComp.ArmBendSign = (slot % 2 == 0) ? 1f : -1f; // alternate bend so multiple arms fan out visually instead of overlapping
            }

            arms[slot] = new MechArmSlot { ProjectileIndex = projIndex, AnchorTile = tile };
        }

        static Vector2 AnchorWorldCenter(Point tile) => new Vector2(tile.X * 16 + 8, tile.Y * 16 + 8);
    }
}
