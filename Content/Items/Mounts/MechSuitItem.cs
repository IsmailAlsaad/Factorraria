using Factorraria.Common.Net;
using Factorraria.Content.Projectiles.Mounts;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
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

            // A real mount item: vanilla accepts it in the mount slot (miscEquips[3]), the Mount key (default Z) and a click both mount and
            // dismount. The suit itself starts and stops from MechSuitMount.SetMount / Dismount (owner only).
            Item.mountType = ModContent.MountType<MechSuitMount>();
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.noMelee = true;
            Item.UseSound = SoundID.Item6;
            Item.autoReuse = false;
            Item.consumable = false;
        }
    }
    public static class MechSuitTileSearch
    {
        // Nearest solid tile to centerWorld within radiusPixels, skipping anything in
        // any tile closer than deadzoneRadiusPixels to an anchor another arm already uses. Plain O(radius^2) tile
        // scan — fine at the throttled call rate MechSuitPlayer uses (cooldown-gated).
        public static bool TryFindNearestAnchorTile(Vector2 centerWorld, float radiusPixels, List<Vector2> deadzoneCenters, float deadzoneRadiusPixels, out Point foundTile)
        {
            foundTile = default;
            float bestDistSq = float.MaxValue;
            bool found = false;

            int centerTileX = (int)(centerWorld.X / 16f);
            int centerTileY = (int)(centerWorld.Y / 16f);
            int radiusTiles = (int)System.MathF.Ceiling(radiusPixels / 16f);
            float radiusSq = radiusPixels * radiusPixels;
            float deadzoneSq = deadzoneRadiusPixels * deadzoneRadiusPixels;

            for (int x = centerTileX - radiusTiles; x <= centerTileX + radiusTiles; x++)
            {
                for (int y = centerTileY - radiusTiles; y <= centerTileY + radiusTiles; y++)
                {
                    if (!WorldGen.InWorld(x, y)) continue;

                    Point tilePoint = new Point(x, y);

                    Tile tile = Main.tile[x, y];
                    if (!tile.HasTile || !Main.tileSolid[tile.TileType]) continue;

                    Vector2 tileCenter = new Vector2(x * 16 + 8, y * 16 + 8);
                    float distSq = Vector2.DistanceSquared(centerWorld, tileCenter);
                    if (distSq > radiusSq) continue;

                    // Deadzone: a tile this close to another arm's anchor is not allowed (a circle, not just the anchor tile itself).
                    bool inDeadzone = false;
                    if (deadzoneCenters != null)
                    {
                        for (int d = 0; d < deadzoneCenters.Count; d++)
                        {
                            if (Vector2.DistanceSquared(deadzoneCenters[d], tileCenter) < deadzoneSq)
                            {
                                inDeadzone = true;
                                break;
                            }
                        }
                    }
                    if (inDeadzone) continue;

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

        public bool IsLatched =>
            IsActive &&
            Main.projectile[ProjectileIndex].ModProjectile is MechSuitArmProjectile arm &&
            arm.Latched;
    }

    public class MechSuitPlayer : ModPlayer
    {
        public const int MaxArms = 3;

        // Tunables — adjust to taste.
        // Public so the EnableDebugs overlay (MechSuitDebugSystem) can draw them. Plain fields on purpose, so hot reload can tweak them.
        public float SearchRadiusTiles = 12f;   // an arm looks for an anchor tile this far from the player
        public float CutoffRadiusTiles = 13f;   // the player can never get farther than this from the closest latched arm
        public float DeadzoneRadiusTiles = 8f;  // a new arm only launches at a tile at least this far from every other arm's anchor
        public float MoveSpeed = 7f;
        public int SearchCooldownTicks = 10;    // throttles the tile scan while drifting/waiting

        public float SearchRadiusPixels => SearchRadiusTiles * 16f;
        public float CutoffRadiusPixels => CutoffRadiusTiles * 16f;
        public float DeadzoneRadiusPixels => DeadzoneRadiusTiles * 16f;

        public bool IsActive { get; private set; }

        // Owner only. How often (in ticks) the owner re-announces its live arms so a client that joined later creates them.
        int ArmResyncTicks = 180;

        MechArmSlot[] arms = new MechArmSlot[MaxArms];
        int searchCooldown;
        int armResyncTimer;
        bool jumpWasDown; // owner only: last frame's controlJump, for the dismount press edge
        // Owner only. Vanilla sends the player packet only when something changes, and the owner's velocity is overwritten every frame, so
        // while it hovers still nothing triggers a send and the others drift. Re-announce the position this often (ticks) while suited.
        int PosResyncTicks = 1;
        int posResyncTimer;
        Vector2 lastMoveInput;

        readonly List<Vector2> deadzoneScratch = new List<Vector2>();

        public void Activate()
        {
            IsActive = true;
            searchCooldown = 0;
            armResyncTimer = ArmResyncTicks;
            posResyncTimer = 0;
            lastMoveInput = Vector2.Zero;
            jumpWasDown = Player.controlJump; // Space still held from before the toggle is not a dismount press
            MechSuitNet.ReportState(true);
        }

        // Owner only. Turns the suit off and takes the vanilla mount off too (Space dismount, death). The mount's own Dismount hook then
        // finds the suit already off and does nothing.
        public void Deactivate()
        {
            DeactivateCore();

            if (Player.mount.Active && Player.mount.Type == ModContent.MountType<MechSuitMount>())
                Player.mount.Dismount(Player);
        }

        // Owner only. The suit's own part of leaving: flag off, arms retracted, everyone told. MechSuitMount.Dismount calls this directly
        // (Mount key, clicking the item, another mount, death), because vanilla is already taking the mount off by then.
        public void DeactivateCore()
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

        // Every machine runs this when the player dies. The owner leaves the suit properly (retracts the arms and tells the server, which
        // tells everyone), so the mirrored flag on the server and the other clients does not stay on through the respawn.
        public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource)
        {
            if (IsActive && FactorrariaNet.IsOwner(Player))
                Deactivate();
        }

        // Server: a client is being told about this player (a late joiner). Tell it whether this player is in the suit.
        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
        {
            if (Main.netMode == NetmodeID.Server && IsActive && toWho >= 0)
            {
                MechSuitNet.SendState(Player.whoAmI, true, toWho);
            }
        }

        // Runs for every player on every machine, before vanilla adds gravity to velocity.Y this frame. PreUpdateMovement is too late for
        // that: gravity is already added by then, and Player.gravity is reset at the start of the next update. The remote copy of a suited
        // player is moved only by the position and velocity packets, so with normal gravity it sags between packets and plays the falling
        // pose. The owner overwrites its velocity in ApplyFreeMovement, so it does not need this.
        public override void PostUpdateRunSpeeds()
        {
            if (IsActive && !FactorrariaNet.IsOwner(Player) && TryGetClosestLatchedAnchor(out _))
            {
                Player.gravity = 0f;
            }
        }

        public override void PreUpdateMovement()
        {
            if (!IsActive) return;

            // PreUpdateMovement runs for every player on every machine. Only the owner drives the suit (the movement, the arm launches, the
            // dismount); everyone else sees the result through the synced position and the arm projectiles.
            if (!FactorrariaNet.IsOwner(Player))
            {
                // Remote copy. Set the velocity here, right before vanilla's move step, so nothing earlier in the frame can leave a trace:
                // an enemy hit received for this copy adds knockback, but the owner discards its own knockback (its velocity is overwritten
                // every frame), so this copy must too. The control bits from the player packet say what the owner is holding; a packet
                // that says "stopped" carries no velocity, so extrapolating the old one made the copy slide on. The position packets
                // (sent every tick by the owner, see PosResyncTicks) are what keep the copy where the owner actually is.
                // With no arm latched the owner is in regular movement (gravity), so the copy keeps what the position packets say.
                if (TryGetClosestLatchedAnchor(out Vector2 remoteAnchor))
                {
                    Player.velocity = BoundedSuitVelocity(remoteAnchor);
                    StepThroughPlatforms();
                }
                return;
            }

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

            UpdateArms();

            // No arm latched (still flying out, or nothing in range): the suit gives up the movement and the player falls and moves like
            // normal, with gravity. Space is then a normal jump; it only dismounts while the suit is in control.
            if (!TryGetClosestLatchedAnchor(out Vector2 anchor))
                return;

            if (jumpPressed)
            {
                Deactivate();
                return;
            }

            ApplyFreeMovement(anchor);
            StepThroughPlatforms();
        }

        // After vanilla has moved the player this frame (so the packet carries the new position). Owner only: send the suit's position when the
        // input changes and every PosResyncTicks, because the owner's velocity is overwritten every frame and vanilla sees nothing worth sending.
        public override void PostUpdate()
        {
            if (IsActive && TryGetClosestLatchedAnchor(out _))
                ApplyFallPose();

            if (!IsActive || !FactorrariaNet.IsOwner(Player))
                return;

            Vector2 input = ReadMoveInput();
            if (input != lastMoveInput || --posResyncTimer <= 0)
            {
                lastMoveInput = input;
                posResyncTimer = PosResyncTicks;
                FactorrariaNet.SyncPlayerNow(Player);
            }
        }

        // The suit's direction rule, from the control bits. Used by the owner (real keys) and by remote copies (bits from the player packet).
        Vector2 ReadMoveInput()
        {
            Vector2 move = Vector2.Zero;
            if (Player.controlLeft) move.X -= 1f;
            if (Player.controlRight) move.X += 1f;
            if (Player.controlUp) move.Y -= 1f;
            if (Player.controlDown) move.Y += 1f;

            if (move != Vector2.Zero)
                move.Normalize();

            return move;
        }

        void ApplyFreeMovement(Vector2 anchor)
        {
            Player.gravity = 0f;

            // Directly setting velocity (not position) keeps normal tile collision intact:
            // the vanilla movement step that follows still runs Collision.TileCollision on this.
            Player.velocity = BoundedSuitVelocity(anchor);
        }

        // The input direction at MoveSpeed, bounded so the player can never end up farther than CutoffRadiusPixels from the closest latched
        // arm's anchor: the part of the move that would leave the circle is cut off (the player slides along it), and a player that is already
        // outside is pulled back in (at most twice MoveSpeed per tick). Used by the owner and by remote copies, so both agree.
        Vector2 BoundedSuitVelocity(Vector2 anchor)
        {
            Vector2 velocity = ReadMoveInput() * MoveSpeed;

            Vector2 fromAnchor = Player.Center + velocity - anchor;
            float distance = fromAnchor.Length();
            float cutoff = CutoffRadiusPixels;
            if (distance > cutoff && distance > 0.001f)
            {
                Vector2 bounded = anchor + fromAnchor / distance * cutoff;
                velocity = bounded - Player.Center;

                float maxPull = MoveSpeed * 2f;
                if (velocity.Length() > maxPull)
                    velocity = Vector2.Normalize(velocity) * maxPull;
            }

            return velocity;
        }

        // Closest latched arm of this player, on any machine. Reads the arm projectiles themselves (the anchor travels in ai[0..2] and every
        // machine works out Latched on its own), not the owner-only arms array, so remote copies and the debug overlay get the same answer.
        public bool TryGetClosestLatchedAnchor(out Vector2 anchor)
        {
            anchor = default;
            float bestDistSq = float.MaxValue;
            bool found = false;
            int armType = ModContent.ProjectileType<MechSuitArmProjectile>();

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile projectile = Main.projectile[i];
                if (!projectile.active || projectile.type != armType || projectile.owner != Player.whoAmI)
                    continue;
                if (!(projectile.ModProjectile is MechSuitArmProjectile arm) || !arm.Latched)
                    continue;

                float distSq = Vector2.DistanceSquared(Player.Center, projectile.Center);
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    anchor = projectile.Center;
                    found = true;
                }
            }

            return found;
        }

        // Platforms: a platform only blocks a body that moves down onto it, and vanilla's move step lets it through only with its own
        // "fall through" flag (tied to the real Down key, which forcing controlDown did not reach). So the suit does the step itself: when
        // the move is blocked only because of a platform (same TileCollision with and without fall-through gives different answers), the
        // player is moved by the fall-through result and the velocity is cleared, so vanilla's step that follows has nothing left to block.
        // The velocity is rebuilt every tick while an arm is latched (owner and remote copies), so clearing it costs nothing.
        void StepThroughPlatforms()
        {
            Vector2 velocity = Player.velocity;
            if (velocity == Vector2.Zero)
                return;

            int gravDir = (int)Player.gravDir;
            Vector2 solid = Collision.TileCollision(Player.position, velocity, Player.width, Player.height, false, false, gravDir);
            Vector2 through = Collision.TileCollision(Player.position, velocity, Player.width, Player.height, true, true, gravDir);
            if (solid == through)
                return;

            Player.position += through;
            Player.velocity = Vector2.Zero;
        }

        // Frame 5 of the player sheet is the jump/fall pose. Legs always; the body only when no item swing is using its arm frames.
        void ApplyFallPose()
        {
            Player.legFrame.Y = Player.legFrame.Height * 5;
            Player.legFrameCounter = 0.0;

            if (Player.itemAnimation <= 0)
            {
                Player.bodyFrame.Y = Player.bodyFrame.Height * 5;
                Player.bodyFrameCounter = 0.0;
            }
        }

        // Draw time as well, in case the frame code ran after PostUpdate this tick. Every machine draws every suited player this way.
        public override void ModifyDrawInfo(ref PlayerDrawSet drawInfo)
        {
            if (IsActive && TryGetClosestLatchedAnchor(out _))
                ApplyFallPose();
        }

        void UpdateArms()
        {
            if (searchCooldown > 0)
                searchCooldown--;

            int activeCount = 0;
            int latchedCount = 0;

            for (int i = 0; i < MaxArms; i++)
            {
                if (!arms[i].IsActive)
                {
                    arms[i] = new MechArmSlot { ProjectileIndex = -1 };
                    continue;
                }

                activeCount++;
                if (arms[i].IsLatched) latchedCount++;
            }

            // Cutoff retraction — only ever drop an arm if at least one other stays attached,
            // so the player is never left with zero support mid-check.
            for (int i = 0; i < MaxArms; i++)
            {
                if (!arms[i].IsActive) continue;

                float dist = Vector2.Distance(Player.Center, AnchorWorldCenter(arms[i].AnchorTile));
                // A latched arm beyond the cutoff is dropped only while another latched arm remains (the bound in BoundedSuitVelocity keeps
                // the player inside the circle of the closest one). An arm still flying out is dropped if any latched arm exists.
                bool wasLatched = arms[i].IsLatched;
                if (dist > CutoffRadiusPixels && (wasLatched ? latchedCount > 1 : latchedCount > 0))
                {
                    Main.projectile[arms[i].ProjectileIndex].Kill();
                    arms[i] = new MechArmSlot { ProjectileIndex = -1 };
                    activeCount--;
                    if (wasLatched) latchedCount--;
                }
            }

            // Always try to fill every free slot (up to MaxArms): one arm per search tick, each at the nearest tile outside the deadzone
            // circles of the arms that are already out. Doesn't retract anything; the scan is throttled by SearchCooldownTicks.
            int freeSlot = FindFreeSlot();

            if (freeSlot != -1 && searchCooldown <= 0)
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
            // Every arm that is out (flying or latched) blocks new anchors inside its deadzone circle.
            deadzoneScratch.Clear();
            for (int i = 0; i < MaxArms; i++)
                if (arms[i].IsActive) deadzoneScratch.Add(AnchorWorldCenter(arms[i].AnchorTile));

            if (!MechSuitTileSearch.TryFindNearestAnchorTile(Player.Center, SearchRadiusPixels, deadzoneScratch, DeadzoneRadiusPixels, out Point tile))
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
            SoundEngine.PlaySound(SoundID.Item1 with { PitchVariance = 0.3f });
        }

        static Vector2 AnchorWorldCenter(Point tile) => new Vector2(tile.X * 16 + 8, tile.Y * 16 + 8);
    }
}
