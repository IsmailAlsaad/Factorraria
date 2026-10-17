using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace Factorraria.Common.Machines
{
    // Ticks and draws every machine that implements IMachineDynamicDraw. Same culling pattern as MachineOverhangSystem
    // (own loop in PostDrawTiles, one SpriteBatch begin), kept separate so the overhang system stays untouched.
    public class MachineDynamicDrawSystem : ModSystem
    {
        // The behind-tiles pass rides on the DrawNPCs(behindTiles: true) call, which vanilla makes before any tile is drawn.
        // Whether a SpriteBatch is already open at that point is learned on the first frame: if drawing throws, the pass switches to
        // its own Begin/End; if even that fails it switches itself off (the beams then simply draw in front, with the claw).
        bool behindUsesOwnBatch;
        bool behindDisabled;

        public override void Load()
        {
            if (Main.dedServ) return; // no screen on a dedicated server: the draw detour is never needed there
            On_Main.DrawNPCs += DrawBehindTilesPass;
        }

        public override void Unload()
        {
            if (Main.dedServ) return; // matches Load()
            On_Main.DrawNPCs -= DrawBehindTilesPass;
        }

        void DrawBehindTilesPass(On_Main.orig_DrawNPCs orig, Main self, bool behindTiles)
        {
            if (behindTiles && !behindDisabled && !Main.dedServ)
                DrawBehindTiles();

            orig(self, behindTiles);
        }

        void DrawBehindTiles()
        {
            SpriteBatch spriteBatch = Main.spriteBatch;

            if (!behindUsesOwnBatch)
            {
                try
                {
                    DrawBehindEntities(spriteBatch);
                }
                catch (InvalidOperationException)
                {
                    behindUsesOwnBatch = true;
                    Mod.Logger.Info("MachineDynamicDrawSystem: no SpriteBatch is open in the behind-tiles pass, using its own.");
                }

                return;
            }

            bool began = false;
            try
            {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                began = true;
                DrawBehindEntities(spriteBatch);
            }
            catch (InvalidOperationException)
            {
                behindDisabled = true;
                Mod.Logger.Warn("MachineDynamicDrawSystem: the behind-tiles pass cannot draw; switched off.");
            }
            finally
            {
                if (began)
                    spriteBatch.End();
            }
        }

        void DrawBehindEntities(SpriteBatch spriteBatch)
        {
            foreach (TileEntity entity in TileEntity.ByPosition.Values)
            {
                if (entity is not IMachineDynamicDraw dynamic)
                    continue;

                if (!dynamic.TryGetDynamicBounds(out Vector2 center, out float radius) || !BaseMachine.IsOnScreen(center, radius))
                    continue;

                dynamic.DynamicDrawBehindTiles(spriteBatch);
            }
        }
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