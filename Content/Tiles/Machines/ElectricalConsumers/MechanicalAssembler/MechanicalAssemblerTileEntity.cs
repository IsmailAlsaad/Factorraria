using Factorraria.Common.Machines;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.MechanicalAssembler
{
    public class MechanicalAssemblerTileEntity : ElectricConsumerMachine
    {
        public override int ValidTileType => ModContent.TileType<MechanicalAssemblerTile>();
        public override RecipeBook Recipes => MechanicalAssemblerRecipeRegistry.Book;
        public override float PowerDemand => 200f;
        protected override int OutputSlotCount => 1;

        protected override void OnAnimationFrameChanged(int newFrame, int previousFrame)
        {
            if(newFrame == 0)
            {
                SoundEngine.PlaySound(SoundID.Tink with { PitchVariance = 0.35f, Volume = 0.15f },MachineCenter);

                for (int i = 0; i < Main.rand.Next(3, 5); i++)
                {
                    Vector2 DustSpawnPosition = MachineCenter + new Vector2(-8f, 0f);
                    Dust d = Dust.NewDustDirect(DustSpawnPosition, 13, 4, DustID.Stone, Main.rand.NextFloat(-5f, 5f), 0f, 100, default, Main.rand.NextFloat(0.8f, 1.2f));
                    d.noGravity = true;
                    d.velocity *= 0.3f;
                    d.velocity.Y += 1f;
                }
            }
        }
    }
}