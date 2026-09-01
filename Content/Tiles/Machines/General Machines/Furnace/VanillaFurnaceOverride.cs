using Factorraria.Common.Machines;
using Factorraria.Common.Systems;
using Factorraria.Content.Configs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.Furnace
{
    public class VanillaFurnaceOverride : GlobalTile
    {
        Asset<Texture2D> FurnaceLitTexture;
        Asset<Texture2D> FurnaceOffTexture;
        FurnaceTileEntity tileEntity;

        public override void Load()
        {
            FurnaceLitTexture = ModContent.Request<Texture2D>("Factorraria/Content/Tiles/Machines/General Machines/Furnace/Furnace_On");
            FurnaceOffTexture = ModContent.Request<Texture2D>("Factorraria/Content/Tiles/Machines/General Machines/Furnace/Furnace_Off");
        }

        public override bool PreDraw(int i, int j, int type, SpriteBatch spriteBatch)
        {
            if(type != TileID.Furnaces)
            {
                return true;
            }

            tileEntity = TileEntityHelper.GetOrCreateEntity<FurnaceTileEntity>(i, j);

            if (tileEntity.isSmelting)
            {
                TileEntityHelper.AnimateTileEntity(spriteBatch, FurnaceLitTexture.Value, i, j);
            }
            else
            {
                TileEntityHelper.AnimateTileEntity(spriteBatch, FurnaceOffTexture.Value, i, j);
            }
            
            return false;
        }

        public override void RightClick(int i, int j, int type)
        {
            if(type != TileID.Furnaces)
            {
                return;
            }

            TileEntityHelper.TryGetEntityFromTile(i, j, out TileEntity entity, out Point16 topLeft);
            FurnaceTileEntity furnaceTileEntity = TileEntityHelper.GetOrCreateEntity<FurnaceTileEntity>(topLeft.X,topLeft.Y);

            ModContent.GetInstance<FurnaceUISystem>().OpenFurnaceUI(topLeft.X, topLeft.Y);

            return;
        }

        public override void KillTile(int i, int j, int type, ref bool fail, ref bool effectOnly, ref bool noItem)
        {
            if (type != TileID.Furnaces) 
            {
                return;
            }

            // spawn back the items stored in product slot 

            ModContent.GetInstance<FurnaceUISystem>().CloseFurnaceUI();
            TileEntityHelper.TryGetEntityFromTile(i, j, out TileEntity entity, out Point16 position);
            FurnaceTileEntity furnaceTileEntity = TileEntityHelper.GetOrCreateEntity<FurnaceTileEntity>(position.X,position.Y);

            Item.NewItem(new EntitySource_TileEntity(furnaceTileEntity), position.X * 16 + 16, position.Y * 16 + 8, 16, 16, furnaceTileEntity.OreItem.type, furnaceTileEntity.OreItem.stack);
            Item.NewItem(new EntitySource_TileEntity(furnaceTileEntity), position.X * 16 + 16, position.Y * 16 + 8, 16, 16, furnaceTileEntity.FuelItem.type, furnaceTileEntity.FuelItem.stack);

            furnaceTileEntity.Kill(position.X, position.Y);
        }
    }
}
