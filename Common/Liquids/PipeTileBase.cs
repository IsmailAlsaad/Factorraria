using Factorraria.Common.Systems;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace Factorraria.Common.Liquids
{
    // Every pipe variant (lead, iron, ...) inherits this and only has to say its
    // own material and max flow rate — everything else (placement tracking, auto-shaping) is shared.
    public abstract class PipeTileBase : ModTile
    {
        protected abstract float MaxFlowRate { get; }

        // Pipes only connect to pipes and motors of the SAME material (see PipeTierRegistry.CanLink).
        protected abstract PipeMaterial Material { get; }

        // Dust the pipe throws when it breaks.
        protected virtual int PipeDustId => DustID.Lead;

        public override void SetStaticDefaults()
        {
            Main.tileSolid[Type] = true;
            Main.tileSolidTop[Type] = true; // platform-like top: things can drop through from below, stand on top
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true; // tells Terraria this tile's look depends on TileFrameX/Y, not a fixed sprite
            Main.tileNoAttach[Type] = false;

            HitSound = SoundID.Tink;

            TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
            // Prevent DivideByZeroException when external mods query tile style
            TileObjectData.newTile.StyleHorizontal = true;
            TileObjectData.newTile.StyleWrapLimit = 1;

            // Clear default floor requirement
            TileObjectData.newTile.AnchorBottom = AnchorData.Empty;

            // Lava: survives it (no break on contact, no obsidian kill) and can be placed inside it.
            TileObjectData.newTile.LavaDeath = false;
            TileObjectData.newTile.LavaPlacement = LiquidPlacement.Allowed;
            Main.tileLavaDeath[Type] = false;
            Main.tileObsidianKill[Type] = false;

            // MUST be called after setting AnchorBottom
            TileObjectData.addTile(Type);

            PipeTierRegistry.Register(Type, MaxFlowRate, Material);

            // Art outside the 16px cell: one cell per connection mask (16 cells in a row), see TileOverdrawSystem.
            ModContent.GetInstance<TileOverdrawSystem>().Register(Type, OverdrawTexturePath, SelectOverdrawCell, inFront: false, previewSelector: SelectPreviewCell);
        }

        // LeadPipeTile -> .../LeadPipeOverdraw (PNG optional: a missing sheet just draws nothing).
        protected virtual string OverdrawTexturePath => Texture.EndsWith("Tile") ? Texture.Substring(0, Texture.Length - 4) + "Overdraw" : Texture + "_Overdraw";

        // Placement hologram: the pipe is not placed yet, so the mask is worked out from its neighbours exactly like ReframeSelf does.
        bool SelectPreviewCell(int i, int j, Tile tile, out int cell, out float rotation)
        {
            rotation = 0f;
            PipeConnectionHelper.GetOpenSides(i, j, out bool up, out bool down, out bool left, out bool right, Type);
            cell = (up ? 1 : 0) | (down ? 2 : 0) | (left ? 4 : 0) | (right ? 8 : 0);
            return true;
        }

        // The tile frame is TileFrameX = connectionMask * 18 (ReframeSelf), so the mask is the cell index.
        static bool SelectOverdrawCell(int i, int j, Tile tile, out int cell, out float rotation)
        {
            rotation = 0f;
            cell = tile.TileFrameX / 18;
            return cell >= 0 && cell < 16;
        }

        public override bool CanPlace(int i, int j)
        {
            // 1. Valid if background wall exists
            if (Main.tile[i, j].WallType > WallID.None)
                return true;

            // 2. Valid if attached to ANY tile (solid blocks, pipes, or motors)
            if (Main.tile[i - 1, j].HasTile ||
                Main.tile[i + 1, j].HasTile ||
                Main.tile[i, j - 1].HasTile ||
                Main.tile[i, j + 1].HasTile)
            {
                return true;
            }

            return false; // Prevents floating placement in mid-air
        }

        // Fires when this exact pipe is placed.
        public override void PlaceInWorld(int i, int j, Item item)
        {
            // Patch 3.3: registering the pipe with the network is done by PipePlacedConsumer on the server (this hook only runs on the
            // placing client). Only the sprite is framed here.
            ReframeSelfAndNeighbors(i, j);
        }

        // Fires when this exact pipe is broken.
        public override void KillTile(int i, int j, ref bool fail, ref bool effectOnly, ref bool noItem)
        {
            LiquidNetworkSystem.UnregisterPipeTile(new Point(i, j));
            // Note: neighbors also need reframing after a break — Terraria's own
            // NearbyEffects (below) handles that automatically once the tile is gone.
        }

        // Terraria calls this on tiles NEAR a change automatically — this is what lets
        // a neighboring pipe notice "something next to me changed" and reshape itself,
        // without the placed/broken tile needing to manually touch every neighbor itself.
        public override void NearbyEffects(int i, int j, bool closer)
        {
            ReframeSelf(i, j);
        }

        static void ReframeSelfAndNeighbors(int i, int j)
        {
            ReframeSelf(i, j);
            ReframeIfPipe(i - 1, j);
            ReframeIfPipe(i + 1, j);
            ReframeIfPipe(i, j - 1);
            ReframeIfPipe(i, j + 1);
        }

        static void ReframeIfPipe(int i, int j)
        {
            if (PipeTierRegistry.IsPipeTile(Main.tile[i, j].TileType))
                ReframeSelf(i, j);
        }

        // Entry point for non-pipe tiles (motors) to say "something about me changed, any pipe
        // touching me should reconsider its sprite." Motor rotation is the main case — that has
        // no natural tile-frame event to piggyback on the way placement/breaking does.
        public static void ReframeNeighbors(int i, int j)
        {
            ReframeIfPipe(i - 1, j);
            ReframeIfPipe(i + 1, j);
            ReframeIfPipe(i, j - 1);
            ReframeIfPipe(i, j + 1);
        }

        static void ReframeSelf(int i, int j)
        {
            PipeConnectionHelper.GetOpenSides(i, j, out bool up, out bool down, out bool left, out bool right);

            int connectionMask = (up ? 1 : 0) | (down ? 2 : 0) | (left ? 4 : 0) | (right ? 8 : 0);

            Main.tile[i, j].TileFrameX = (short)(connectionMask * 18);
            Main.tile[i, j].TileFrameY = 0;
        }

        // Unhammerable: no slopes, no half blocks (the sprite and the flow logic assume a full tile).
        public override bool Slope(int i, int j)
        {
            return false;
        }

        public override bool CreateDust(int i, int j, ref int type)
        {
            type = PipeDustId;
            return true;
        }
    }
}