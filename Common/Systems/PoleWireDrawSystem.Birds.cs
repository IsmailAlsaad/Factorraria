using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace Factorraria.Common.Systems
{
    // Client only (power poles patch P11). Little birds that now and then fly in, land on a hanging wire, look around, peck, and fly off
    // again when something moves close, when the wire flashes (a pole powering up), or when night, rain or a strong wind comes.
    // Purely cosmetic: nothing here is saved, sent or read by any gameplay code, the server never runs it, and every client rolls its own
    // birds (like the sparks). A bird only exists while its link is on screen (the same visibility test as the rope), so an off-screen wire
    // costs nothing. A perched bird is stored as a fractional node index 'U' of the live rope and re-read every tick, so it rides the wire
    // as it swings, and its landing and take-off give the rope a small kick. The rest of the class is in PoleWireDrawSystem.cs and
    // PoleWireDrawSystem.Sparks.cs.
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
        public int BirdPeckFrame = 4;                 // there is no head-down frame, so a peck is the perched frame tipped forward by BirdPeckTilt
        public float BirdPeckTilt = 0.25f;            // radians, toward the way the bird faces
        public int BirdFlyFirstFrame = 0;             // flying: this frame and the next BirdFlyFrameCount - 1, in a loop
        public int BirdFlyFrameCount = 4;
        public float BirdFootLift = -1f;               // pixels the bottom of the frame sits above the wire's centre (raise it if the feet sink into the wire)
        public int MaxBirds = 5;                      // alive at once, in the whole view
        public float BirdSpawnSeconds = 1f;          // on average one new bird this often while a wire is on screen and there is room
        public float BirdMaxWind = 0.6f;              // |Main.windSpeedCurrent| above this: no new birds, and the perched ones leave (0.6 is about 30 mph)
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
        public int BirdLeaveMaxTicks = 300;           // a bird that is leaving is removed after this long even if it is still on screen

        enum BirdState : byte { Arriving, Perched, Leaving }

        class WireBird
        {
            public BirdState State;
            public PoleLink Link;       // the wire it flies to or sits on (null once it is leaving for good)
            public float U;             // fractional node index on that wire's rope
            public Vector2 Pos;         // world position (kept up to date in every state, so a take-off starts from the right place)
            public Vector2 Vel;
            public int Dir = 1;         // 1 faces right, -1 faces left
            public int Age;             // ticks in the current state
            public int Timer;           // ticks until the next idle action
            public int Life;            // ticks until a perched bird leaves on its own
            public bool Pecking;
            public int Variant;
        }

        readonly List<WireBird> birds = new List<WireBird>();
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

        // ---------------------------------------------------------------- update (called from PostUpdateEverything, after UpdateSparks)

        void UpdateBirds()
        {
            PoleLinkSystem links = PoleLinkSystem.Instance;
            if (!Birds || links.Links.Count == 0)
            {
                birds.Clear();
                return;
            }

            EnsureCache(links);
            GetVisibleRect(out Rectangle visible);
            bool allowed = BirdsAllowed();

            for (int i = birds.Count - 1; i >= 0; i--)
            {
                if (!UpdateBird(birds[i], visible, allowed))
                    birds.RemoveAt(i);
            }

            if (allowed && quietTicks >= StartupQuietTicks && birds.Count < MaxBirds && Main.rand.NextFloat() < 1f / Math.Max(1f, BirdSpawnSeconds * 60f))
                TrySpawnBird(visible);
        }

        // Day, no rain, not too windy. Cheap, so it is asked once per tick.
        bool BirdsAllowed()
        {
            return Main.dayTime && !Main.raining && Math.Abs(Main.windSpeedCurrent) <= BirdMaxWind;
        }

        // False: remove the bird.
        bool UpdateBird(WireBird bird, Rectangle visible, bool allowed)
        {
            bird.Age++;

            if (bird.State == BirdState.Leaving)
            {
                bird.Vel.Y -= 0.025f;
                bird.Vel.X *= 1.005f;
                bird.Pos += bird.Vel;
                bird.Dir = bird.Vel.X >= 0f ? 1 : -1;

                Rectangle area = visible;
                area.Inflate(64, 64);
                return bird.Age <= BirdLeaveMaxTicks && area.Contains((int)bird.Pos.X, (int)bird.Pos.Y);
            }

            // Its wire: gone (cut, or the poles are missing) means it takes off where it is; off screen means it is simply dropped.
            if (bird.Link == null || !byLink.TryGetValue(bird.Link, out CachedLink entry))
            {
                TakeOff(bird, null);
                return true;
            }

            PoleRope rope = entry.Rope;
            if (rope == null)
                return false;

            if (bird.State == BirdState.Arriving)
            {
                Vector2 target = PointAlong(rope, ClampU(rope, bird.U));
                Vector2 delta = target - bird.Pos;
                float distance = delta.Length();

                if (distance <= 3f)
                {
                    bird.State = BirdState.Perched;
                    bird.Age = 0;
                    bird.Pos = target;
                    bird.Timer = Main.rand.Next(40, 120);
                    bird.Life = Main.rand.Next(Math.Min(BirdMinPerchTicks, BirdMaxPerchTicks), Math.Max(BirdMinPerchTicks, BirdMaxPerchTicks) + 1);
                    Kick(rope, bird.U, LandKick);
                    return true;
                }

                // Slow down for the last stretch so it lands instead of overshooting.
                float speed = Math.Clamp(distance * 0.05f, 0.7f, Math.Max(0.8f, BirdFlySpeed));
                bird.Vel = delta / distance * speed;
                bird.Pos += bird.Vel;
                if (Math.Abs(delta.X) > 2f)
                    bird.Dir = delta.X >= 0f ? 1 : -1;

                if (!allowed || bird.Age > 900)
                    TakeOff(bird, null);

                return true;
            }

            // Perched: ride the wire.
            bird.U = ClampU(rope, bird.U);
            bird.Pos = PointAlong(rope, bird.U);

            Vector2 tangent = Tangent(rope, bird.U);
            bool tooSteep = Math.Abs(tangent.X) < 0.001f || Math.Abs(tangent.Y / tangent.X) > BirdMaxSlope * 2f;
            if (tooSteep || bird.Age >= bird.Life || (!allowed && Main.rand.NextBool(120)))
            {
                TakeOff(bird, null);
                return true;
            }

            if (FindScare(bird.Pos, out Vector2 from))
            {
                TakeOff(bird, from);
                return true;
            }

            if (--bird.Timer <= 0)
            {
                // Idle behaviour: look the other way, peck for a moment, or just sit.
                if (bird.Pecking)
                {
                    bird.Pecking = false;
                    bird.Timer = Main.rand.Next(50, 200);
                }
                else
                {
                    int roll = Main.rand.Next(100);
                    if (roll < 30)
                    {
                        bird.Dir = -bird.Dir;
                        bird.Timer = Main.rand.Next(30, 120);
                    }
                    else if (roll < 65)
                    {
                        bird.Pecking = true;
                        bird.Timer = Main.rand.Next(14, 36);
                    }
                    else
                    {
                        bird.Timer = Main.rand.Next(60, 180);
                    }
                }
            }

            return true;
        }

        // A moving player or NPC close enough to scare a bird at 'position'. 'from' is where it is, so the bird flees the other way.
        bool FindScare(Vector2 position, out Vector2 from)
        {
            float radiusSquared = BirdScareRadius * BirdScareRadius;
            float speedSquared = BirdScareSpeed * BirdScareSpeed;
            float closeSquared = radiusSquared * 0.35f * 0.35f;

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];
                if (!player.active || player.dead || player.ghost)
                    continue;

                float distanceSquared = Vector2.DistanceSquared(player.Center, position);
                if (distanceSquared <= closeSquared || (distanceSquared <= radiusSquared && player.velocity.LengthSquared() >= speedSquared))
                {
                    from = player.Center;
                    return true;
                }
            }

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.lifeMax <= 5 || npc.velocity.LengthSquared() < speedSquared)
                    continue;

                if (Vector2.DistanceSquared(npc.Center, position) <= radiusSquared)
                {
                    from = npc.Center;
                    return true;
                }
            }

            from = default;
            return false;
        }

        // Starts a take-off from the bird's current spot. 'from' (may be null) is what scared it.
        void TakeOff(WireBird bird, Vector2? from)
        {
            if (bird.Link != null && byLink.TryGetValue(bird.Link, out CachedLink entry) && entry.Rope != null && bird.State == BirdState.Perched)
                Kick(entry.Rope, bird.U, -TakeoffKick);

            float side = from.HasValue ? Math.Sign(bird.Pos.X - from.Value.X) : 0f;
            if (side == 0f)
                side = Main.rand.NextBool() ? 1f : -1f;

            bird.State = BirdState.Leaving;
            bird.Link = null;
            bird.Age = 0;
            bird.Pecking = false;
            bird.Vel = new Vector2(side * (1.4f + 0.8f * Main.rand.NextFloat()), -(1.6f + 0.8f * Main.rand.NextFloat()));
            bird.Dir = side > 0f ? 1 : -1;
        }

        // Every bird on this link takes off (the wire just flashed). Called from the Sparks partial.
        void ScareBirds(PoleLink link)
        {
            for (int i = 0; i < birds.Count; i++)
            {
                WireBird bird = birds[i];
                if (bird.State != BirdState.Leaving && ReferenceEquals(bird.Link, link))
                    TakeOff(bird, null);
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

        // The margin keeps a bird off the pole ends (in whole nodes, at least 2).
        int BirdMarginNodes(PoleRope rope)
        {
            return Math.Max(2, (int)Math.Ceiling(BirdPoleMarginPixels / Math.Max(1f, rope.SegmentLength)));
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

        // ---------------------------------------------------------------- spawning

        void TrySpawnBird(Rectangle visible)
        {
            if (cache.Count == 0)
                return;

            // A random link that is on screen and has its rope; a few tries so a screen with one wire still lands birds.
            int start = Main.rand.Next(cache.Count);
            for (int n = 0; n < cache.Count; n++)
            {
                CachedLink entry = cache[(start + n) % cache.Count];
                if (entry.Rope == null || !entry.Resolved)
                    continue;

                PoleRope rope = entry.Rope;
                int margin = BirdMarginNodes(rope);
                int lastNode = rope.Count - 1;
                if (lastNode - 2 * margin < 1)
                    continue;

                for (int attempt = 0; attempt < 6; attempt++)
                {
                    float u = margin + Main.rand.NextFloat() * (lastNode - 2 * margin);
                    Vector2 perch = PointAlong(rope, u);
                    Vector2 tangent = Tangent(rope, u);

                    if (Math.Abs(tangent.X) < 0.001f || Math.Abs(tangent.Y / tangent.X) > BirdMaxSlope)
                        continue;

                    if (!visible.Contains((int)perch.X, (int)perch.Y) || perch.Y / 16f >= Main.worldSurface)
                        continue;

                    if (IsSpotTaken(entry.Link, u, rope.SegmentLength))
                        continue;

                    // Fly in from just outside the screen on a random side, a little above the perch.
                    float side = Main.rand.NextBool() ? 1f : -1f;
                    Vector2 origin = new Vector2(side > 0f ? visible.Right + 48f : visible.Left - 48f, perch.Y - (40f + 100f * Main.rand.NextFloat()));
                    birds.Add(new WireBird
                    {
                        State = BirdState.Arriving,
                        Link = entry.Link,
                        U = u,
                        Pos = origin,
                        Dir = side > 0f ? -1 : 1,
                        Variant = Main.rand.Next(Math.Max(1, BirdNpcIds == null ? 1 : BirdNpcIds.Length))
                    });
                    return;
                }
            }
        }

        bool IsSpotTaken(PoleLink link, float u, float segmentLength)
        {
            float minNodes = BirdSpacingPixels / Math.Max(1f, segmentLength);
            for (int i = 0; i < birds.Count; i++)
            {
                WireBird other = birds[i];
                if (other.State != BirdState.Leaving && ReferenceEquals(other.Link, link) && Math.Abs(other.U - u) < minNodes)
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

                if (bird.State == BirdState.Perched && bird.Link != null && byLink.TryGetValue(bird.Link, out CachedLink entry) && entry.Rope != null)
                {
                    position = PointAlong(entry.Rope, ClampU(entry.Rope, bird.U));
                    Vector2 tangent = Tangent(entry.Rope, bird.U);
                    rotation = tangent.X > 0.001f ? (float)Math.Atan2(tangent.Y, tangent.X) * 0.6f : 0f;
                    frame = bird.Pecking ? BirdPeckFrame : BirdPerchFrame;
                    if (bird.Pecking)
                        rotation += BirdPeckTilt * bird.Dir;   // clockwise (positive) tips a right-facing bird's head down
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
                var source = new Rectangle(0, frame * frameHeight, frameWidth, frameHeight);
                var origin = new Vector2(frameWidth / 2f, frameHeight + BirdFootLift);

                // Vanilla art faces left, so a bird that looks right is the flipped one (the other way round when BirdArtFacesLeft is off).
                bool flip = BirdArtFacesLeft ? bird.Dir > 0 : bird.Dir < 0;
                spriteBatch.Draw(texture, position - Main.screenPosition, source, light, rotation, origin, 1f, flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
            }
        }
    }
}