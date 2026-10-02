using Factorraria.Common.Machines;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.Furnace
{
    public class FurnaceTileEntity : BaseMachine
    {
        public override int ValidTileType => TileID.Furnaces;
        protected override FuelTable AcceptedFuels => FurnaceRecipeRegistry.Fuels;
        public override RecipeBook Recipes => FurnaceRecipeRegistry.Book;
        protected override int OutputSlotCount => 1;

        protected override void OnAnimationFrameChanged(int newFrame, int previousFrame)
        {
            if (Main.netMode == NetmodeID.Server) return;

            if (!IsOnScreen(MachineCenter, 600f))
            {
                return;
            }

            if (Main.rand.NextBool(7))
            {
                Vector2 DustSpawnPosition = MachineCenter + new Vector2(-15f, -7f);
                Dust d = Dust.NewDustDirect(DustSpawnPosition, 20, 8, DustID.Torch, Main.rand.NextFloat(-5f, 5f), -1f, 100, default, 1.25f);
                d.noGravity = true;
                d.velocity *= 0.5f;
                d.velocity.Y -= 1.5f;
            }
        }
    }
}