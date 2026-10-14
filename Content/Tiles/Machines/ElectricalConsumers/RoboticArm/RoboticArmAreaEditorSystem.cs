using Factorraria.Common.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.RoboticArm
{
    // Draws the working area of the Robotic Arm whose UI is open as a box over the world tiles, and lets the player drag it
    // (body = move, edges / corners = resize), snapped to tiles. Tiles outside the arm's reach are tinted red.
    // Two Game-scale interface layers: the overlay is drawn before the inventory (so the UI covers it), and the input runs
    // after the inventory and machine UI so it only grabs the cursor when no UI has it (Player.mouseInterface is already set then).
    public class RoboticArmAreaEditorSystem : ModSystem
    {
        // --- TUNABLES (plain fields so hot reload can change them) ---
        public float EdgeGrabPixels = 6f;                        // screen pixels either side of an edge that grab it
        public float BorderPixels = 2f;                          // border thickness on screen
        public Color InReachColor = new Color(90, 200, 255) * 0.2f;
        public Color OutOfReachColor = new Color(255, 70, 70) * 0.32f;
        public Color BorderColor = Color.White * 0.7f;
        public Color GrabColor = new Color(255, 230, 90);
        public Color MoveTint = Color.White * 0.1f;

        [Flags]
        enum AreaGrab { None = 0, Left = 1, Right = 2, Top = 4, Bottom = 8, Move = 16 }

        AreaGrab hoverGrab;
        AreaGrab dragGrab;
        RoboticArmTileEntity dragArm;
        Point dragStartTile;
        Rectangle dragStartArea;

        RoboticArmTileEntity OpenArm => ModContent.GetInstance<MachineUISystem>().OpenMachine as RoboticArmTileEntity;

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            // The later layer is inserted first so the earlier index stays valid.
            int mouseText = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
            if (mouseText != -1)
            {
                layers.Insert(mouseText, new LegacyGameInterfaceLayer("Factorraria: Robotic Arm Area Input", delegate
                {
                    HandleInput();
                    return true;
                }, InterfaceScaleType.Game));
            }

            int inventory = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Inventory"));
            if (inventory != -1)
            {
                layers.Insert(inventory, new LegacyGameInterfaceLayer("Factorraria: Robotic Arm Area", delegate
                {
                    DrawArea(Main.spriteBatch);
                    return true;
                }, InterfaceScaleType.Game));
            }
        }

        void HandleInput()
        {
            RoboticArmTileEntity arm = OpenArm;
            Player player = Main.LocalPlayer;

            // Closing the UI, or switching to another arm, ends any drag.
            if (arm == null || arm != dragArm)
                dragGrab = AreaGrab.None;

            if (arm == null)
            {
                hoverGrab = AreaGrab.None;
                return;
            }

            if (dragGrab != AreaGrab.None)
            {
                if (Main.mouseLeft)
                {
                    ApplyDrag(arm);
                    hoverGrab = dragGrab;
                    player.mouseInterface = true;
                    return;
                }

                dragGrab = AreaGrab.None;
            }

            hoverGrab = AreaGrab.None;
            if (player.mouseInterface)
                return; // the inventory, the machine panel or a slot already has the cursor

            AreaGrab grab = HitTest(arm, Main.MouseWorld);
            if (grab == AreaGrab.None)
                return;

            hoverGrab = grab;
            player.mouseInterface = true; // no swinging or placing while the cursor is on the box

            if (Main.mouseLeft && Main.mouseLeftRelease)
            {
                dragGrab = grab;
                dragArm = arm;
                dragStartTile = Main.MouseWorld.ToTileCoordinates();
                dragStartArea = new Rectangle(arm.AreaX, arm.AreaY, arm.AreaW, arm.AreaH);
                Main.mouseLeftRelease = false;
            }
        }

        AreaGrab HitTest(RoboticArmTileEntity arm, Vector2 mouse)
        {
            Rectangle tiles = arm.AreaTiles;
            float left = tiles.X * 16f;
            float top = tiles.Y * 16f;
            float right = tiles.Right * 16f;
            float bottom = tiles.Bottom * 16f;

            // A thin box must keep a middle that moves it, so the grab zone never exceeds a third of the box.
            float zoom = MathF.Max(Main.GameViewMatrix.ZoomMatrix.M11, 0.01f);
            float margin = MathF.Min(EdgeGrabPixels / zoom, MathF.Min(right - left, bottom - top) / 3f);

            if (mouse.X < left - margin || mouse.X > right + margin || mouse.Y < top - margin || mouse.Y > bottom + margin)
                return AreaGrab.None;

            AreaGrab grab = AreaGrab.None;
            if (mouse.X <= left + margin) grab |= AreaGrab.Left;
            else if (mouse.X >= right - margin) grab |= AreaGrab.Right;

            if (mouse.Y <= top + margin) grab |= AreaGrab.Top;
            else if (mouse.Y >= bottom - margin) grab |= AreaGrab.Bottom;

            return grab == AreaGrab.None ? AreaGrab.Move : grab;
        }

        // Always computed from the area as it was when the drag started, so clamping never accumulates.
        void ApplyDrag(RoboticArmTileEntity arm)
        {
            Point mouseTile = Main.MouseWorld.ToTileCoordinates();
            int dx = mouseTile.X - dragStartTile.X;
            int dy = mouseTile.Y - dragStartTile.Y;
            int reach = arm.ReachTiles;
            Rectangle start = dragStartArea;

            if (dragGrab == AreaGrab.Move)
            {
                int x = Math.Clamp(start.X + dx, -reach, reach - start.Width + 1);
                int y = Math.Clamp(start.Y + dy, -reach, reach - start.Height + 1);
                arm.SetArea(x, y, start.Width, start.Height);
                return;
            }

            // Inclusive edges, so a side can never cross the opposite one (the area stays at least 1x1).
            int left = start.X;
            int right = start.X + start.Width - 1;
            int top = start.Y;
            int bottom = start.Y + start.Height - 1;

            if (dragGrab.HasFlag(AreaGrab.Left)) left = Math.Clamp(left + dx, -reach, right);
            if (dragGrab.HasFlag(AreaGrab.Right)) right = Math.Clamp(right + dx, left, reach);
            if (dragGrab.HasFlag(AreaGrab.Top)) top = Math.Clamp(top + dy, -reach, bottom);
            if (dragGrab.HasFlag(AreaGrab.Bottom)) bottom = Math.Clamp(bottom + dy, top, reach);

            arm.SetArea(left, top, right - left + 1, bottom - top + 1);
        }

        void DrawArea(SpriteBatch spriteBatch)
        {
            RoboticArmTileEntity arm = OpenArm;
            if (arm == null)
                return;

            Rectangle tiles = arm.AreaTiles;
            Texture2D pixel = TextureAssets.MagicPixel.Value;

            for (int x = tiles.X; x < tiles.Right; x++)
            {
                for (int y = tiles.Y; y < tiles.Bottom; y++)
                {
                    Color color = arm.IsTileInReach(x, y) ? InReachColor : OutOfReachColor;
                    FillRect(spriteBatch, pixel, new Vector2(x * 16f, y * 16f), new Vector2(16f, 16f), color);
                }
            }

            Vector2 origin = new Vector2(tiles.X * 16f, tiles.Y * 16f);
            Vector2 size = new Vector2(tiles.Width * 16f, tiles.Height * 16f);
            float thickness = BorderPixels / MathF.Max(Main.GameViewMatrix.ZoomMatrix.M11, 0.01f);

            if (hoverGrab == AreaGrab.Move)
                FillRect(spriteBatch, pixel, origin, size, MoveTint);

            // Edges the cursor would grab (or is dragging) are highlighted.
            bool all = hoverGrab == AreaGrab.Move;
            DrawEdge(spriteBatch, pixel, origin, new Vector2(size.X, thickness), all || hoverGrab.HasFlag(AreaGrab.Top));
            DrawEdge(spriteBatch, pixel, origin + new Vector2(0f, size.Y - thickness), new Vector2(size.X, thickness), all || hoverGrab.HasFlag(AreaGrab.Bottom));
            DrawEdge(spriteBatch, pixel, origin, new Vector2(thickness, size.Y), all || hoverGrab.HasFlag(AreaGrab.Left));
            DrawEdge(spriteBatch, pixel, origin + new Vector2(size.X - thickness, 0f), new Vector2(thickness, size.Y), all || hoverGrab.HasFlag(AreaGrab.Right));
        }

        void DrawEdge(SpriteBatch spriteBatch, Texture2D pixel, Vector2 worldPosition, Vector2 size, bool grabbed)
        {
            FillRect(spriteBatch, pixel, worldPosition, size, grabbed ? GrabColor : BorderColor);
        }

        static void FillRect(SpriteBatch spriteBatch, Texture2D pixel, Vector2 worldPosition, Vector2 size, Color color)
        {
            spriteBatch.Draw(pixel, worldPosition - Main.screenPosition, null, color, 0f, Vector2.Zero, size, SpriteEffects.None, 0f);
        }
    }
}