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
        ShieldBreak = 0,  // the Wall of Flesh's stone shield broke (patch S1.2)
        Ignition = 1,     // a primed missile's boosters just lit (patch S4.1): sound and camera shake at the tank
        Launch = 2,       // the missile left the ground and its parts vanished (patch S4.2): bang and camera shake at the weapon
        Impact = 3,       // the missile hit the wall (patch S4.3): the big explosion at the missile (ShieldBreak follows from BreakShield)
        Fizzle = 4        // the missile ended without a hit (patch S4.3): a small puff at the missile
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

                case SuperWeaponEffectKind.Ignition:
                    // Placeholder sounds. The flames are drawn every tick by SuperWeaponIgnitionFxSystem from the tank's own state.
                    SoundEngine.PlaySound(SoundID.Item34 with { Volume = 1f, Pitch = -0.3f }, position);
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.6f, Pitch = -0.8f }, position);
                    Main.instance.CameraModifiers.Add(new PunchCameraModifier(position, Main.rand.NextVector2CircularEdge(1f, 1f), 3f, 20f, 120, 2000f, "FactorrariaIgnition"));
                    break;

                case SuperWeaponEffectKind.Launch:
                    // Placeholder sounds and dust; the flames of the flight itself come from the missile projectile.
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1f, Pitch = -0.6f }, position);
                    SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.8f, Pitch = -0.4f }, position);
                    for (int i = 0; i < 40; i++)
                    {
                        Dust smoke = Dust.NewDustPerfect(position + Main.rand.NextVector2Circular(60f, 24f), DustID.Smoke, Main.rand.NextVector2Circular(4f, 3f), 100, default, 2.2f);
                        smoke.noGravity = true;
                    }

                    Main.instance.CameraModifiers.Add(new PunchCameraModifier(position, Main.rand.NextVector2CircularEdge(1f, 1f), 8f, 12f, 60, 2500f, "FactorrariaLaunch"));
                    break;

                case SuperWeaponEffectKind.Impact:
                    // The big one (S4.4): sounds and the camera here, everything that plays over time (fireball, shockwave ring, rising column,
                    // secondary blasts, screen flash, lingering smoke) lives in SuperWeaponExplosionFxSystem. The stone debris comes with the ShieldBreak effect.
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1f, Pitch = -1f }, position);
                    SoundEngine.PlaySound(SoundID.Item62 with { Volume = 1f, Pitch = -0.8f }, position);
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.8f, Pitch = -0.4f }, position);
                    ModContent.GetInstance<SuperWeaponExplosionFxSystem>().Start(position);
                    Main.instance.CameraModifiers.Add(new PunchCameraModifier(position, Main.rand.NextVector2CircularEdge(1f, 1f), 24f, 9f, 90, 4500f, "FactorrariaImpact"));
                    break;

                case SuperWeaponEffectKind.Fizzle:
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.4f, Pitch = 0.2f }, position);
                    for (int i = 0; i < 20; i++)
                    {
                        Dust puff = Dust.NewDustPerfect(position + Main.rand.NextVector2Circular(24f, 16f), i % 2 == 0 ? DustID.Smoke : DustID.Torch, Main.rand.NextVector2Circular(3f, 3f), 100, default, 1.6f);
                        puff.noGravity = true;
                    }

                    break;
            }
        }

        // The shield breaking (S6.2): the sounds and the camera here; the real show (the art chopped into flying shards, the dust burst and the
        // falling dust curtain) is WallShieldBreakFxSystem.
        static void ShieldBreak(Vector2 position)
        {
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1f, Pitch = -0.5f }, position);
            SoundEngine.PlaySound(SoundID.Tink with { Volume = 1f, Pitch = -0.7f }, position);
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.8f, Pitch = -0.9f }, position);

            ModContent.GetInstance<WallShieldBreakFxSystem>().Start(position);

            Main.instance.CameraModifiers.Add(new PunchCameraModifier(position, Main.rand.NextVector2CircularEdge(1f, 1f), 18f, 8f, 70, 3500f, "FactorrariaShieldBreak"));
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
            if (kind > (byte)SuperWeaponEffectKind.Fizzle)
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