using Factorraria.Common.Net;
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
            // UseItem also runs for remote players on every machine, and on the server. Only the owner's own client toggles the suit; everyone
            // else learns the flag from MechSuitNet.
            if (!FactorrariaNet.IsOwner(player))
                return true;

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

        // Owner only. How often (in ticks) the owner re-announces its live arms so a client that joined later creates them.
        int ArmResyncTicks = 180;

        MechArmSlot[] arms = new MechArmSlot[MaxArms];
        int searchCooldown;
        int armResyncTimer;
        bool jumpWasDown; // owner only: last frame's controlJump, for the dismount press edge

        public void Activate()
        {
            IsActive = true;
            searchCooldown = 0;
            armResyncTimer = ArmResyncTicks;
            jumpWasDown = Player.controlJump; // Space still held from before the toggle is not a dismount press
            MechSuitNet.ReportState(true);
        }

        public void Deactivate()
        {
            IsActive = false;
            RetractArms();
            MechSuitNet.ReportState(false);
        }

        // Owner only: kills this player's own arm projectiles and forgets them.
        void RetractArms()
        {
            for (int i = 0; i < MaxArms; i++)
            {
                if (arms[i].IsActive)
                    Main.projectile[arms[i].ProjectileIndex].Kill();

                arms[i] = new MechArmSlot { ProjectileIndex = -1 };
            }
        }

        // Called by MechSuitNet on the server and on remote clients: mirrors the owner's flag so the arm projectiles (which run everywhere)
        // know the suit is on. The owner ignores it, because its own flag is the source.
        public void SetReportedState(bool active)
        {
            if (FactorrariaNet.IsOwner(Player))
                return;

            IsActive = active;
        }

        // Server and remote clients: the player left, so their mirrored flag goes with them (their arms die with them, see MechSuitArmProjectile.AI).
        public override void PlayerDisconnect()
        {
            IsActive = false;
        }

        // Server: a client is being told about this player (a late joiner). Tell it whether this player is in the suit.
        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
        {
            if (Main.netMode == NetmodeID.Server && IsActive && toWho >= 0)
            {
                MechSuitNet.SendState(Player.whoAmI, true, toWho);
                Mod.Logger.Info($"[MechSuit] sent suit flag of player {Player.whoAmI} to joining client {toWho}"); // 2.1b test line, remove in Phase 10
            }
        }

        // Runs for every player on every machine, before vanilla adds gravity to velocity.Y this frame. PreUpdateMovement is too late for
        // that: gravity is already added by then, and Player.gravity is reset at the start of the next update. The remote copy of a suited
        // player is moved only by the position and velocity packets, so with normal gravity it sags between packets and plays the falling
        // pose. The owner overwrites its velocity in ApplyFreeMovement, so it does not need this.
        public override void PostUpdateRunSpeeds()
        {
            if (IsActive && !FactorrariaNet.IsOwner(Player))
                Player.gravity = 0f;
        }

        public override void PreUpdateMovement()
        {
            if (!IsActive) return;

            // PreUpdateMovement runs for every player on every machine. Only the owner drives the suit (the movement, the arm launches, the
            // dismount); everyone else sees the result through the synced position and the arm projectiles.
            if (!FactorrariaNet.IsOwner(Player)) return;

            if (Player.dead)
            {
                Deactivate();
                return;
            }

            // Space (Jump) while active = full dismount, retracts everything. A fresh press is controlJump going from up to down. releaseJump
            // cannot be used: vanilla's jump code runs earlier in Player.Update and has already cleared it on every frame where controlJump is
            // held, so "controlJump && releaseJump" was never true here. PlayerInput would read the local keyboard for every player.
            bool jumpPressed = Player.controlJump && !jumpWasDown;
            jumpWasDown = Player.controlJump;
            if (jumpPressed)
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

            // Vanilla sends a projectile only when it spawns, so a client that joins while we are suited never gets our arms. Marking them
            // netUpdate every few seconds makes the owner send them again: the server relays it, a client without the arm creates it, and a
            // client that already has it re-applies the same anchor and position. Single-player has nobody to tell.
            if (Main.netMode == NetmodeID.MultiplayerClient && --armResyncTimer <= 0)
            {
                armResyncTimer = ArmResyncTicks;
                for (int i = 0; i < MaxArms; i++)
                {
                    if (arms[i].IsActive)
                        Main.projectile[arms[i].ProjectileIndex].netUpdate = true;
                }
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

            // The anchor tile and the bend side travel in ai[0..2] of the spawn packet, so every machine builds the same arm from the packet itself.
            int projIndex = Projectile.NewProjectile(
                Player.GetSource_Misc("MechSuitArm"),
                Player.Center,
                Vector2.Zero,
                ModContent.ProjectileType<MechSuitArmProjectile>(),
                0, 0f,
                Player.whoAmI,
                tile.X, tile.Y,
                (slot % 2 == 0) ? 1f : -1f); // alternate bend so multiple arms fan out visually instead of overlapping

            if (projIndex < 0 || projIndex >= Main.maxProjectiles)
                return;

            arms[slot] = new MechArmSlot { ProjectileIndex = projIndex, AnchorTile = tile };
        }

        static Vector2 AnchorWorldCenter(Point tile) => new Vector2(tile.X * 16 + 8, tile.Y * 16 + 8);
    }
}
