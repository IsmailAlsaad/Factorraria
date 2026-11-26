using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Systems
{
    // Power poles patch P11 (little birds on the hanging wires), made multiplayer in P11c. A bird flies in, lands on a hanging wire, looks
    // around, chirps now and then, and flies off again when something moves close or the wire flashes (a pole powering up or down). It fades out
    // instead of vanishing, and it has 1 health: a weapon that touches it kills it (the vanilla bird NPC of its kind is spawned and killed).
    // Birds spawn day and night, in rain and in wind, but never underground.
    // Cosmetic only, nothing is saved. THE SERVER OWNS THE BIRDS (Common/Net/WireBirdNet.cs: spawning, which wire, how long they stay, who
    // scares them, death), so every player sees the same birds. This file is the client side: it receives the server's events, animates the
    // birds (arrival flight, idle turning, chirps, fade-out), draws them and asks the server about hits and flashes. A bird sits at a fraction
    // 'T' of its wire (WireKey names the wire), which a live rope turns into a fractional node index 'U' every tick, so it rides the wire as
    // it swings, and its landing and take-off give the rope a small kick. A bird whose wire is off screen is simply not drawn. The rest of
    // the class is in PoleWireDrawSystem.cs and PoleWireDrawSystem.Sparks.cs.
    //
    // Art: no new texture. The birds use the vanilla bird NPC sheets (Bird 74, Blue Jay 297, Cardinal 298), one picked at random per bird.
    // Layout (told by Ismail, not read from the game files): each sheet is one column of BirdFrameCount = 5 frames of 30x24, top to bottom:
    // frames 0 to 3 flying, frame 4 (the bottom one) perched. The art faces LEFT. All of it is tunable below.
    public partial class PoleWireDrawSystem
    {
        // Tunables (plain instance fields, so hot reload can change them).
        public bool Birds = true;
        public int[] BirdNpcIds = { 74, 297, 298 };   // vanilla Bird, Blue Jay, Cardinal: one is picked at random for each bird
        public bool BirdArtFacesLeft = true;          // vanilla NPC art faces left; set false if the birds look backwards
        public int BirdFrameCount = 5;                // frames in one sheet, stacked vertically (the frame height is the texture height divided by this)
        public int BirdPerchFrame = 4;                // the bottom frame
        public int BirdFlyFirstFrame = 0;             // flying: this frame and the next BirdFlyFrameCount - 1, in a loop
        public int BirdFlyFrameCount = 4;
        public float BirdFootLift = -2f;               // pixels the bottom of the frame sits above the wire's centre (raise it if the feet sink into the wire)
        public int MaxBirds = 10;                      // alive at once, in the whole view
        public float BirdSpawnSeconds = 10f;          // on average one new bird this often while a wire is on screen and there is room
        public float BirdMaxSlope = 0.6f;             // a bird only lands where the wire is flatter than this (rise over run)
        public float BirdPoleMarginPixels = 24f;      // no perching this close to a pole
        public float BirdSpacingPixels = 20f;         // minimum distance between two birds on one wire
        public float BirdFlySpeed = 2.6f;             // pixels per tick when flying in
        public int BirdMinPerchTicks = 600;           // how long a bird stays if nothing scares it
        public int BirdMaxPerchTicks = 2400;
        public float BirdScareRadius = 96f;           // pixels: a moving player or NPC this close scares a bird
        public float BirdScareSpeed = 1.2f;           // pixels per tick: how fast the mover must be (anything closer than 35% of the radius scares a player too)
        public float LandKick = 1.4f;                 // how hard a landing pushes the rope down (pixels per tick of node speed)
        public float TakeoffKick = 0.9f;              // how hard a take-off pulls it back up
        public int BirdLeaveMaxTicks = 300;           // a bird that is leaving starts to fade out after this long even if it is still on screen
        public int BirdFadeTicks = 30;                // how long a fade-out takes
        public int BirdArriveTicks = 360;             // the server treats a new bird as flying in for this long (not scared or retired yet); the client lands it sooner
        public int BirdSpawnRangeTilesX = 55;         // the server spawns birds on wires inside a box of this half size around a player (about a screen)
        public int BirdSpawnRangeTilesY = 32;
        public int BirdChirpMinTicks = 400;           // a perched bird chirps (SoundID.Bird) every so often, a random time in this range
        public int BirdChirpMaxTicks = 1600;
        public float BirdChirpVolume = 0.7f;
        public int BirdHitRangeTiles = 150;           // the server ignores a hit request from a player farther than this from the bird
        public int BirdHitWidth = 20;                 // pixels: the box a weapon has to touch to kill a bird (its feet are at the bottom centre)
        public int BirdHitHeight = 16;

        enum BirdState : byte { Arriving, Perched, Leaving }

        class WireBird
        {
            public BirdState State;
            public int Id;              // the server's id for this bird
            public WireKey Key;         // the wire it flies to or sits on
            public float T;             // where on that wire: 0 = end A, 1 = end B
            public float U;             // T as a fractional node index on the wire's live rope (only while that wire is on screen)
            public Vector2 Pos;         // world position (kept up to date in every state, so a take-off starts from the right place)
            public Vector2 Vel;
            public int Dir = 1;         // 1 faces right, -1 faces left
            public int Age;             // ticks in the current state
            public int Timer;           // ticks until the next idle action
            public int ChirpTimer;      // ticks until the next chirp
            public int HitCooldown;     // ticks until a hit on this bird may be sent to the server again
            public int Variant;
            public float Alpha = 1f;    // 1 solid, fades to 0 and the bird is then removed
            public bool Fading;         // set once it is leaving the world for good (off screen, wire off screen, leave time up)
        }

        readonly List<WireBird> birds = new List<WireBird>();
        readonly List<Rectangle> meleeHitboxes = new List<Rectangle>();       // melee swing boxes of this tick, filled by WireBirdMeleeHits
        // The vanilla texture of a bird variant (loaded on demand) and the size of one frame of it.
        bool TryGetBirdTexture(int variant, out Texture2D texture, out int frameWidth, out int frameHeight)
        {
            texture = null;
            frameWidth = 0;
            frameHeight = 0;
            if (BirdNpcIds == null || BirdNpcIds.Length == 0)
                return false;

            int id = BirdNpcIds[Math.Clamp(variant, 0, BirdNpcIds.Length - 1)];
            if (id <= 0 || id >= TextureAssets.Npc.Length)
                return false;

            Main.instance.LoadNPC(id);
            Asset<Texture2D> asset = TextureAssets.Npc[id];
            if (asset == null || !asset.IsLoaded)
                return false;

            int frames = Math.Max(1, BirdFrameCount);
            texture = asset.Value;
            frameWidth = texture.Width;
            frameHeight = texture.Height / frames;
            return frameHeight > 0;
        }

        // ---------------------------------------------------------------- client side: the server's events, animation, chirps, hit requests

        // Multiplayer (P11c): the SERVER owns the birds (Common/Net/WireBirdNet.cs): which exist, which wire and where on it (a fraction T of
        // the wire), when they take off and when they die. A client only shows them. The packet handlers may run off the main thread, so
        // they only queue work here and UpdateBirds runs it. In single player the same server code runs in the same process.
        readonly ConcurrentQueue<Action> birdEvents = new ConcurrentQueue<Action>();

        // "Bird <id> lands on wire <key> at fraction <t>." fresh = it just spawned (fly in from off screen), not fresh = it was already
        // sitting there (join sync): it just appears on its wire.
        public void ClientBirdSpawn(int id, WireKey key, float t, int variant, int side, bool fresh)
        {
            birdEvents.Enqueue(() => ApplyBirdSpawn(id, key, t, variant, side, fresh));
        }

        public void ClientBirdLeave(int id, int side)
        {
            birdEvents.Enqueue(() =>
            {
                foreach (WireBird bird in birds)
                {
                    if (bird.Id != id)
                        continue;

                    if (bird.State != BirdState.Leaving)
                        TakeOff(bird, bird.Pos - new Vector2(side * 10f, 0f));

                    return;
                }
            });
        }

        // The bird is dead: only the sprite goes (the server spawned and killed the vanilla bird NPC, which brings its own gore and sound).
        public void ClientBirdDie(int id)
        {
            birdEvents.Enqueue(() => birds.RemoveAll(bird => bird.Id == id));
        }

        // Join sync: these are all the birds that exist now.
        public void ClientBirdsReplaceAll(List<WireBirdInfo> infos)
        {
            birdEvents.Enqueue(() =>
            {
                birds.Clear();
                foreach (WireBirdInfo info in infos)
                    ApplyBirdSpawn(info.Id, info.Key, info.T, info.Variant, 1, false);
            });
        }

        void ApplyBirdSpawn(int id, WireKey key, float t, int variant, int side, bool fresh)
        {
            birds.RemoveAll(other => other.Id == id);

            var bird = new WireBird
            {
                Id = id,
                Key = key,
                T = t,
                Variant = variant,
                State = BirdState.Perched,
                Dir = side > 0 ? -1 : 1,
                Timer = Main.rand.Next(40, 120),
                ChirpTimer = Main.rand.Next(Math.Min(BirdChirpMinTicks, BirdChirpMaxTicks), Math.Max(BirdChirpMinTicks, BirdChirpMaxTicks) + 1)
            };

            PoleLinkSystem links = PoleLinkSystem.Instance;
            EnsureCache(links);
            if (byKey.TryGetValue(key, out CachedLink entry))
            {
                if (entry.Resolved)
                {
                    RestPoint(entry.RestA, entry.RestB, t, out Vector2 rest, out _);
                    bird.Pos = rest;
                }

                // A new bird on a wire that is on screen flies in from just outside the screen, a little above its spot.
                if (fresh && entry.Rope != null)
                {
                    GetVisibleRect(out Rectangle visible);
                    Vector2 perch = PointAlong(entry.Rope, ClampU(entry.Rope, t * (entry.Rope.Count - 1)));
                    bird.State = BirdState.Arriving;
                    bird.Pos = new Vector2(side > 0 ? visible.Right + 48f : visible.Left - 48f, perch.Y - (40f + 100f * Main.rand.NextFloat()));
                }
            }

            birds.Add(bird);
        }

        // ---------------------------------------------------------------- update (called from PostUpdateEverything, after UpdateSparks)

        void UpdateBirds()
        {
            // The server's events first (not skipped when the wire view is empty: a join sync may arrive before the links do).
            while (birdEvents.TryDequeue(out Action action))
                action();

            if (!Birds)
            {
                birds.Clear();
                meleeHitboxes.Clear();
                return;
            }

            PoleLinkSystem links = PoleLinkSystem.Instance;
            EnsureCache(links);
            GetVisibleRect(out Rectangle visible);

            for (int i = birds.Count - 1; i >= 0; i--)
            {
                WireBird bird = birds[i];

                // Any hit is only a request: the server decides, kills the bird and tells everyone (ClientBirdDie).
                if (bird.HitCooldown > 0)
                {
                    bird.HitCooldown--;
                }
                else if (IsBirdHit(bird))
                {
                    bird.HitCooldown = 20;
                    global::Factorraria.Common.Net.WireBirdNetSystem.RequestHit(bird.Id);
                }

                if (!UpdateBird(bird, visible))
                    birds.RemoveAt(i);
            }

            meleeHitboxes.Clear();
        }

        // False: remove the bird.
        bool UpdateBird(WireBird bird, Rectangle visible)
        {
            bird.Age++;

            if (bird.Fading)
            {
                bird.Alpha -= 1f / Math.Max(1, BirdFadeTicks);
                if (bird.Alpha <= 0f)
                    return false;
            }

            if (bird.State == BirdState.Leaving)
            {
                bird.Vel.Y -= 0.025f;
                bird.Vel.X *= 1.005f;
                bird.Pos += bird.Vel;
                bird.Dir = bird.Vel.X >= 0f ? 1 : -1;

                Rectangle area = visible;
                area.Inflate(64, 64);
                if (bird.Age > BirdLeaveMaxTicks || !area.Contains((int)bird.Pos.X, (int)bird.Pos.Y))
                    StartFade(bird);

                return true;
            }

            // Its wire. Not known here (yet), or off screen so no rope is kept for it: the bird stays where it is, unseen, and waits for the
            // server (a cut wire makes the server send it off). Its position is kept up to date from the resting curve so a take-off starts right.
            if (!byKey.TryGetValue(bird.Key, out CachedLink entry) || entry.Rope == null)
            {
                if (bird.State == BirdState.Arriving)
                {
                    bird.State = BirdState.Perched;
                    bird.Age = 0;
                }

                if (entry != null && entry.Resolved)
                {
                    RestPoint(entry.RestA, entry.RestB, bird.T, out Vector2 rest, out _);
                    bird.Pos = rest;
                }

                return true;
            }

            PoleRope rope = entry.Rope;
            bird.U = ClampU(rope, bird.T * (rope.Count - 1));
            Vector2 target = PointAlong(rope, bird.U);

            if (bird.State == BirdState.Arriving)
            {
                Vector2 delta = target - bird.Pos;
                float distance = delta.Length();

                if (distance <= 3f || bird.Age > 900)
                {
                    bird.State = BirdState.Perched;
                    bird.Age = 0;
                    bird.Pos = target;
                    bird.Timer = Main.rand.Next(40, 120);
                    Kick(rope, bird.U, LandKick);
                    return true;
                }

                // Slow down for the last stretch so it lands instead of overshooting.
                float speed = Math.Clamp(distance * 0.05f, 0.7f, Math.Max(0.8f, BirdFlySpeed));
                bird.Vel = delta / distance * speed;
                bird.Pos += bird.Vel;
                if (Math.Abs(delta.X) > 2f)
                    bird.Dir = delta.X >= 0f ? 1 : -1;

                return true;
            }

            // Perched: ride the wire.
            bird.Pos = target;

            if (--bird.Timer <= 0)
            {
                // Idle behaviour: look the other way or just sit.
                if (Main.rand.Next(100) < 40)
                {
                    bird.Dir = -bird.Dir;
                    bird.Timer = Main.rand.Next(30, 120);
                }
                else
                {
                    bird.Timer = Main.rand.Next(60, 180);
                }
            }

            // Every client chirps on its own (a sound is no shared state).
            if (--bird.ChirpTimer <= 0)
            {
                bird.ChirpTimer = Main.rand.Next(Math.Min(BirdChirpMinTicks, BirdChirpMaxTicks), Math.Max(BirdChirpMinTicks, BirdChirpMaxTicks) + 1);
                SoundEngine.PlaySound(SoundID.Bird.WithVolumeScale(BirdChirpVolume), bird.Pos);
            }

            return true;
        }

        // Starts a take-off from the bird's current spot. 'from' (may be null) is what scared it.
        void TakeOff(WireBird bird, Vector2? from)
        {
            if (bird.State == BirdState.Perched && byKey.TryGetValue(bird.Key, out CachedLink entry) && entry.Rope != null)
                Kick(entry.Rope, bird.U, -TakeoffKick);

            float side = from.HasValue ? Math.Sign(bird.Pos.X - from.Value.X) : 0f;
            if (side == 0f)
                side = Main.rand.NextBool() ? 1f : -1f;

            bird.State = BirdState.Leaving;
            bird.Age = 0;
            bird.Vel = new Vector2(side * (1.4f + 0.8f * Main.rand.NextFloat()), -(1.6f + 0.8f * Main.rand.NextFloat()));
            bird.Dir = side > 0f ? 1 : -1;
        }

        // The wire just flashed (called from the Sparks partial, on the client that saw it). The server decides who is scared, so this only
        // asks, and only when a bird of this client sits on that wire.
        void ScareBirds(PoleLink link)
        {
            var key = new WireKey(link);
            for (int i = 0; i < birds.Count; i++)
            {
                WireBird bird = birds[i];
                if (bird.State != BirdState.Leaving && bird.Key.Equals(key))
                {
                    global::Factorraria.Common.Net.WireBirdNetSystem.RequestScare(key);
                    return;
                }
            }
        }

        // Gives the nodes around 'u' a vertical speed (positive pushes down): the rope's Verlet speed is Pos - Old.
        static void Kick(PoleRope rope, float u, float strength)
        {
            int centre = (int)Math.Round(u);
            for (int i = centre - 2; i <= centre + 2; i++)
            {
                if (i < 1 || i > rope.Count - 2)
                    continue;

                float weight = 1f - Math.Abs(i - u) / 3f;
                if (weight > 0f)
                    rope.Old[i].Y -= strength * weight;
            }
        }

        float ClampU(PoleRope rope, float u)
        {
            return Math.Clamp(u, 0f, rope.Count - 1.001f);
        }

        // Direction of the rope at fractional node index 'u' (not normalised, always pointing from end A to end B).
        static Vector2 Tangent(PoleRope rope, float u)
        {
            int i0 = Math.Clamp((int)u, 0, rope.Count - 2);
            return rope.Pos[i0 + 1] - rope.Pos[i0];
        }

        // The point at fraction t (0 = end A, 1 = end B) of the RESTING curve of a wire from a to b, and its direction. The same parabola
        // PoleRope.FillRestCurve draws, so the server (which has no ropes) and every client agree on where a bird sits: a fraction t is node
        // index t * (Count - 1) on a live rope.
        public static void RestPoint(Vector2 a, Vector2 b, float t, out Vector2 point, out Vector2 tangent)
        {
            float chord = Vector2.Distance(a, b);
            float sag = (float)Math.Sqrt(0.375f * chord * PoleLinkSystem.Instance.SlackExtra(chord));

            point = Vector2.Lerp(a, b, t);
            point.Y += 4f * sag * t * (1f - t);
            tangent = (b - a) + new Vector2(0f, 4f * sag * (1f - 2f * t));
        }

        // ---------------------------------------------------------------- fading and getting hit

        // Starts the fade-out (once). The bird keeps doing what it was doing while it fades.
        void StartFade(WireBird bird)
        {
            bird.Fading = true;
        }

        // Called by WireBirdMeleeHits for every swing box of the local player this tick (world pixels).
        public void AddBirdMeleeHitbox(Rectangle hitbox)
        {
            if (Birds && birds.Count > 0)
                meleeHitboxes.Add(hitbox);
        }

        // Has the local player's weapon touched the bird? (Only the local player's swings and projectiles: every client asks about its own,
        // so the server gets one request, not one per client.)
        bool IsBirdHit(WireBird bird)
        {
            var box = new Rectangle((int)(bird.Pos.X - BirdHitWidth / 2f), (int)(bird.Pos.Y - BirdHitHeight), BirdHitWidth, BirdHitHeight);

            for (int i = 0; i < meleeHitboxes.Count; i++)
            {
                if (meleeHitboxes[i].Intersects(box))
                    return true;
            }

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile projectile = Main.projectile[i];
                if (!projectile.active || projectile.owner != Main.myPlayer || !projectile.friendly || projectile.hostile || projectile.damage <= 0)
                    continue;

                if (projectile.Hitbox.Intersects(box))
                    return true;

                // A fast projectile can cross the box between two ticks: test the path it covered this tick too.
                if (projectile.velocity.LengthSquared() > 64f
                    && Collision.CheckAABBvLineCollision(new Vector2(box.X, box.Y), new Vector2(box.Width, box.Height), projectile.Center - projectile.velocity, projectile.Center))
                    return true;
            }

            return false;
        }

        // ---------------------------------------------------------------- drawing (inside DrawLinks' batch, so the birds sit on the wire and behind the crossboards)

        void DrawBirds(SpriteBatch spriteBatch)
        {
            for (int i = 0; i < birds.Count; i++)
            {
                WireBird bird = birds[i];
                if (!TryGetBirdTexture(bird.Variant, out Texture2D texture, out int frameWidth, out int frameHeight))
                    continue;

                Vector2 position = bird.Pos;
                float rotation;
                int frame;

                CachedLink entry = null;
                if (bird.State == BirdState.Perched && (!byKey.TryGetValue(bird.Key, out entry) || entry.Rope == null))
                    continue;   // its wire is off screen (or not known here yet): nothing to draw

                if (bird.State == BirdState.Perched)
                {
                    position = PointAlong(entry.Rope, ClampU(entry.Rope, bird.U));
                    Vector2 tangent = Tangent(entry.Rope, bird.U);
                    rotation = tangent.X > 0.001f ? (float)Math.Atan2(tangent.Y, tangent.X) * 0.6f : 0f;
                    frame = BirdPerchFrame;
                }
                else
                {
                    rotation = MathHelper.Clamp(bird.Vel.Y * 0.08f * bird.Dir, -0.4f, 0.4f);
                    frame = BirdFlyFirstFrame + (bird.Age / 5 + i) % Math.Max(1, BirdFlyFrameCount);
                }

                frame = Math.Clamp(frame, 0, Math.Max(1, texture.Height / frameHeight) - 1);

                // Whole pixels, like the wire itself.
                position = new Vector2((float)Math.Round(position.X), (float)Math.Round(position.Y));
                Color light = Lighting.GetColor(Math.Clamp((int)(position.X / 16f), 0, Main.maxTilesX - 1), Math.Clamp((int)(position.Y / 16f), 0, Main.maxTilesY - 1));
                light *= Math.Clamp(bird.Alpha, 0f, 1f);
                var source = new Rectangle(0, frame * frameHeight, frameWidth, frameHeight);
                var origin = new Vector2(frameWidth / 2f, frameHeight + BirdFootLift);

                // Vanilla art faces left, so a bird that looks right is the flipped one (the other way round when BirdArtFacesLeft is off).
                bool flip = BirdArtFacesLeft ? bird.Dir > 0 : bird.Dir < 0;
                spriteBatch.Draw(texture, position - Main.screenPosition, source, light, rotation, origin, 1f, flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
            }
        }
    }

    // P11b: hands every melee swing box of the local player to the wire birds (they have 1 health, a swing that touches one asks the server to kill it).
    public class WireBirdMeleeHits : GlobalItem
    {
        public override void MeleeEffects(Item item, Player player, Rectangle hitbox)
        {
            if (Main.dedServ || player.whoAmI != Main.myPlayer)
                return;

            ModContent.GetInstance<PoleWireDrawSystem>().AddBirdMeleeHitbox(hitbox);
        }
    }

    // P11c: names a wire by value (its two poles and the connection points), the same on the server and every client (the PoleLink objects are
    // not shared between them, and a resynced link is a new object).
    public readonly struct WireKey : IEquatable<WireKey>
    {
        public readonly Point16 A;
        public readonly byte SlotA;
        public readonly Point16 B;
        public readonly byte SlotB;

        public WireKey(Point16 a, byte slotA, Point16 b, byte slotB)
        {
            A = a;
            SlotA = slotA;
            B = b;
            SlotB = slotB;
        }

        public WireKey(PoleLink link) : this(link.A, link.SlotA, link.B, link.SlotB)
        {
        }

        public bool Matches(PoleLink link) => link.A == A && link.SlotA == SlotA && link.B == B && link.SlotB == SlotB;

        public bool Equals(WireKey other) => A == other.A && SlotA == other.SlotA && B == other.B && SlotB == other.SlotB;

        public override bool Equals(object obj) => obj is WireKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(A.X, A.Y, SlotA, B.X, B.Y, SlotB);

        public void Write(System.IO.BinaryWriter writer)
        {
            writer.Write(A.X);
            writer.Write(A.Y);
            writer.Write(SlotA);
            writer.Write(B.X);
            writer.Write(B.Y);
            writer.Write(SlotB);
        }

        public static WireKey Read(System.IO.BinaryReader reader)
        {
            var a = new Point16(reader.ReadInt16(), reader.ReadInt16());
            byte slotA = reader.ReadByte();
            var b = new Point16(reader.ReadInt16(), reader.ReadInt16());
            byte slotB = reader.ReadByte();
            return new WireKey(a, slotA, b, slotB);
        }
    }

    // P11c: one bird as the join sync sends it.
    public struct WireBirdInfo
    {
        public int Id;
        public WireKey Key;
        public float T;
        public int Variant;
    }
}