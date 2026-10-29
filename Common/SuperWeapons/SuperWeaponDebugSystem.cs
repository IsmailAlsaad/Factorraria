using Factorraria.Common.Machines;
using Factorraria.Content.Configs;
using Factorraria.Content.Tiles.SuperWeapons.MissileTank;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace Factorraria.Common.SuperWeapons
{
    // Super weapons patch S3.1: the EnableDebugs overlay of the missile assembly. CLIENT ONLY drawing; it reads the state the tank entity
    // already has (computed on the server / in single-player, received through the snapshot and light updates on a client).
    //   assembled   -> a green box around the whole 7 x 3 weapon and one text line
    //   incomplete  -> a red box where the first missing (or wrong) piece belongs and the text that says what is wrong
    // The tank's own 4 x 3 footprint is outlined in white either way. Remove with the other debug aids in S6 (it only draws while
    // EnableDebugs is on in the Factorraria mod config).
    public class SuperWeaponDebugSystem : ModSystem
    {
        public override void PostDrawTiles()
        {
            if (Main.dedServ || Main.gameMenu)
                return;

            FurnaceOffsetConfig config = ModContent.GetInstance<FurnaceOffsetConfig>();
            if (config == null || !config.EnableDebugs)
                return;

            SpriteBatch spriteBatch = Main.spriteBatch;
            bool begun = false;

            foreach (TileEntity entity in TileEntity.ByID.Values)
            {
                if (!(entity is MissileTankTileEntity tank))
                    continue;

                Vector2 tankTopLeft = new Vector2(tank.Position.X, tank.Position.Y) * 16f;
                Vector2 tankCenter = tankTopLeft + new Vector2(SuperWeaponAssembly.TankWidth, SuperWeaponAssembly.TankHeight) * 8f;
                if (!BaseMachine.IsOnScreen(tankCenter, 400f))
                    continue;

                if (!begun)
                {
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                    begun = true;
                }

                DrawTank(spriteBatch, tank, tankTopLeft);
            }

            if (begun)
                spriteBatch.End();
        }

        static void DrawTank(SpriteBatch spriteBatch, MissileTankTileEntity tank, Vector2 tankTopLeft)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle one = new Rectangle(0, 0, 1, 1);

            Rectangle tankRect = ScreenRect(tankTopLeft, SuperWeaponAssembly.TankWidth, SuperWeaponAssembly.TankHeight);
            Utils.DrawRect(spriteBatch, tankRect, Color.White * 0.8f);

            Color textColor;
            if (tank.IsAssembled)
            {
                // facing right: thruster column, tank, drill (7 tiles from tankX - 1); facing left: drill, tank, thruster column (from tankX - 2)
                int firstTileX = tank.WeaponFacing > 0 ? -1 : -SuperWeaponAssembly.DrillWidth;
                int weaponWidth = SuperWeaponAssembly.TankWidth + 1 + SuperWeaponAssembly.DrillWidth;
                Rectangle weaponRect = ScreenRect(tankTopLeft + new Vector2(firstTileX * 16f, 0f), weaponWidth, SuperWeaponAssembly.TankHeight);
                spriteBatch.Draw(pixel, weaponRect, one, Color.Lime * 0.12f);
                Utils.DrawRect(spriteBatch, weaponRect, Color.Lime);
                textColor = Color.Lime;
            }
            else
            {
                bool thrusterIssue = tank.WeaponIssue == AssemblyIssue.ThrusterMissing
                    || tank.WeaponIssue == AssemblyIssue.ThrusterRowOff
                    || tank.WeaponIssue == AssemblyIssue.ThrusterWrongFacing;
                int width = thrusterIssue ? 1 : SuperWeaponAssembly.DrillWidth;
                int height = thrusterIssue ? 1 : SuperWeaponAssembly.DrillHeight;

                Rectangle issueRect = ScreenRect(tankTopLeft + new Vector2(tank.WeaponIssueDx, tank.WeaponIssueDy) * 16f, width, height);
                spriteBatch.Draw(pixel, issueRect, one, new Color(255, 60, 60) * 0.3f);
                Utils.DrawRect(spriteBatch, issueRect, new Color(255, 80, 80));
                textColor = new Color(255, 120, 120);
            }

            string text = "[Missile] " + tank.WeaponState + ": " + tank.AssemblyText;
            Vector2 textPos = tankTopLeft - Main.screenPosition + new Vector2(0f, -22f);
            Utils.DrawBorderString(spriteBatch, text, textPos, textColor, 0.8f);
        }

        static Rectangle ScreenRect(Vector2 worldTopLeft, int widthTiles, int heightTiles)
        {
            Vector2 screen = worldTopLeft - Main.screenPosition;
            return new Rectangle((int)screen.X, (int)screen.Y, widthTiles * 16, heightTiles * 16);
        }
    }
}