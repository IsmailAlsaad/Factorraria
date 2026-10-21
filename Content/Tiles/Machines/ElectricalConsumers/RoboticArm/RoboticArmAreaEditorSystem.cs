using Factorraria.Common.Net;
using Factorraria.Common.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.RoboticArm
{
    // Draws the working area of the Robotic Arm whose UI is open as a 4x4 sprite sheet laid over the world tiles, with two
    // handles: one in the middle of the area (hold left click and drag to move it) and one on each of its four corners (drag to
    // resize). Everything snaps to tiles. Tiles outside the arm's reach are drawn from a second sheet (RoboticArmAreaInvalid).
    // Two Game-scale interface layers: the overlay is drawn before the inventory (so the UI covers it), and the input runs
    // after the inventory and machine UI so it only grabs the cursor when no UI has it (Player.mouseInterface is already set then).
    //
    // Art (all under Content/Tiles/Machines/ElectricalConsumers/RoboticArm/):
    //   RoboticArmArea.png                    64x64: 4x4 cells of 16x16, no padding. Each tile of the area draws one cell:
    //                                         [TL ][T  ][TR ][T1W]     T1W/M1W/B1W = top/middle/bottom of a 1-wide area
    //                                         [L  ][M  ][R  ][M1W]     L1H/M1H/R1H = left/middle/right of a 1-high area
    //                                         [BL ][B  ][BR ][B1W]     1x1         = a single-tile area
    //                                         [L1H][M1H][R1H][1x1]
    //   RoboticArmAreaInvalid.png             same 4x4 layout as RoboticArmArea.png; used for tiles the arm cannot reach
    //   RoboticArmArea_Move.png / _Move_Highlight.png       the move handle and its hovered version (drawn centred)
    //   RoboticArmArea_Resize.png / _Resize_Highlight.png   the resize handle and its hovered version (drawn centred; this is the top-right one, flipped for the other corners)
    public class RoboticArmAreaEditorSystem : ModSystem
    {
        // --- TUNABLES (plain fields so hot reload can change them) ---
        public Vector2 MoveHandleOffset = Vector2.Zero;                  // pixels, from the middle of the area to the move handle's centre
        public Vector2 ResizeHandleOffset = Vector2.Zero;                // pixels, from the area's top-right corner to the resize handle's centre (mirrored for the other corners)
        public string TexturePath = "Factorraria/Content/Tiles/Machines/ElectricalConsumers/RoboticArm/RoboticArmArea";

        enum Handle { None, Move, Resize }

        Asset<Texture2D> frameTexture;
        Asset<Texture2D> invalidTexture;
        Asset<Texture2D> moveTexture;
        Asset<Texture2D> moveHighlightTexture;
        Asset<Texture2D> resizeTexture;
        Asset<Texture2D> resizeHighlightTexture;
        bool texturesRequested;
        bool texturesFound;

        // The four corners as (-1 left / +1 right, -1 top / +1 bottom).
        readonly Point[] corners = { new Point(-1, -1), new Point(1, -1), new Point(-1, 1), new Point(1, 1) };

        Handle hoverHandle;
        Point hoverCorner;
        Handle dragHandle;
        Point dragCorner;
        RoboticArmTileEntity dragArm;
        Point dragStartTile;
        Rectangle dragStartArea;

        RoboticArmTileEntity OpenArm => ModContent.GetInstance<MachineUISystem>().OpenMachine as RoboticArmTileEntity;

        // True while the move or resize handle is held. The arm's UI hides its slots and power icon during that time.
        public bool DraggingHandle => dragHandle != Handle.None;

        // Patch 7b.5: the net layer (RoboticArmAreaNet) keeps an answer aside while this editor still drags the same arm.
        public bool IsDraggingArm(RoboticArmTileEntity arm) => dragHandle != Handle.None && dragArm == arm;

        // The arm whose UI was open last frame; a change means a UI was just opened (or the replica was rebound), so a multiplayer client
        // asks the server for the arm's real area (a replica that missed a broadcast, or an older snapshot, is corrected here).
        RoboticArmTileEntity lastOpenArm;

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

        // Requested on first use (not at load) so a missing PNG only disables the overlay and logs once, instead of crashing.
        bool EnsureTextures()
        {
            if (texturesRequested)
                return texturesFound;

            texturesRequested = true;
            bool found = ModContent.RequestIfExists<Texture2D>(TexturePath, out frameTexture, AssetRequestMode.ImmediateLoad);
            found &= ModContent.RequestIfExists<Texture2D>(TexturePath + "Invalid", out invalidTexture, AssetRequestMode.ImmediateLoad);
            found &= ModContent.RequestIfExists<Texture2D>(TexturePath + "_Move", out moveTexture, AssetRequestMode.ImmediateLoad);
            found &= ModContent.RequestIfExists<Texture2D>(TexturePath + "_Move_Highlight", out moveHighlightTexture, AssetRequestMode.ImmediateLoad);
            found &= ModContent.RequestIfExists<Texture2D>(TexturePath + "_Resize", out resizeTexture, AssetRequestMode.ImmediateLoad);
            found &= ModContent.RequestIfExists<Texture2D>(TexturePath + "_Resize_Highlight", out resizeHighlightTexture, AssetRequestMode.ImmediateLoad);

            texturesFound = found;
            if (!found)
                Mod.Logger.Warn("RoboticArmAreaEditorSystem: one or more area PNGs are missing (" + TexturePath + ", " + TexturePath + "Invalid, _Move, _Move_Highlight, _Resize, _Resize_Highlight); the area overlay is disabled.");

            return texturesFound;
        }

        static Vector2 MoveHandleCenter(RoboticArmAreaEditorSystem system, Rectangle tiles) =>
            new Vector2(tiles.X * 16f + tiles.Width * 8f, tiles.Y * 16f + tiles.Height * 8f) + system.MoveHandleOffset;

        // The offset is written for the top-right corner and mirrored for the others.
        static Vector2 ResizeHandleCenter(RoboticArmAreaEditorSystem system, Rectangle tiles, Point corner) =>
            new Vector2(corner.X < 0 ? tiles.X * 16f : tiles.Right * 16f, corner.Y < 0 ? tiles.Y * 16f : tiles.Bottom * 16f)
            + system.ResizeHandleOffset * new Vector2(corner.X, -corner.Y);

        static bool IsOver(Vector2 center, Texture2D texture, Vector2 mouse) =>
            MathF.Abs(mouse.X - center.X) <= texture.Width / 2f && MathF.Abs(mouse.Y - center.Y) <= texture.Height / 2f;

        void HandleInput()
        {
            RoboticArmTileEntity arm = OpenArm;
            Player player = Main.LocalPlayer;

            // Patch 7b.5: a UI was just opened for this arm: a multiplayer client asks for the real area (single-player: nothing is sent).
            if (arm != lastOpenArm)
            {
                lastOpenArm = arm;
                if (arm != null)
                    RoboticArmAreaNet.RequestRefresh(arm);
            }

            // Closing the UI, or switching to another arm, ends any drag. That drag is cancelled: the preview is thrown away by asking
            // the server for the real area (nothing is sent in single-player).
            if (dragHandle != Handle.None && (arm == null || arm != dragArm))
                RoboticArmAreaNet.RequestRefresh(dragArm);

            if (arm == null || arm != dragArm)
                dragHandle = Handle.None;

            if (arm == null || !EnsureTextures())
            {
                hoverHandle = Handle.None;
                dragHandle = Handle.None;
                return;
            }

            if (dragHandle != Handle.None)
            {
                if (Main.mouseLeft)
                {
                    ApplyDrag(arm);
                    hoverHandle = dragHandle;
                    hoverCorner = dragCorner;
                    player.mouseInterface = true;
                    return;
                }

                // Mouse released: the whole drag becomes ONE request (patch 7b.5). Single-player and the host send nothing.
                RoboticArmAreaNet.Commit(arm, dragStartArea);
                dragHandle = Handle.None;
            }

            hoverHandle = Handle.None;
            if (player.mouseInterface)
                return; // the inventory, the machine panel or a slot already has the cursor

            Handle handle = HitTest(arm, Main.MouseWorld, out Point corner);
            if (handle == Handle.None)
                return;

            hoverHandle = handle;
            hoverCorner = corner;
            player.mouseInterface = true; // no swinging or placing while the cursor is on a handle

            if (Main.mouseLeft && Main.mouseLeftRelease)
            {
                dragHandle = handle;
                dragCorner = corner;
                dragArm = arm;
                dragStartTile = Main.MouseWorld.ToTileCoordinates();
                dragStartArea = new Rectangle(arm.AreaX, arm.AreaY, arm.AreaW, arm.AreaH);
                Main.mouseLeftRelease = false;
            }
        }

        // A resize handle wins over the move handle; where several corners overlap (a tiny area) the one nearest the cursor wins.
        Handle HitTest(RoboticArmTileEntity arm, Vector2 mouse, out Point corner)
        {
            Rectangle tiles = arm.AreaTiles;
            corner = default;
            bool found = false;
            float best = float.MaxValue;

            foreach (Point candidate in corners)
            {
                Vector2 center = ResizeHandleCenter(this, tiles, candidate);
                float distance = Vector2.DistanceSquared(center, mouse);
                if (IsOver(center, resizeTexture.Value, mouse) && distance < best)
                {
                    best = distance;
                    corner = candidate;
                    found = true;
                }
            }

            if (found)
                return Handle.Resize;

            if (IsOver(MoveHandleCenter(this, tiles), moveTexture.Value, mouse))
                return Handle.Move;

            return Handle.None;
        }

        // Always computed from the area as it was when the drag started, so clamping never accumulates.
        void ApplyDrag(RoboticArmTileEntity arm)
        {
            Point mouseTile = Main.MouseWorld.ToTileCoordinates();
            int dx = mouseTile.X - dragStartTile.X;
            int dy = mouseTile.Y - dragStartTile.Y;
            int reach = arm.AreaLimit; // one tile past the reach: that ring is drawn red
            Rectangle start = dragStartArea;

            if (dragHandle == Handle.Move)
            {
                int x = Math.Clamp(start.X + dx, -reach, reach - start.Width + 1);
                int y = Math.Clamp(start.Y + dy, -reach, reach - start.Height + 1);
                arm.SetArea(x, y, start.Width, start.Height);
                return;
            }

            // Each corner moves its own two edges and keeps the opposite two where they were. Inclusive edges, so a side can
            // never cross the opposite one (the area stays at least 1x1).
            int left = start.X;
            int top = start.Y;
            int right = start.X + start.Width - 1;
            int bottom = start.Y + start.Height - 1;

            if (dragCorner.X > 0)
                right = Math.Clamp(right + dx, left, reach);
            else
                left = Math.Clamp(left + dx, -reach, right);

            if (dragCorner.Y < 0)
                top = Math.Clamp(top + dy, -reach, bottom);
            else
                bottom = Math.Clamp(bottom + dy, top, reach);

            arm.SetArea(left, top, right - left + 1, bottom - top + 1);
        }

        // Cell index (0 first, 1 middle, 2 last) along one axis of the sheet; a 1-tile-long axis uses the extra 4th cell (3).
        static int Slot(int index, int count) => count == 1 ? 3 : index == 0 ? 0 : index == count - 1 ? 2 : 1;

        void DrawArea(SpriteBatch spriteBatch)
        {
            RoboticArmTileEntity arm = OpenArm;
            if (arm == null || !EnsureTextures())
                return;

            Rectangle tiles = arm.AreaTiles;
            Texture2D frame = frameTexture.Value;
            Texture2D invalid = invalidTexture.Value;

            for (int x = tiles.X; x < tiles.Right; x++)
            {
                for (int y = tiles.Y; y < tiles.Bottom; y++)
                {
                    // Same cell of the same layout, taken from the invalid sheet when the arm cannot reach this tile.
                    Texture2D sheet = arm.IsTileInReach(x, y) ? frame : invalid;
                    int cell = sheet.Width / 4;
                    float scale = 16f / cell;

                    Vector2 position = new Vector2(x * 16f, y * 16f) - Main.screenPosition;
                    int column = Slot(x - tiles.X, tiles.Width);
                    int row = Slot(y - tiles.Y, tiles.Height);
                    Rectangle source = new Rectangle(column * cell, row * cell, cell, cell);
                    spriteBatch.Draw(sheet, position, source, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                }
            }

            DrawHandle(spriteBatch, MoveHandleCenter(this, tiles), moveTexture.Value, moveHighlightTexture.Value, hoverHandle == Handle.Move || dragHandle == Handle.Move);
            foreach (Point corner in corners)
            {
                bool active = (hoverHandle == Handle.Resize && hoverCorner == corner) || (dragHandle == Handle.Resize && dragCorner == corner);
                SpriteEffects flip = (corner.X < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None)
                    | (corner.Y > 0 ? SpriteEffects.FlipVertically : SpriteEffects.None);
                DrawHandle(spriteBatch, ResizeHandleCenter(this, tiles, corner), resizeTexture.Value, resizeHighlightTexture.Value, active, flip);
            }
        }

        static void DrawHandle(SpriteBatch spriteBatch, Vector2 center, Texture2D normal, Texture2D highlighted, bool active, SpriteEffects flip = SpriteEffects.None)
        {
            Texture2D texture = active ? highlighted : normal;
            spriteBatch.Draw(texture, center - Main.screenPosition, null, Color.White, 0f, texture.Size() / 2f, 1f, flip, 0f);
        }
    }
}