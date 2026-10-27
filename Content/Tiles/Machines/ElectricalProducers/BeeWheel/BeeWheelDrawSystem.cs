using Factorraria.Common.Machines;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.ElectricalProducers.BeeWheel
{
    // Draws every Bee Wheel's wheel and bees in front of its base. The base is drawn in MachineOverhangSystem.PostDrawTiles and
    // MachineDynamicDrawSystem.PostDrawTiles runs BEFORE it (tModLoader runs systems in order of their full type name, Common... before
    // Content...), so a normal IMachineDynamicDraw.DynamicDraw would be painted over by the base. This system lives in a Content
    // namespace, so it runs after both.
    public class BeeWheelDrawSystem : ModSystem
    {
        public override void PostDrawTiles()
        {
            if (Main.dedServ)
                return;

            SpriteBatch spriteBatch = Main.spriteBatch;
            bool batchBegun = false;

            foreach (TileEntity entity in TileEntity.ByPosition.Values)
            {
                if (entity is not BeeWheelTileEntity wheel)
                    continue;

                if (!wheel.TryGetDynamicBounds(out Vector2 center, out float radius) || !BaseMachine.IsOnScreen(center, radius))
                    continue;

                if (!batchBegun)
                {
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                    batchBegun = true;
                }

                wheel.DrawFront(spriteBatch);
            }

            if (batchBegun)
                spriteBatch.End();
        }
    }
}