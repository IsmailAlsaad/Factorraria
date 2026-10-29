using Factorraria.Common.SuperWeapons;
using Microsoft.Xna.Framework;
using System.Collections.Concurrent;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // The super weapon effects that clients draw and hear. The numbers are part of the wire format (append only).
    public enum SuperWeaponEffectKind : byte
    {
        ShieldBreak = 0   // the Wall of Flesh's stone shield broke (patch S1.2)
    }

    // Super weapons patch S1.2. The shield state itself is synced with the wall NPC (S1.1); this is only the cosmetic burst. The server (or
    // single-player) calls SendEffect where the event happens, and:
    //   single-player      -> plays it right here
    //   dedicated server   -> broadcasts SuperWeaponEffect to every client (a dedicated server has no audio, no dust, no camera)
    //   multiplayer client -> does nothing (the packet below plays it)
    public static class SuperWeaponNet
    {
        public static void SendEffect(SuperWeaponEffectKind kind, Vector2 position)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            if (Main.netMode == NetmodeID.Server)
            {
                FactorrariaNet.Broadcast(FactorrariaPacket.SuperWeaponEffect, writer =>
                {
                    writer.Write((byte)kind);
                    writer.Write(position.X);
                    writer.Write(position.Y);
                });
                return;
            }

            PlayLocal(kind, position);
        }

        // Client and single-player: the real effect.
        public static void PlayLocal(SuperWeaponEffectKind kind, Vector2 position)
        {
            if (Main.dedServ)
                return;

            switch (kind)
            {
                case SuperWeaponEffectKind.ShieldBreak:
                    ShieldBreak(position);
                    break;
            }
        }

        // Stone debris along the whole wall, a boom plus a stone crack (placeholder sounds), and a camera punch that fades with distance.
        static void ShieldBreak(Vector2 position)
        {
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1f, Pitch = -0.5f }, position);
            SoundEngine.PlaySound(SoundID.Tink with { Volume = 1f, Pitch = -0.7f }, position);

            WallShieldDrawSystem draw = ModContent.GetInstance<WallShieldDrawSystem>();
            draw.TryGetBodySpan(position, out float top, out float bottom);

            for (int i = 0; i < 120; i++)
            {
                Vector2 at = new Vector2(position.X + Main.rand.NextFloat(-70f, 70f), Main.rand.NextFloat(top, bottom));
                Vector2 velocity = Main.rand.NextVector2Circular(7f, 5f);
                Dust dust = Dust.NewDustPerfect(at, DustID.Stone, velocity, 0, default, Main.rand.NextFloat(1.2f, 2.1f));
                dust.noGravity = Main.rand.NextBool(3);
            }

            for (int i = 0; i < 30; i++)
            {
                Vector2 at = new Vector2(position.X + Main.rand.NextFloat(-50f, 50f), Main.rand.NextFloat(top, bottom));
                Dust dust = Dust.NewDustPerfect(at, DustID.Smoke, Main.rand.NextVector2Circular(3f, 3f), 120, default, 1.6f);
                dust.noGravity = true;
            }

            Main.instance.CameraModifiers.Add(new PunchCameraModifier(position, Main.rand.NextVector2CircularEdge(1f, 1f), 14f, 8f, 45, 3000f, "FactorrariaShieldBreak"));
        }
    }

    public class SuperWeaponNetSystem : ModSystem
    {
        struct Pending
        {
            public SuperWeaponEffectKind Kind;
            public Vector2 Position;
        }

        static ConcurrentQueue<Pending> incoming = new ConcurrentQueue<Pending>();

        public override void Load()
        {
            FactorrariaNet.Register(FactorrariaPacket.SuperWeaponEffect, PacketDirection.ToClient, HandleEffect);
        }

        public override void OnWorldLoad() => Clear();

        public override void OnWorldUnload() => Clear();

        static void Clear()
        {
            while (incoming.TryDequeue(out _)) { }
        }

        static void HandleEffect(System.IO.BinaryReader reader, int sender)
        {
            byte kind = reader.ReadByte();
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            if (kind > (byte)SuperWeaponEffectKind.ShieldBreak)
                return;

            incoming.Enqueue(new Pending { Kind = (SuperWeaponEffectKind)kind, Position = new Vector2(x, y) });
        }

        // Client, main thread.
        public override void PostUpdateEverything()
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            int budget = 4;
            while (budget-- > 0 && incoming.TryDequeue(out Pending effect))
                SuperWeaponNet.PlayLocal(effect.Kind, effect.Position);
        }
    }
}