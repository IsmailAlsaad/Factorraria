using Factorraria.Common;
using Factorraria.Common.Systems;
using Factorraria.Content.Configs;
using Factorraria.Content.Tiles.Furnace;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Autohammer
{
    public class VanillaSteampunkBoilerOverride : GlobalTile
    {
        Asset<Texture2D> GelBurnerOnTexture;
        Asset<Texture2D> GelBurnerOffTexture;
        GelBurnerTileEntity tileEntity;

        public override void Load()
        {
            GelBurnerOnTexture = ModContent.Request<Texture2D>("Factorraria/Content/Tiles/GelBurner/GelBurner_On");
            GelBurnerOffTexture = ModContent.Request<Texture2D>("Factorraria/Content/Tiles/GelBurner/GelBurner_Off");
        }

        public override bool PreDraw(int i, int j, int type, SpriteBatch spriteBatch)
        {
            if (type != TileID.SteampunkBoiler)
            {
                return true;
            }

            tileEntity = TileEntityHelper.GetOrCreateEntity<GelBurnerTileEntity>(i, j);

            if (tileEntity.isGenerating)
            {
                TileEntityHelper.AnimateTileEntity(spriteBatch, GelBurnerOnTexture.Value, i, j);
            }
            else
            {
                TileEntityHelper.AnimateTileEntity(spriteBatch, GelBurnerOffTexture.Value, i, j);
            }
            return false;
        }

        public override void KillTile(int i, int j, int type, ref bool fail, ref bool effectOnly, ref bool noItem)
        {
            if (type != TileID.SteampunkBoiler)
            {
                return;
            }

            // spawn back the items stored in product slot 

            //ModContent.GetInstance<FurnaceUISystem>().CloseFurnaceUI();
            TileEntityHelper.TryGetEntityFromTile(i, j, out TileEntity entity, out Point16 position);
            GelBurnerTileEntity tileEntity = TileEntityHelper.GetOrCreateEntity<GelBurnerTileEntity>(position.X, position.Y);

            Item.NewItem(new EntitySource_TileEntity(tileEntity), position.X * 16 + 16, position.Y * 16 + 8, 16, 16, tileEntity.inputItem.type, tileEntity.inputItem.stack);

            tileEntity.Kill(position.X, position.Y);
        }
    }
}
