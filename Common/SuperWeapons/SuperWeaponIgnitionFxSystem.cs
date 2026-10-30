using Factorraria.Common.Machines;
using Factorraria.Content.Tiles.SuperWeapons.MissileTank;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.SuperWeapons
{
    // Super weapons patch S4.1. CLIENT ONLY and cosmetic: while a tank's WeaponState is Igniting (single-player: the real state; client: the
    // replica, which the light update keeps current) fire and smoke come out of the three thruster nozzles, opposite to the facing, and the
    // thruster tiles glow. Placeholder look: dust only, the real flame art comes later. The sound and the camera shake come once from the
    // Ignition effect packet.
    public class SuperWeaponIgnitionFxSystem : ModSystem
    {
        readonly List<MissileTankTileEntity> ignitingTanks = new List<MissileTankTileEntity>();
        int scanTimer;
        int tick;

        public override void OnWorldLoad() => ignitingTanks.Clear();

        public override void OnWorldUnload() => ignitingTanks.Clear();

        public override void PostUpdateEverything()
        {
            if (Main.dedServ || Main.gameMenu)
                return;

            if (--scanTimer <= 0)
            {
                scanTimer = 10;
                ignitingTanks.Clear();
                foreach (TileEntity entity in TileEntity.ByID.Values)
                    if (entity is MissileTankTileEntity tank && tank.WeaponState == SuperWeaponState.Igniting)
                        ignitingTanks.Add(tank);
            }

            tick++;
            foreach (MissileTankTileEntity tank in ignitingTanks)
            {
                if (tank.WeaponState != SuperWeaponState.Igniting)
                    continue;

                Vector2 center = SuperWeaponSystem.TankCenter(tank);
                if (!BaseMachine.IsOnScreen(center, 600f))
                    continue;

                int facing = tank.WeaponFacing == 0 ? 1 : tank.WeaponFacing;

                // x of the nozzles: just outside the thruster column, on the side opposite the drill
                float nozzleX = facing > 0 ? (tank.Position.X - 1) * 16f - 8f : (tank.Position.X + SuperWeaponAssembly.TankWidth) * 16f + 24f;
                for (int row = 0; row < SuperWeaponAssembly.TankHeight; row++)
                {
                    Vector2 at = new Vector2(nozzleX, tank.Position.Y * 16f + row * 16f + 8f);

                    Vector2 velocity = new Vector2(-facing * Main.rand.NextFloat(4f, 8f), Main.rand.NextFloat(-0.6f, 0.6f));
                    Dust fire = Dust.NewDustPerfect(at, DustID.Torch, velocity, 0, default, Main.rand.NextFloat(1.6f, 2.6f));
                    fire.noGravity = true;

                    if (tick % 3 == 0)
                    {
                        Dust smoke = Dust.NewDustPerfect(at, DustID.Smoke, velocity * 0.5f, 100, default, 1.8f);
                        smoke.noGravity = true;
                    }

                    Lighting.AddLight(at, 1f, 0.6f, 0.2f);
                }
            }
        }
    }
}