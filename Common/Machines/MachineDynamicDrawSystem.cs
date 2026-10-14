using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace Factorraria.Common.Machines
{
    // Ticks and draws every machine that implements IMachineDynamicDraw. Same culling pattern as MachineOverhangSystem
    // (own loop in PostDrawTiles, one SpriteBatch begin), kept separate so the overhang system stays untouched.
    public class MachineDynamicDrawSystem : ModSystem
    {
        public override void PostUpdateEverything()
        {
            if (Main.dedServ)
                return;

            foreach (TileEntity entity in TileEntity.ByPosition.Values)
            {
                if (entity is IMachineDynamicDraw dynamic)
                    dynamic.DynamicUpdate();
            }
        }

        public override void PostDrawTiles()
        {
            if (Main.dedServ)
                return;

            SpriteBatch spriteBatch = Main.spriteBatch;
            bool batchBegun = false;

            foreach (TileEntity entity in TileEntity.ByPosition.Values)
            {
                if (entity is not IMachineDynamicDraw dynamic)
                    continue;

                if (!dynamic.TryGetDynamicBounds(out Vector2 center, out float radius) || !BaseMachine.IsOnScreen(center, radius))
                    continue;

                if (!batchBegun)
                {
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                    batchBegun = true;
                }

                dynamic.DynamicDraw(spriteBatch);
            }

            if (batchBegun)
                spriteBatch.End();
        }
    }
}