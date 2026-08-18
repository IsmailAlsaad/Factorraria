using Factorraria.Content.Configs;
using Factorraria.Common.Systems;
using Factorraria.Content.Tiles.Furnace;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Factorraria.Common;

namespace Factorraria.Content.Tiles.Autohammer
{
    public class VanillaAutohammerOverride : GlobalTile
    {
        Asset<Texture2D> autohammerOnTexture;
        Asset<Texture2D> autohammerOffTexture;
        AutohammerTileEntity tileEntity;
        public override void Load()
        {
            autohammerOnTexture = ModContent.Request<Texture2D>("Factorraria/Content/Tiles/Autohammer/Autohammer_On");
            autohammerOffTexture = ModContent.Request<Texture2D>("Factorraria/Content/Tiles/Autohammer/Autohammer_Off");
        }

        public override bool PreDraw(int i, int j, int type, SpriteBatch spriteBatch)
        {
            if (type != TileID.Autohammer)
            {
                return true;
            }

            tileEntity = TileEntityHelper.GetOrCreateEntity<AutohammerTileEntity>(i, j);

            if (tileEntity.isPowered)
            {
                TileEntityHelper.AnimateTileEntity(spriteBatch, autohammerOnTexture.Value, i, j);
            }
            else
            {
                TileEntityHelper.AnimateTileEntity(spriteBatch, autohammerOffTexture.Value, i, j);
            }
            
            return false;
        }

        public override void KillTile(int i, int j, int type, ref bool fail, ref bool effectOnly, ref bool noItem)
        {
            if (type != TileID.Autohammer)
            {
                return;
            }

            // spawn back the items stored in product slot 

            //ModContent.GetInstance<FurnaceUISystem>().CloseFurnaceUI();
            TileEntityHelper.TryGetEntityFromTile(i, j, out TileEntity entity, out Point16 position);
            AutohammerTileEntity tileEntity = TileEntityHelper.GetOrCreateEntity<AutohammerTileEntity>(position.X, position.Y);

            Item.NewItem(new EntitySource_TileEntity(tileEntity), position.X * 16 + 16, position.Y * 16 + 8, 16, 16, tileEntity.inputItem.type, tileEntity.inputItem.stack);

            tileEntity.Kill(position.X, position.Y);
        }
    }
}
