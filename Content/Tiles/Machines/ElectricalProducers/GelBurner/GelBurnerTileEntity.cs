using Factorraria.Common.Machines;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace Factorraria.Content.Tiles.Machines.GelBurner
{
    public class GelBurnerTileEntity : ElectricProducerMachine
    {
        public override int ValidTileType => TileID.SteampunkBoiler;
        public override float PowerSupply => 500f;

        // The fuel table lives in GelBurnerRecipeRegistry (shared with the Steampunk motor cart).
        // The burner uses 1 unit per tick, so gel = 3 seconds, coal = 8 seconds.
        public static FuelTable GelFuels => GelBurnerRecipeRegistry.Fuels;
        protected override FuelTable AcceptedFuels => GelFuels;

        public override void Update()
        {
            base.Update();

            // isWorking == true is determined when the generator has valid & enough fuel to burn & product slot is not full, so count its power output
            // isOn is set to false by the PowerNetwork not the machine when the grid is overloaded, so stop consuming fuel and turn off, but you could still be working!
            // i.e. have enough fuel to work once the grid is not overloaded
            // Supply only counts while there is gel to burn
            isWorking = Fuel.HasFuelAvailable(InputSlots);

            if (!isOn || !isWorking)
            {
                return;
            }

            Fuel.TryEnsureFuel(InputSlots);   // take a gel if the burner is empty
            Fuel.Consume();                   // burn 1 unit this tick
        }

        protected override void OnAnimationFrameChanged(int newFrame, int previousFrame)
        {
            if (Main.netMode == NetmodeID.Server) return;
            
            if(!IsOnScreen(MachineCenter,600f))
            {
                return;
            }

            if (Main.rand.NextBool(3))
            {
                Vector2 DustSpawnPosition = MachineCenter + new Vector2(-23f, 14f);
                Dust d = Dust.NewDustDirect(DustSpawnPosition, 13, 4, DustID.Torch, Main.rand.NextFloat(-5f,5f), -1f, 100, default, 1.5f);
                d.noGravity = true;
                d.velocity *= 0.3f;
                d.velocity.Y -= 0.5f;
            }

            if (newFrame != 3)
            {
                return;
            }

            float wind = Main.WindForVisuals; // roughly -1.2..1.2, positive = blowing right
            Vector2 velocity = new Vector2(
                wind * 1.5f + Main.rand.NextFloat(-0.2f, 0.2f), // wind sets direction and strength
                -Main.rand.NextFloat(0.6f, 1.2f));              // upward lift

            int type = GoreID.ChimneySmoke1 + Main.rand.Next(3); // 3 smoke variants
            Vector2 CloudSpawnPosition = MachineCenter + new Vector2(20f, -20f);

            //if (!Main.rand.NextBool(2))
            //{
            //    return;
            //}

            int index1 = Gore.NewGore(new EntitySource_TileEntity(this), CloudSpawnPosition, velocity, type, Main.rand.NextFloat(0.8f, 1.2f));

            if (index1 >= 0 && index1 < Main.maxGore)
            {
                Gore g = Main.gore[index1];
                g.timeLeft = 100;// Main.rand.Next(80,120);   // default is 600
                g.alpha = Main.rand.Next(0, 50);    // 0 = opaque, 255 = invisible
                g.scale = Main.rand.NextFloat(0.6f,0.8f);
                //g.rotation = 0f;
                g.velocity *= 0.8f;
            }

            if (!Main.rand.NextBool(2))
            {
                return;
            }

            int index2 = Gore.NewGore(new EntitySource_TileEntity(this), CloudSpawnPosition + new Vector2(20f,10f), velocity, type, Main.rand.NextFloat(1.8f, 2.2f));

            if (index2 >= 0 && index2 < Main.maxGore)
            {
                Gore g = Main.gore[index2];
                g.timeLeft = 120;// Main.rand.Next(80,120);   // default is 600
                g.alpha = Main.rand.Next(180, 240);    // 0 = opaque, 255 = invisible
                g.scale = Main.rand.NextFloat(1.5f, 1.2f);
                //g.rotation += 1f;
                g.velocity *= 0.8f;
            }
        }
    }
}
