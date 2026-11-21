using Block = Factorraria.Common.Mounts.MechSuitBlueprint.Block;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Terraria.UI;
using Factorraria.Content.Tiles.Machines.ElectricalConsumers.RoboticArm;
using Terraria.ID;
using Factorraria.Content.Items.Mounts;

namespace Factorraria.Common.Mounts
{
    // Mech Suit build tool, step 2 (docs/mech_suit_builder/ROADMAP.md): the placement hologram. Client-only, nothing is synced, saved or built.
    // Patch M3: the blueprint scan and the tile / wall -> item lookup moved to MechSuitBlueprint (shared with the server build job).
    // Design change 2026-11-20: this hologram and its box are the ONLY thing that stays on screen with a saved selection (the source area is
    // not drawn any more); it is hidden while a new selection is being dragged.
    // While the local player is in the suit, holds Shift and has a saved selection (MechSuitBuilderSystem.Selection), this draws:
    //   * the selected region's tiles and walls at the cursor (the cursor tile is the centre) as real tile / wall textures at about 50% alpha,
    //   * the placement box around that region (RoboticArmArea.png, or RoboticArmAreaInvalid.png on cells that hold a red block).
    // The blueprint (what the region holds) is read from the world once per selection. The state of every block at the destination
    // (free / already built / blocked / out of material) is recomputed when the cursor tile changes and every EvaluateIntervalTicks.
    // Block states:
    //   built       destination already holds the same block -> not drawn
    //   collision   destination holds a different block      -> red, ignored until it is cleared
    //   no material the local inventory ran out of the item   -> red; materials run out nearest-first from the cursor (no build order)
    // Rules: a tile with no placeable item (grass) is skipped. A multi-tile object counts as ONE item and is only copied when its whole
    // footprint is inside the selection. Chests and machines are copied as plain objects (placed empty; since patch M5 the job builds them too,
// and built / blocked is decided by MechSuitBlueprint.Matches / StateOf, the same rule the job uses). Slopes are drawn as full blocks,
    // half blocks as half blocks, paint is not shown, liquids and wires are not copied.
    public class MechSuitHologramSystem : ModSystem
    {
        // --- TUNABLES (plain fields so hot reload can change them) ---
        public float HologramAlpha = 0.5f;
        public Color RedTint = new Color(255, 70, 70);
        public int EvaluateIntervalTicks = 10;
        public string TexturePath = "Factorraria/Content/Tiles/Machines/ElectricalConsumers/RoboticArm/RoboticArmArea";

        enum BlockState : byte { Free, Built, Collision, NoMaterial }

        readonly List<Block> blocks = new();
        readonly List<int> candidates = new();
        readonly Dictionary<int, int> remaining = new();
        // Copies already sent to the server (patch M4). Client only: each is scanned from the local world at the click, the same way the server
        // reads it, and drawn until the build job ends. Blocks that exist at the destination are not drawn.
        class PlacedCopy
        {
            public Point Origin;
            public readonly List<Block> Blocks = new();
        }

        public int PlacedGraceTicks = 300;      // a placed copy waits this long for the server to confirm a job before it is dropped
        readonly List<PlacedCopy> placedCopies = new();
        uint lastPlacedTick;
        bool sawBuilding;
        readonly Dictionary<int, int> placedHave = new();

        BlockState[] states = Array.Empty<BlockState>();
        byte[] cellState = Array.Empty<byte>();
        int[] groupTotal = Array.Empty<int>();
        int[] groupMatch = Array.Empty<int>();
        int[] groupConflict = Array.Empty<int>();
        bool[] cellRed = Array.Empty<bool>();
        int blueprintWidth;
        int blueprintHeight;

        int builtVersion = -1;
        bool evaluated;
        Point lastCursor;
        uint lastEvaluate;

        Asset<Texture2D> frameTexture;
        Asset<Texture2D> invalidTexture;
        bool texturesRequested;
        bool texturesFound;

        public override void OnWorldUnload()
        {
            ClearBlueprint();
        }

        public override void Unload()
        {
            ClearBlueprint();
        }

        void ClearBlueprint()
        {
            blocks.Clear();
            placedCopies.Clear();
            builtVersion = -1;
            evaluated = false;
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int inventory = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Inventory"));
            if (inventory == -1)
                return;

            layers.Insert(inventory, new LegacyGameInterfaceLayer("Factorraria: Mech Suit Hologram", delegate
            {
                DrawPlaced(Main.spriteBatch);
                DrawHologram(Main.spriteBatch);
                return true;
            }, InterfaceScaleType.Game));
        }

        bool EnsureTextures()
        {
            if (texturesRequested)
                return texturesFound;

            texturesRequested = true;
            texturesFound = ModContent.RequestIfExists<Texture2D>(TexturePath, out frameTexture, AssetRequestMode.ImmediateLoad)
                & ModContent.RequestIfExists<Texture2D>(TexturePath + "Invalid", out invalidTexture, AssetRequestMode.ImmediateLoad);
            if (!texturesFound)
                Mod.Logger.Warn("MechSuitHologramSystem: " + TexturePath + ".png or " + TexturePath + "Invalid.png is missing; the placement box is disabled.");

            return texturesFound;
        }

        // ---------------------------------------------------------------- blueprint

        void BuildBlueprint(Rectangle selection, int version)
        {
            ModContent.GetInstance<MechSuitBlueprint>().Scan(selection, blocks);

            int n = blocks.Count;
            states = new BlockState[n];
            cellState = new byte[n];
            groupTotal = new int[n];
            groupMatch = new int[n];
            groupConflict = new int[n];
            blueprintWidth = selection.Width;
            blueprintHeight = selection.Height;
            cellRed = new bool[blueprintWidth * blueprintHeight];
            builtVersion = version;
            evaluated = false;
        }

        // ---------------------------------------------------------------- state at the destination

        void Evaluate(Point origin, Point cursor, Player player)
        {
            int n = blocks.Count;
            Array.Clear(groupTotal, 0, n);
            Array.Clear(groupMatch, 0, n);
            Array.Clear(groupConflict, 0, n);

            for (int i = 0; i < n; i++)
            {
                Block b = blocks[i];
                int x = origin.X + b.X;
                int y = origin.Y + b.Y;

                byte cell; // 0 empty, 1 matches the blueprint, 2 holds something else (or is outside the world)
                if (!WorldGen.InWorld(x, y, 1))
                {
                    cell = 2;
                }
                else
                {
                    Tile t = Main.tile[x, y];
                    // The same-block rule of the server job (MechSuitBlueprint.Matches: type, and style for objects; frames are not compared).
                    cell = (byte)(MechSuitBlueprint.Matches(b, t) ? 1 : MechSuitBlueprint.Occupied(b, t) ? 2 : 0);
                }

                cellState[i] = cell;
                int g = b.Group;
                groupTotal[g]++;
                if (cell == 1) groupMatch[g]++;
                else if (cell == 2) groupConflict[g]++;
            }

            // Collision / built per object, and the free ones become candidates for the material pass.
            candidates.Clear();
            for (int i = 0; i < n; i++)
            {
                if (blocks[i].Group != i)
                    continue;

                if (groupConflict[i] > 0 || (groupMatch[i] > 0 && groupMatch[i] < groupTotal[i]))
                    states[i] = BlockState.Collision;
                else if (groupMatch[i] == groupTotal[i])
                    states[i] = BlockState.Built;
                else
                {
                    states[i] = BlockState.Free;
                    candidates.Add(i);
                }
            }

            // Materials: what the local inventory holds, spent nearest-first from the cursor.
            remaining.Clear();
            for (int s = 0; s < 58; s++)
            {
                Item slot = player.inventory[s];
                if (slot != null && !slot.IsAir)
                    remaining[slot.type] = (remaining.TryGetValue(slot.type, out int have) ? have : 0) + slot.stack;
            }

            candidates.Sort((a, b) => DistanceSquared(blocks[a], origin, cursor).CompareTo(DistanceSquared(blocks[b], origin, cursor)));
            foreach (int i in candidates)
            {
                int item = blocks[i].Item;
                if (remaining.TryGetValue(item, out int left) && left > 0)
                    remaining[item] = left - 1;
                else
                    states[i] = BlockState.NoMaterial;
            }

            // Every block takes its object's state; the box cells under red blocks (and outside the world) turn red.
            Array.Clear(cellRed, 0, cellRed.Length);
            for (int i = 0; i < n; i++)
            {
                Block b = blocks[i];
                states[i] = states[b.Group];
                if (states[i] == BlockState.Collision || states[i] == BlockState.NoMaterial)
                    cellRed[b.X + b.Y * blueprintWidth] = true;
            }

            for (int cx = 0; cx < blueprintWidth; cx++)
                for (int cy = 0; cy < blueprintHeight; cy++)
                    if (!WorldGen.InWorld(origin.X + cx, origin.Y + cy, 1))
                        cellRed[cx + cy * blueprintWidth] = true;

            lastCursor = cursor;
            lastEvaluate = Main.GameUpdateCount;
            evaluated = true;
        }

        static float DistanceSquared(Block b, Point origin, Point cursor)
        {
            float dx = origin.X + b.X - cursor.X;
            float dy = origin.Y + b.Y - cursor.Y;
            return dx * dx + dy * dy;
        }

        // ---------------------------------------------------------------- copies already placed

        /// <summary>Owner side, called when the place click sends its build request.</summary>
        public void AddPlaced(Rectangle source, Point origin)
        {
            if (Main.dedServ)
                return;

            // Blocks are shifted to their destination (absolute tiles), so Origin stays zero. Objects are kept whole: every cell is drawn, the
            // head of each stands for the unit (the same units the server job holds).
            PlacedCopy copy = new PlacedCopy { Origin = Point.Zero };
            ModContent.GetInstance<MechSuitBlueprint>().Scan(source, copy.Blocks);
            MechSuitBlueprint.Shift(copy.Blocks, origin);
            if (copy.Blocks.Count == 0)
                return;

            // Same rule as the server: the new copy wins where it overlaps an older one (whole units are dropped).
            HashSet<(int, int, bool)> newCells = MechSuitBlueprint.CellsOf(copy.Blocks);
            foreach (PlacedCopy older in placedCopies)
                older.Blocks.RemoveAll(b => MechSuitBlueprint.Overlaps(newCells, b));

            placedCopies.RemoveAll(c => c.Blocks.Count == 0);
            placedCopies.Add(copy);
            lastPlacedTick = Main.GameUpdateCount;
            sawBuilding = Main.LocalPlayer.GetModPlayer<MechSuitPlayer>().IsBuilding;
        }

        void DrawPlaced(SpriteBatch spriteBatch)
        {
            if (placedCopies.Count == 0 || Main.dedServ)
                return;

            Player player = Main.LocalPlayer;
            bool building = player.GetModPlayer<MechSuitPlayer>().IsBuilding;
            if (building)
            {
                sawBuilding = true;
            }
            else if (sawBuilding || Main.GameUpdateCount - lastPlacedTick > (uint)PlacedGraceTicks)
            {
                placedCopies.Clear(); // the job ended (or the server never started one)
                sawBuilding = false;
                return;
            }

            Vector2 viewSize = Vector2.Max(Main.ViewSize, new Vector2(Main.screenWidth, Main.screenHeight));
            Rectangle view = new Rectangle((int)Main.screenPosition.X - 96, (int)Main.screenPosition.Y - 96, (int)viewSize.X + 192, (int)viewSize.Y + 192);
            Color valid = Color.White * HologramAlpha;
            Color red = RedTint * HologramAlpha;

            placedHave.Clear();
            for (int s = 0; s < 58; s++)
            {
                Item slot = player.inventory[s];
                if (slot != null && !slot.IsAir)
                    placedHave[slot.type] = (placedHave.TryGetValue(slot.type, out int have) ? have : 0) + slot.stack;
            }

            for (int pass = 0; pass < 2; pass++)
            {
                foreach (PlacedCopy copy in placedCopies)
                {
                    foreach (Block b in copy.Blocks)
                    {
                        if (b.IsWall != (pass == 0))
                            continue;

                        int x = copy.Origin.X + b.X;
                        int y = copy.Origin.Y + b.Y;
                        if (!WorldGen.InWorld(x, y, 1))
                            continue;

                        // The server job's rule for the whole unit: built units vanish, units with something else in the way are red.
                        MechSuitBlueprint.UnitState state = MechSuitBlueprint.StateOf(b);
                        if (state == MechSuitBlueprint.UnitState.Built)
                            continue;

                        if (!view.Contains(x * 16, y * 16))
                            continue;

                        // Red: something else stands there, or the inventory holds none of the material (the hologram stays either way).
                        bool tinted = state == MechSuitBlueprint.UnitState.Blocked || !placedHave.TryGetValue(b.Item, out int owned) || owned <= 0;
                        Vector2 position = new Vector2(x * 16f, y * 16f) - Main.screenPosition;
                        if (b.IsWall)
                            DrawWall(spriteBatch, b, position, tinted ? red : valid);
                        else
                            DrawTile(spriteBatch, b, position, tinted ? red : valid);
                    }
                }
            }
        }

        // ---------------------------------------------------------------- drawing

        void DrawHologram(SpriteBatch spriteBatch)
        {
            if (!MechSuitBuilderSystem.Ready(out Player player) || !MechSuitBuilderSystem.ShiftHeld)
                return;

            MechSuitBuilderSystem builder = ModContent.GetInstance<MechSuitBuilderSystem>();
            if (builder.IsDragging || !builder.Selection.HasValue)
                return;

            Rectangle selection = builder.Selection.Value;
            if (builtVersion != builder.SelectionVersion)
                BuildBlueprint(selection, builder.SelectionVersion);

            Point cursor = Main.MouseWorld.ToTileCoordinates();
            Point origin = MechSuitBuilderSystem.OriginFor(selection, cursor);
            if (!evaluated || cursor != lastCursor || Main.GameUpdateCount - lastEvaluate >= (uint)EvaluateIntervalTicks)
                Evaluate(origin, cursor, player);

            // Never smaller than the real screen (ViewSize can lag behind a zoom change) and with a generous margin, so the hologram is not cut off at the screen edges.
            Vector2 viewSize = Vector2.Max(Main.ViewSize, new Vector2(Main.screenWidth, Main.screenHeight));
            Rectangle view = new Rectangle((int)Main.screenPosition.X - 96, (int)Main.screenPosition.Y - 96, (int)viewSize.X + 192, (int)viewSize.Y + 192);
            Color valid = Color.White * HologramAlpha;
            Color red = RedTint * HologramAlpha;

            // Walls first so tiles sit on top of them.
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < blocks.Count; i++)
                {
                    Block b = blocks[i];
                    if (b.IsWall != (pass == 0) || states[i] == BlockState.Built)
                        continue;

                    int x = origin.X + b.X;
                    int y = origin.Y + b.Y;
                    if (!WorldGen.InWorld(x, y, 1) || !view.Contains(x * 16, y * 16))
                        continue;

                    Color color = states[i] == BlockState.Free ? valid : red;
                    Vector2 position = new Vector2(x * 16f, y * 16f) - Main.screenPosition;
                    if (b.IsWall)
                        DrawWall(spriteBatch, b, position, color);
                    else
                        DrawTile(spriteBatch, b, position, color);
                }
            }

            DrawBox(spriteBatch, origin, selection, view);
        }

        static void DrawWall(SpriteBatch spriteBatch, Block b, Vector2 position, Color color)
        {
            Main.instance.LoadWall(b.Type);
            Texture2D texture = TextureAssets.Wall[b.Type].Value;

            // Vanilla draws the 32x32 frame 8 px up-left of the tile; the tile's own part is the middle 16x16.
            Rectangle source = new Rectangle(b.FrameX + 8, b.FrameY + Main.wallFrame[b.Type] * 180 + 8, 16, 16);
            spriteBatch.Draw(texture, position, source, color);
        }

        static void DrawTile(SpriteBatch spriteBatch, Block b, Vector2 position, Color color)
        {
            Main.instance.LoadTiles(b.Type);
            Texture2D texture = TextureAssets.Tile[b.Type].Value;

            Rectangle source = new Rectangle(b.FrameX, b.FrameY, b.Width, b.Height);
            position.Y += b.DrawYOffset;
            if (b.Half)
            {
                source.Y += 8;
                source.Height = 8;
                position.Y += 8f;
            }

            spriteBatch.Draw(texture, position, source, color);
        }

        // The placement box: same cells as the selection overlay (RoboticArmAreaEditorSystem.Slot), from the red sheet where a block is red.
        void DrawBox(SpriteBatch spriteBatch, Point origin, Rectangle selection, Rectangle view)
        {
            if (!EnsureTextures())
                return;

            for (int cx = 0; cx < selection.Width; cx++)
            {
                for (int cy = 0; cy < selection.Height; cy++)
                {
                    int x = origin.X + cx;
                    int y = origin.Y + cy;
                    if (!view.Contains(x * 16, y * 16))
                        continue;

                    Texture2D sheet = cellRed[cx + cy * blueprintWidth] ? invalidTexture.Value : frameTexture.Value;
                    int cell = sheet.Width / 4;
                    float scale = 16f / cell;
                    int column = RoboticArmAreaEditorSystem.Slot(cx, selection.Width);
                    int row = RoboticArmAreaEditorSystem.Slot(cy, selection.Height);
                    Rectangle source = new Rectangle(column * cell, row * cell, cell, cell);
                    Vector2 position = new Vector2(x * 16f, y * 16f) - Main.screenPosition;
                    spriteBatch.Draw(sheet, position, source, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                }
            }
        }
    }
}