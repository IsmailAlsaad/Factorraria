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
    public class VanillaSolidiferOverride : GlobalTile
    {
        Asset<Texture2D> SolidifierOnTexture;
        Asset<Texture2D> SolidifierOffTexture;
        SolidifierTileEntity tileEntity;

        public override void Load()
        {
            SolidifierOnTexture = ModContent.Request<Texture2D>("Factorraria/Content/Tiles/Solidifier/Solidifier_On");
            SolidifierOffTexture = ModContent.Request<Texture2D>("Factorraria/Content/Tiles/Solidifier/Solidifier_Off");
        }

        public override bool PreDraw(int i, int j, int type, SpriteBatch spriteBatch)
        {
            if (type != TileID.Solidifier)
            {
                return true;
            }
            
            tileEntity = TileEntityHelper.GetOrCreateEntity<SolidifierTileEntity>(i, j);

            if (tileEntity.isPowered)
            {
                TileEntityHelper.AnimateTileEntity(spriteBatch, SolidifierOnTexture.Value, i, j);
            }
            else
            {
                TileEntityHelper.AnimateTileEntity(spriteBatch, SolidifierOffTexture.Value, i, j);
            }
            return false;
        }

        public override void KillTile(int i, int j, int type, ref bool fail, ref bool effectOnly, ref bool noItem)
        {
            if (type != TileID.Solidifier)
            {
                return;
            }

            // spawn back the items stored in product slot 

            //ModContent.GetInstance<FurnaceUISystem>().CloseFurnaceUI();
            TileEntityHelper.TryGetEntityFromTile(i, j, out TileEntity entity, out Point16 position);
            SolidifierTileEntity tileEntity = TileEntityHelper.GetOrCreateEntity<SolidifierTileEntity>(position.X, position.Y);

            // it wont output the liquids inside
            Item.NewItem(new EntitySource_TileEntity(tileEntity), position.X * 16 + 16, position.Y * 16 + 8, 16, 16, tileEntity.inputItem.type, tileEntity.inputItem.stack);
            // or maybe? it would be cool

            tileEntity.Kill(position.X, position.Y);
        }
    }
}
