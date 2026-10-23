using Microsoft.Xna.Framework;
using System.Collections.Concurrent;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // The cart events that make a sound. The numbers are part of the wire format (append only).
    public enum CartSoundKind : byte
    {
        Bump = 0,    // a cart hit a bumper track
        Collide = 1, // two carts collided
        Couple = 2,  // a free cart hooked onto a motor's chain
        Pour = 3,    // a terrarium cart poured or drained liquid
        PressurePlate = 4, // patch 9.4b: a cart tripped a pressure plate track (the server has no audio, so clients play the click)
        PassengerPickup = 5,  // cart passengers B: an NPC got into a cart (sound plus a puff of dust)
        PassengerRelease = 6  // cart passengers B: an NPC was thrown or dropped out of a cart
    }

    // Patch 9.6a (finding C8). Carts only simulate on the server (or in single-player), and a dedicated server has no audio, so a sound
    // played where the event happens is never heard in multiplayer. CartSound.Play is the one place every cart sound goes through:
    //   single-player      -> plays the sound right here
    //   dedicated server   -> sends CartSound to the players near the position (rate limited per kind, so a pile-up is not a flood)
    //   multiplayer client -> does nothing (clients never run the events; the packet below plays it)
    public static class CartSound
    {
        public static int NearbyTiles = 80;      // players further away than this do not get the packet
        public static int MinTicksBetween = 4;   // server: the same kind is sent at most once per this many ticks

        static uint[] lastSent = new uint[System.Enum.GetValues(typeof(CartSoundKind)).Length];

        public static void Play(CartSoundKind kind, Vector2 position)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            if (Main.netMode == NetmodeID.Server)
            {
                int index = (int)kind;
                if (index < 0 || index >= lastSent.Length)
                    return;

                uint now = Main.GameUpdateCount;
                if (lastSent[index] != 0 && now - lastSent[index] < (uint)MinTicksBetween)
                    return;
                lastSent[index] = now;

                FactorrariaNet.SendToNearby(position, NearbyTiles, FactorrariaPacket.CartSound, writer =>
                {
                    writer.Write((byte)kind);
                    writer.Write(position.X);
                    writer.Write(position.Y);
                });
                return;
            }

            PlayLocal(kind, position);
        }

        public static void ResetServerState()
        {
            for (int i = 0; i < lastSent.Length; i++)
                lastSent[i] = 0;
        }

        // Client and single-player: the real sound.
        public static void PlayLocal(CartSoundKind kind, Vector2 position)
        {
            if (Main.dedServ)
                return;

            switch (kind)
            {
                case CartSoundKind.Bump:
                    SoundEngine.PlaySound(SoundID.Item56, position); // placeholder bumper "boing"
                    break;
                case CartSoundKind.Collide:
                    SoundEngine.PlaySound(SoundID.Item53, position);
                    break;
                case CartSoundKind.Couple:
                    SoundEngine.PlaySound(SoundID.Item37, position);
                    break;
                case CartSoundKind.Pour:
                    SoundEngine.PlaySound(SoundID.SplashWeak, position);
                    break;
                case CartSoundKind.PressurePlate:
                    SoundEngine.PlaySound(SoundID.Mech, position); // vanilla's switch / pressure plate click
                    break;
                case CartSoundKind.PassengerPickup:
                    SoundEngine.PlaySound(SoundID.Grab, position); // placeholder "hop in" pop
                    PuffDust(position, 1f);
                    break;
                case CartSoundKind.PassengerRelease:
                    SoundEngine.PlaySound(SoundID.Item1, position); // placeholder "thrown out" whoosh
                    PuffDust(position, 1.6f);
                    break;
            }
        }

        // A small puff of smoke where a passenger got in or out. Dust is a client thing (a dedicated server never gets here), so it needs no sync.
        static void PuffDust(Vector2 position, float strength)
        {
            for (int i = 0; i < 10; i++)
            {
                Vector2 velocity = Main.rand.NextVector2Circular(2f, 1.2f) * strength + new Vector2(0f, -0.6f);
                Dust dust = Dust.NewDustPerfect(position + Main.rand.NextVector2Circular(10f, 6f), DustID.Smoke, velocity, 120, default, 1.3f);
                dust.noGravity = true;
            }
        }
    }

    public class CartSoundSystem : ModSystem
    {
        struct Pending
        {
            public CartSoundKind Kind;
            public Vector2 Position;
        }

        static ConcurrentQueue<Pending> incoming = new ConcurrentQueue<Pending>();

        public override void Load()
        {
            FactorrariaNet.Register(FactorrariaPacket.CartSound, PacketDirection.ToClient, HandleSound);
        }

        public override void OnWorldLoad() => Clear();

        public override void OnWorldUnload() => Clear();

        static void Clear()
        {
            while (incoming.TryDequeue(out _)) { }
            CartSound.ResetServerState();
        }

        static void HandleSound(System.IO.BinaryReader reader, int sender)
        {
            byte kind = reader.ReadByte();
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            if (kind > (byte)CartSoundKind.PassengerRelease)
                return;

            incoming.Enqueue(new Pending { Kind = (CartSoundKind)kind, Position = new Vector2(x, y) });
        }

        // Client, main thread.
        public override void PostUpdateEverything()
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            int budget = 8; // never more than a handful of sounds in one tick
            while (budget-- > 0 && incoming.TryDequeue(out Pending sound))
                CartSound.PlayLocal(sound.Kind, sound.Position);
        }
    }
}