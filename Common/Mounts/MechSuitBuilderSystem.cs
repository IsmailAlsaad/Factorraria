using Factorraria.Content.Items.Mounts;
using Factorraria.Content.Tiles.Machines.ElectricalConsumers.RoboticArm;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace Factorraria.Common.Mounts
{
    // Mech Suit build tool, step 1 (docs/mech_suit_builder/ROADMAP.md): selecting a region. Client-only: the selection is never saved or synced.
    //   Shift + right-click drag  = select a region of tiles (it is saved and stays on screen).
    //   Shift + right-click, no drag, while a selection exists = clear it. (With no selection it selects that one tile.)
    // Everything only works while the local player is in the suit. The selection is drawn with the Robotic Arm's area sheet
    // (RoboticArmArea.png, 4x4 cells of 16x16, see RoboticArmAreaEditorSystem for the cell layout). There is no size text.
    // Holding Shift in the suit also takes the cursor (mouseInterface), so the held item is not used; the next steps need Shift + left click.
    public class MechSuitBuilderSystem : ModSystem
    {
        // --- TUNABLES (plain fields so hot reload can change them) ---
        public int MaxSelectionWidth = 64;
        public int MaxSelectionHeight = 64;
        public string TexturePath = "Factorraria/Content/Tiles/Machines/ElectricalConsumers/RoboticArm/RoboticArmArea";

        /// <summary>The saved selection in world tile coordinates, or null.</summary>
        public Rectangle? Selection { get; private set; }

        bool dragging;
        bool dragMoved;
        Point dragStart;
        Rectangle dragRect;

        Asset<Texture2D> frameTexture;
        bool texturesRequested;
        bool texturesFound;

        static bool ShiftHeld => Main.keyState.PressingShift();

        // True while the local player is in the suit (the only time any of this works).
        static bool Ready(out Player player)
        {
            player = Main.LocalPlayer;
            return !Main.dedServ && !Main.gameMenu && player != null && player.active && !player.dead
                && player.GetModPlayer<MechSuitPlayer>().IsActive;
        }

        public override void OnWorldUnload()
        {
            Selection = null;
            dragging = false;
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            // The later layer is inserted first so the earlier index stays valid.
            int mouseText = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
            if (mouseText != -1)
            {
                layers.Insert(mouseText, new LegacyGameInterfaceLayer("Factorraria: Mech Suit Builder Input", delegate
                {
                    HandleInput();
                    return true;
                }, InterfaceScaleType.Game));
            }

            int inventory = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Inventory"));
            if (inventory != -1)
            {
                layers.Insert(inventory, new LegacyGameInterfaceLayer("Factorraria: Mech Suit Builder Selection", delegate
                {
                    DrawSelection(Main.spriteBatch);
                    return true;
                }, InterfaceScaleType.Game));
            }
        }

        // Requested on first use so a missing PNG only disables the overlay and logs once, instead of crashing.
        bool EnsureTexture()
        {
            if (texturesRequested)
                return texturesFound;

            texturesRequested = true;
            texturesFound = ModContent.RequestIfExists<Texture2D>(TexturePath, out frameTexture, AssetRequestMode.ImmediateLoad);
            if (!texturesFound)
                Mod.Logger.Warn("MechSuitBuilderSystem: " + TexturePath + ".png is missing; the selection overlay is disabled.");

            return texturesFound;
        }

        void HandleInput()
        {
            if (!Ready(out Player player))
            {
                dragging = false;
                return;
            }

            bool shift = ShiftHeld;

            if (dragging)
            {
                if (Main.mouseRight && shift)
                {
                    UpdateDragRect();
                    player.mouseInterface = true;
                    return;
                }

                // Released the button: keep the rectangle. Let go of Shift first: cancel and keep the old selection.
                if (!Main.mouseRight)
                {
                    if (!dragMoved && Selection.HasValue)
                        Selection = null;
                    else
                        Selection = dragRect;
                }

                dragging = false;
            }

            if (!shift)
                return;

            if (!player.mouseInterface && Main.mouseRight && Main.mouseRightRelease)
            {
                dragging = true;
                dragMoved = false;
                dragStart = Main.MouseWorld.ToTileCoordinates();
                dragRect = new Rectangle(dragStart.X, dragStart.Y, 1, 1);
                Main.mouseRightRelease = false;
            }

            player.mouseInterface = true; // Shift in the suit never swings or places the held item
        }

        // The end tile is limited so the rectangle never gets bigger than the maximum or leaves the world.
        void UpdateDragRect()
        {
            Point end = Main.MouseWorld.ToTileCoordinates();
            end.X = Math.Clamp(end.X, dragStart.X - (MaxSelectionWidth - 1), dragStart.X + (MaxSelectionWidth - 1));
            end.Y = Math.Clamp(end.Y, dragStart.Y - (MaxSelectionHeight - 1), dragStart.Y + (MaxSelectionHeight - 1));
            end.X = Math.Clamp(end.X, 0, Main.maxTilesX - 1);
            end.Y = Math.Clamp(end.Y, 0, Main.maxTilesY - 1);

            if (end != dragStart)
                dragMoved = true;

            int left = Math.Min(dragStart.X, end.X);
            int top = Math.Min(dragStart.Y, end.Y);
            dragRect = new Rectangle(left, top, Math.Abs(end.X - dragStart.X) + 1, Math.Abs(end.Y - dragStart.Y) + 1);
        }

        void DrawSelection(SpriteBatch spriteBatch)
        {
            if (!Ready(out _) || !EnsureTexture())
                return;

            Rectangle? shown = dragging ? dragRect : Selection;
            if (!shown.HasValue)
                return;

            Rectangle tiles = shown.Value;
            Texture2D sheet = frameTexture.Value;
            int cell = sheet.Width / 4;
            float scale = 16f / cell;

            for (int x = tiles.X; x < tiles.Right; x++)
            {
                for (int y = tiles.Y; y < tiles.Bottom; y++)
                {
                    Vector2 position = new Vector2(x * 16f, y * 16f) - Main.screenPosition;
                    int column = RoboticArmAreaEditorSystem.Slot(x - tiles.X, tiles.Width);
                    int row = RoboticArmAreaEditorSystem.Slot(y - tiles.Y, tiles.Height);
                    Rectangle source = new Rectangle(column * cell, row * cell, cell, cell);
                    spriteBatch.Draw(sheet, position, source, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                }
            }
        }
    }
}