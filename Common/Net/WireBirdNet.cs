using Factorraria.Common.Systems;
using Factorraria.Content.Tiles.Machines.ElectricalConsumers.PowerPoles;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // Wire birds, multiplayer (power poles patch P11c; the client side is in Common/Systems/PoleWireDrawSystem.Birds.cs).
    // AUTHORITY: the server (and single player, which runs this same code) owns the birds: which exist, which wire and where on it, how
    // long they stay, when they take off and when they die. Nothing is saved. A client only shows them and runs the cosmetic parts
    // itself (the arrival flight, idle turning, the chirps, the rope kicks, the fade-out).
    // A bird sits at a fraction T (0 = end A, 1 = end B) of a wire named by WireKey (the two poles and their slots), so the position does not
    // depend on a rope: a client maps T to a node of its own rope, the server uses the resting curve (PoleWireDrawSystem.RestPoint).
    // Packets (all cosmetic, a lost one only loses a bird): WireBirdSpawn / WireBirdLeave / WireBirdDie, server -> every client (a few per
    // minute), WireBirdHitRequest and WireBirdScareRequest, client -> server. The join sync provider "wire-birds" gives a joining client
    // the birds that exist. In single player the "send to clients" step calls the local client directly.
    public class WireBirdNetSystem : ModSystem
    {
        class ServerBird
        {
            public int Id;
            public WireKey Key;
            public float T;
            public int Variant;
            public int Age;            // ticks since it spawned (its arrival flight counts: ArriveTicks)
            public int ArriveTicks;    // how long it is treated as flying in (0 = it was already sitting there); not scared or retired before that
            public int Life;           // age at which it takes off on its own
            public bool Leaving;
            public int LeaveAge;       // ticks since it took off; it stays hittable for a while, then it is forgotten
        }

        readonly List<ServerBird> birds = new List<ServerBird>();
        readonly List<PoleLink> candidates = new List<PoleLink>();
        int nextId = 1;
        int lastLinkVersion = -1;
        int startupTicks;

        static PoleWireDrawSystem Config => ModContent.GetInstance<PoleWireDrawSystem>();

        public override void Load()
        {
            JoinSync.Register("wire-birds", WriteAll, ReadAll);
            FactorrariaNet.Register(FactorrariaPacket.WireBirdSpawn, PacketDirection.ToClient, HandleSpawn);
            FactorrariaNet.Register(FactorrariaPacket.WireBirdLeave, PacketDirection.ToClient, HandleLeave);
            FactorrariaNet.Register(FactorrariaPacket.WireBirdDie, PacketDirection.ToClient, HandleDie);
            FactorrariaNet.Register(FactorrariaPacket.WireBirdHitRequest, PacketDirection.ToServer, HandleHitRequest);
            FactorrariaNet.Register(FactorrariaPacket.WireBirdScareRequest, PacketDirection.ToServer, HandleScareRequest);
        }

        public override void OnWorldLoad() => Reset();

        public override void OnWorldUnload() => Reset();

        void Reset()
        {
            birds.Clear();
            nextId = 1;
            lastLinkVersion = -1;
            startupTicks = 0;
        }

        // ---------------------------------------------------------------- requests (client side, or single player)

        // "My weapon hit bird <id> while it was at <position> facing <dir>." In single player the server handler runs right here.
        public static void RequestHit(int id, Vector2 position, int dir)
        {
            FactorrariaNet.SendRequest(FactorrariaPacket.WireBirdHitRequest, writer =>
            {
                writer.Write(id);
                writer.Write(position.X);
                writer.Write(position.Y);
                writer.Write((sbyte)(dir >= 0 ? 1 : -1));
            });
        }

        // "This wire flashed (a pole powered up or down): scare its birds."
        public static void RequestScare(WireKey key)
        {
            FactorrariaNet.SendRequest(FactorrariaPacket.WireBirdScareRequest, writer => key.Write(writer));
        }

        // ---------------------------------------------------------------- the server simulation

        public override void PostUpdateEverything()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient || Main.gameMenu || Main.gamePaused)
                return;

            ServerTick();
        }

        void ServerTick()
        {
            PoleWireDrawSystem config = Config;
            if (!config.Birds)
            {
                birds.Clear();
                return;
            }

            PoleLinkSystem links = PoleLinkSystem.Instance;
            if (startupTicks < config.StartupQuietTicks)
                startupTicks++;

            bool linksChanged = links.Version != lastLinkVersion;
            lastLinkVersion = links.Version;

            for (int i = birds.Count - 1; i >= 0; i--)
            {
                ServerBird bird = birds[i];
                bird.Age++;

                if (bird.Leaving)
                {
                    if (++bird.LeaveAge > config.BirdLeaveMaxTicks + config.BirdFadeTicks + 60)
                        birds.RemoveAt(i);

                    continue;
                }

                // Its wire was cut (or a pole is gone): it takes off.
                if (linksChanged && !TryGetEnds(bird.Key, out _, out _))
                {
                    Leave(bird, Main.rand.NextBool() ? 1 : -1);
                    continue;
                }

                // Still flying in (the clients animate that): nothing scares or retires it yet.
                if (bird.Age < bird.ArriveTicks)
                    continue;

                if (bird.Age >= bird.Life)
                {
                    Leave(bird, Main.rand.NextBool() ? 1 : -1);
                    continue;
                }

                if ((bird.Age + bird.Id) % 4 == 0 && TryGetPosition(bird, out Vector2 position) && FindScare(config, position, out Vector2 from))
                    Leave(bird, from.X > position.X ? -1 : 1);
            }

            int players = 0;
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                if (IsLivePlayer(Main.player[i]))
                    players++;
            }

            // The old rate was one bird every BirdSpawnSeconds for the one local player; with more players the world rolls more often.
            if (players > 0 && startupTicks >= config.StartupQuietTicks && links.Links.Count > 0
                && Main.rand.NextFloat() < players / Math.Max(1f, config.BirdSpawnSeconds * 60f))
                TrySpawn(config, links);
        }

        static bool IsLivePlayer(Player player) => player != null && player.active && !player.dead && !player.ghost;

        // Both ends of a wire as they hang at rest (no sway), false when the wire or one of its poles does not exist.
        bool TryGetEnds(WireKey key, out Vector2 a, out Vector2 b)
        {
            a = default;
            b = default;
            foreach (PoleLink link in PoleLinkSystem.Instance.Links)
            {
                if (key.Matches(link))
                    return TryGetEnds(link, out a, out b);
            }

            return false;
        }

        static bool TryGetEnds(PoleLink link, out Vector2 a, out Vector2 b)
        {
            a = default;
            b = default;
            if (!PoleLinkSystem.TryGetPole(link.A, out PowerPoleTileEntityBase poleA) || !PoleLinkSystem.TryGetPole(link.B, out PowerPoleTileEntityBase poleB))
                return false;

            if (link.SlotA >= poleA.LinkPointCount || link.SlotB >= poleB.LinkPointCount)
                return false;

            a = poleA.GetLinkPointWorld(link.SlotA);
            b = poleB.GetLinkPointWorld(link.SlotB);
            return true;
        }

        bool TryGetPosition(ServerBird bird, out Vector2 position)
        {
            position = default;
            if (!TryGetEnds(bird.Key, out Vector2 a, out Vector2 b))
                return false;

            PoleWireDrawSystem.RestPoint(a, b, bird.T, out position, out _);
            return true;
        }

        // A moving player or NPC close enough to scare a bird at 'position'. 'from' is where it is, so the bird flees the other way.
        static bool FindScare(PoleWireDrawSystem config, Vector2 position, out Vector2 from)
        {
            float radiusSquared = config.BirdScareRadius * config.BirdScareRadius;
            float speedSquared = config.BirdScareSpeed * config.BirdScareSpeed;
            float closeSquared = radiusSquared * 0.35f * 0.35f;

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];
                if (!IsLivePlayer(player))
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

        // Picks one live player and one wire near that player, uniformly at random among the wires there, and lands a bird on a flat part of
        // it. Birds spawn day and night, in any weather and wind; the only rule is no underground (the spot must be above the world surface).
        void TrySpawn(PoleWireDrawSystem config, PoleLinkSystem links)
        {
            int count = 0;
            int chosen = -1;
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                if (IsLivePlayer(Main.player[i]) && Main.rand.Next(++count) == 0)
                    chosen = i;
            }

            if (chosen < 0)
                return;

            Player player = Main.player[chosen];
            int halfWidth = (int)(config.BirdSpawnRangeTilesX * 16f);
            int halfHeight = (int)(config.BirdSpawnRangeTilesY * 16f);
            var nearBox = new Rectangle((int)player.Center.X - halfWidth, (int)player.Center.Y - halfHeight, halfWidth * 2, halfHeight * 2);

            // Sometimes the bird is one that "was already there": it sits on a wire outside the near box (about the screen) but inside the
            // far box (about two screens), with no flight, so it looks like it flew in before the player came. A client that happens to have
            // that wire on screen (zoomed out) still shows the flight, a client that does not just has it sitting there (ApplyBirdSpawn).
            bool preperched = Main.rand.NextFloat() < config.BirdPreperchedChance;
            Rectangle box = nearBox;
            if (preperched)
            {
                float factor = Math.Max(1f, config.BirdPreperchRangeFactor);
                int farWidth = (int)(halfWidth * factor);
                int farHeight = (int)(halfHeight * factor);
                box = new Rectangle((int)player.Center.X - farWidth, (int)player.Center.Y - farHeight, farWidth * 2, farHeight * 2);
            }

            // Room for another bird in that area?
            int near = 0;
            foreach (ServerBird other in birds)
            {
                if (!other.Leaving && TryGetPosition(other, out Vector2 otherPosition) && box.Contains((int)otherPosition.X, (int)otherPosition.Y))
                    near++;
            }

            if (near >= config.MaxBirds)
                return;

            candidates.Clear();
            foreach (PoleLink link in links.Links)
            {
                if (!TryGetEnds(link, out Vector2 a, out Vector2 b))
                    continue;

                float chord = Vector2.Distance(a, b);
                float reach = links.SagReach(chord) + 16f;
                var bounds = new Rectangle((int)(Math.Min(a.X, b.X) - reach), (int)(Math.Min(a.Y, b.Y) - reach), (int)(Math.Abs(a.X - b.X) + 2f * reach), (int)(Math.Abs(a.Y - b.Y) + 2f * reach));
                if (bounds.Intersects(box))
                    candidates.Add(link);
            }

            // Tried in a uniformly random order, so any wire can be the one (a random start and then "the next one" favoured some wires).
            while (candidates.Count > 0)
            {
                int pick = Main.rand.Next(candidates.Count);
                PoleLink link = candidates[pick];
                candidates[pick] = candidates[candidates.Count - 1];
                candidates.RemoveAt(candidates.Count - 1);

                if (!TryGetEnds(link, out Vector2 a, out Vector2 b))
                    continue;

                var key = new WireKey(link);
                float chord = Vector2.Distance(a, b);
                float length = chord + links.SlackExtra(chord);
                float margin = MathHelper.Clamp(config.BirdPoleMarginPixels / Math.Max(1f, length), 0f, 0.45f);

                for (int attempt = 0; attempt < 8; attempt++)
                {
                    float t = margin + Main.rand.NextFloat() * (1f - 2f * margin);
                    PoleWireDrawSystem.RestPoint(a, b, t, out Vector2 point, out Vector2 tangent);

                    if (Math.Abs(tangent.X) < 0.001f || Math.Abs(tangent.Y / tangent.X) > config.BirdMaxSlope)
                        continue;

                    if (!box.Contains((int)point.X, (int)point.Y) || point.Y / 16f >= Main.worldSurface)
                        continue;

                    if (preperched && nearBox.Contains((int)point.X, (int)point.Y))
                        continue;

                    if (IsSpotTaken(key, t, length, config.BirdSpacingPixels))
                        continue;

                    // A new bird comes in from just outside the screen on a random side. The server does not know the client's screen, so it
                    // takes the spawn box edge as that point, and gives the bird the time that distance needs at BirdFlySpeed (plus slack).
                    int side = Main.rand.NextBool() ? 1 : -1;
                    int arriveTicks = 0;
                    if (!preperched)
                    {
                        float dx = Math.Abs(player.Center.X + side * (halfWidth + 80f) - point.X);
                        float distance = (float)Math.Sqrt(dx * dx + 90f * 90f);
                        float speed = Math.Max(0.5f, config.BirdFlySpeed);
                        arriveTicks = (int)(((Math.Max(0f, distance - 20f * speed) / speed) + 20f * speed) * Math.Max(1f, config.BirdArriveSlack)) + 30;
                    }

                    var bird = new ServerBird
                    {
                        Id = nextId++,
                        Key = key,
                        T = t,
                        Variant = Main.rand.Next(Math.Max(1, config.BirdNpcIds == null ? 1 : config.BirdNpcIds.Length)),
                        ArriveTicks = arriveTicks,
                        Life = arriveTicks + Main.rand.Next(Math.Min(config.BirdMinPerchTicks, config.BirdMaxPerchTicks), Math.Max(config.BirdMinPerchTicks, config.BirdMaxPerchTicks) + 1)
                    };

                    birds.Add(bird);
                    EmitSpawn(bird, side);
                    return;
                }
            }
        }

        bool IsSpotTaken(WireKey key, float t, float length, float spacingPixels)
        {
            foreach (ServerBird other in birds)
            {
                if (!other.Leaving && other.Key.Equals(key) && Math.Abs(other.T - t) * length < spacingPixels)
                    return true;
            }

            return false;
        }

        void Leave(ServerBird bird, int side)
        {
            bird.Leaving = true;
            bird.LeaveAge = 0;
            EmitLeave(bird, side);
        }

        // ---------------------------------------------------------------- requests (server side)

        // A client says its weapon hit a bird. Birds have 1 health: the bird dies, the vanilla bird NPC of its kind is spawned and killed in
        // the same call (so its gore, blood and sound play for everyone through the normal NPC sync) and every client removes the sprite.
        void HandleHitRequest(BinaryReader reader, int sender)
        {
            int id = reader.ReadInt32();
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            int dir = reader.ReadSByte() >= 0 ? 1 : -1;

            ServerBird bird = birds.Find(other => other.Id == id);
            if (bird == null || float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y))
                return;

            // Where the client saw the bird when it was hit (it may be flying in or away, far from its spot on the wire). Kept inside the
            // world, and the asker must be near it.
            var position = new Vector2(Math.Clamp(x, 16f, Main.maxTilesX * 16f - 16f), Math.Clamp(y, 16f, Main.maxTilesY * 16f - 16f));

            PoleWireDrawSystem config = Config;
            if (sender >= 0 && sender < Main.maxPlayers && Vector2.Distance(Main.player[sender].Center, position) > config.BirdHitRangeTiles * 16f)
                return;

            birds.Remove(bird);
            EmitDie(bird);

            if (config.BirdNpcIds == null || config.BirdNpcIds.Length == 0)
                return;

            int type = config.BirdNpcIds[Math.Clamp(bird.Variant, 0, config.BirdNpcIds.Length - 1)];
            int index = NPC.NewNPC(new EntitySource_Misc("FactorrariaWireBird"), (int)position.X, (int)position.Y, type);
            if (index >= 0 && index < Main.maxNPCs)
            {
                NPC npc = Main.npc[index];
                npc.direction = dir;
                npc.spriteDirection = dir;
                npc.StrikeInstantKill();
            }
        }

        // A client saw the wire flash: every bird sitting on it takes off.
        void HandleScareRequest(BinaryReader reader, int sender)
        {
            WireKey key = WireKey.Read(reader);
            foreach (ServerBird bird in birds)
            {
                if (!bird.Leaving && bird.Key.Equals(key))
                    Leave(bird, Main.rand.NextBool() ? 1 : -1);
            }
        }

        // ---------------------------------------------------------------- sending (server, or single player)

        void EmitSpawn(ServerBird bird, int side)
        {
            if (Main.netMode == NetmodeID.Server)
            {
                FactorrariaNet.Broadcast(FactorrariaPacket.WireBirdSpawn, writer =>
                {
                    writer.Write(bird.Id);
                    bird.Key.Write(writer);
                    writer.Write(bird.T);
                    writer.Write((byte)bird.Variant);
                    writer.Write((sbyte)side);
                    writer.Write(true);
                });
            }
            else if (!Main.dedServ)
            {
                Config.ClientBirdSpawn(bird.Id, bird.Key, bird.T, bird.Variant, side, true);
            }
        }

        void EmitLeave(ServerBird bird, int side)
        {
            if (Main.netMode == NetmodeID.Server)
            {
                FactorrariaNet.Broadcast(FactorrariaPacket.WireBirdLeave, writer =>
                {
                    writer.Write(bird.Id);
                    writer.Write((sbyte)side);
                });
            }
            else if (!Main.dedServ)
            {
                Config.ClientBirdLeave(bird.Id, side);
            }
        }

        void EmitDie(ServerBird bird)
        {
            if (Main.netMode == NetmodeID.Server)
                FactorrariaNet.Broadcast(FactorrariaPacket.WireBirdDie, writer => writer.Write(bird.Id));
            else if (!Main.dedServ)
                Config.ClientBirdDie(bird.Id);
        }

        // ---------------------------------------------------------------- client handlers

        void HandleSpawn(BinaryReader reader, int sender)
        {
            int id = reader.ReadInt32();
            WireKey key = WireKey.Read(reader);
            float t = reader.ReadSingle();
            int variant = reader.ReadByte();
            int side = reader.ReadSByte();
            bool fresh = reader.ReadBoolean();
            Config.ClientBirdSpawn(id, key, t, variant, side, fresh);
        }

        void HandleLeave(BinaryReader reader, int sender)
        {
            int id = reader.ReadInt32();
            int side = reader.ReadSByte();
            Config.ClientBirdLeave(id, side);
        }

        void HandleDie(BinaryReader reader, int sender)
        {
            Config.ClientBirdDie(reader.ReadInt32());
        }

        // ---------------------------------------------------------------- join sync ("wire-birds")

        // Server: the birds that sit on a wire now (birds already leaving are not worth sending).
        void WriteAll(BinaryWriter writer)
        {
            int count = 0;
            foreach (ServerBird bird in birds)
            {
                if (!bird.Leaving)
                    count++;
            }

            writer.Write((ushort)count);
            foreach (ServerBird bird in birds)
            {
                if (bird.Leaving)
                    continue;

                writer.Write(bird.Id);
                bird.Key.Write(writer);
                writer.Write(bird.T);
                writer.Write((byte)bird.Variant);
            }
        }

        // Client: replaces the local birds (they just appear on their wires, no flight).
        void ReadAll(BinaryReader reader)
        {
            int count = reader.ReadUInt16();
            var infos = new List<WireBirdInfo>(count);
            for (int i = 0; i < count; i++)
            {
                int id = reader.ReadInt32();
                WireKey key = WireKey.Read(reader);
                float t = reader.ReadSingle();
                int variant = reader.ReadByte();
                infos.Add(new WireBirdInfo { Id = id, Key = key, T = t, Variant = variant });
            }

            Config.ClientBirdsReplaceAll(infos);
        }
    }
}