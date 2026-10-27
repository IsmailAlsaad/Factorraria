using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace Factorraria.Content.Tiles.Machines.ElectricalProducers.BeeWheel
{
    // Textures and drawing helpers shared by the entity (live wheel and bees) and the placement hologram.
    // Assets are loaded by BeeWheelSystem and released on unload.
    public static class BeeWheelVisual
    {
        static Asset<Texture2D> wheel;
        static Asset<Texture2D> beeBig;     // optional, falls back to the vanilla Bee NPC sheet
        static Asset<Texture2D> beeSmall;   // optional, falls back to the vanilla small Bee NPC sheet

        public static void LoadAssets()
        {
            string folder = "Factorraria/Content/Tiles/Machines/ElectricalProducers/BeeWheel/";
            ModContent.RequestIfExists<Texture2D>(folder + "BeeWheel", out wheel);
            ModContent.RequestIfExists<Texture2D>(folder + "BeeWheel_BeeBig", out beeBig);
            ModContent.RequestIfExists<Texture2D>(folder + "BeeWheel_BeeSmall", out beeSmall);
        }

        public static void UnloadAssets()
        {
            wheel = null;
            beeBig = null;
            beeSmall = null;
        }

        // Draws the wheel centred on `center` (world position), rotated by `rotation` radians.
        public static void DrawWheel(SpriteBatch spriteBatch, Vector2 center, float rotation, Color color)
        {
            if (wheel == null)
                return;

            Texture2D texture = wheel.Value;
            spriteBatch.Draw(texture, center - Main.screenPosition, null, color, rotation, texture.Size() / 2f, 1f, SpriteEffects.None, 0f);
        }

        // Where the wheel's centre sits: the middle of the top tile of the footprint.
        public static Vector2 WheelCenter(Vector2 footprintTopLeft, int widthInTiles)
        {
            return footprintTopLeft + new Vector2(widthInTiles * 8f, 8f);
        }

        // Called by MachineOverhangSystem inside the placement hologram (MachineVisualDefinition.HologramExtra): the wheel at angle 0,
        // faded when the spot is valid and red when it is not (the same tint as the base).
        public static void DrawHologram(SpriteBatch spriteBatch, Vector2 footprintTopLeft, Color tint)
        {
            TileObjectData data = TileObjectData.GetTileData(ModContent.TileType<BeeWheelTile>(), 0);
            int width = data != null ? data.Width : 5;
            DrawWheel(spriteBatch, WheelCenter(footprintTopLeft, width), 0f, tint);
        }

        // Your own bee sheets (frames stacked vertically, customFrames of them) when the PNGs exist, else the vanilla bee NPC sheets.
        public static bool TryGetBeeTexture(bool big, int customFrames, out Texture2D texture, out int frames)
        {
            Asset<Texture2D> custom = big ? beeBig : beeSmall;
            if (custom != null)
            {
                texture = custom.Value;
                frames = System.Math.Clamp(customFrames, 1, 64);
                return true;
            }

            int npcId = big ? NPCID.Bee : NPCID.BeeSmall;
            Main.instance.LoadNPC(npcId);
            texture = TextureAssets.Npc[npcId].Value;
            frames = System.Math.Max(1, Main.npcFrameCount[npcId]);
            return texture != null;
        }
    }
}