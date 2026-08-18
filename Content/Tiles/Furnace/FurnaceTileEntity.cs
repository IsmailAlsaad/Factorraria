using Factorraria.Common;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Factorraria.Content.Tiles.Furnace
{
    public class FurnaceTileEntity : ModTileEntity
    {
        public bool isSmelting;
        public int FuelRemaining = 0;
        public Item FuelItem = new Item();
        public Item OreItem = new Item();

        int smeltProgress = 0;
        // 120 = 60 ticks * 2 seconds
        const int SmeltDuration = 120;

        public override void Update()
        {
            if (!isValidInput())
            {
                smeltProgress = 0;
                isSmelting = false;
                return;
            }

            if (!CanAcceptInput(OreItem.type))
            {
                isSmelting = false;
                return;
            }

            if (FuelRemaining <= 0)
            {
                if (FuelItem.stack <= 0)
                {
                    isSmelting = false;
                    return;
                }
                else
                {
                    FuelRemaining += FurnaceRecipeRegistry.ValidFuels[FuelItem.type];
                    FuelItem.stack--;
                }
            }

            isSmelting = true;
            smeltProgress++;

            if (smeltProgress >= SmeltDuration)
            {
                FinishSmelting();
            }
        }

        bool isValidInput()
        {
            return !OreItem.IsAir && OreItem.stack >= FurnaceRecipeRegistry.SmeltingRecipes[OreItem.type].InputItemCount;
        }

        void FinishSmelting()
        {
            RecipeData data = FurnaceRecipeRegistry.SmeltingRecipes[OreItem.type];
            int ProductID = data.OutputItemID;
            Vector2 spawnPosition = Position.ToWorldCoordinates();
            int ProductIndex = Item.NewItem(
                new EntitySource_TileEntity(this),
                (int)spawnPosition.X + 16,
                (int)spawnPosition.Y,
                16, 16,
                ProductID,
                data.OutputItemCount);

            Main.item[ProductIndex].velocity = new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(2f, 3f));

            FuelRemaining--;
            OreItem.stack -= data.InputItemCount;
            if (OreItem.stack <= 0) 
            {
                OreItem = new Item();
            }

            smeltProgress = 0;
        }

        int fuelSmeltCount = 3;
        public float GetSmeltPercent()
        {
            if (!isValidInput())
            {
                return 0f;
            }

            if (FuelItem.type != ItemID.None)
            {
                fuelSmeltCount = FurnaceRecipeRegistry.ValidFuels[FuelItem.type];
            }

            float fuelPercent = FuelRemaining / (float)fuelSmeltCount;
            float smeltPercent = (float)smeltProgress / (float)SmeltDuration;

            //
            //Main.NewText($"Smelt percent = {smeltPercent}");
            //

            return fuelPercent - smeltPercent / (float)fuelSmeltCount;
        }

        public bool CanAcceptInput(int InputID)
        {
            return FurnaceRecipeRegistry.SmeltingRecipes.ContainsKey(InputID);
        }

        public bool CanAcceptFuel(int FuelID)
        {
            return FurnaceRecipeRegistry.ValidFuels.ContainsKey(FuelID);
        }

        public override bool IsTileValidForEntity(int x, int y)
        {
            Tile tile = Framing.GetTileSafely(x, y);
            return tile.HasTile && tile.TileType == TileID.Furnaces;
        }

        public override int Hook_AfterPlacement(int i, int j, int type, int style, int direction, int alternate)
        {
            // Multiplayer stuff I don't know
            //if (Main.netMode == NetmodeID.MultiplayerClient)
            //{
            //    // Synchronize the 3x2 tile area across the network
            //    NetMessage.SendTileSquare(Main.myPlayer, i, j, 3, 2);
            //    NetMessage.SendData(MessageID.TileEntityPlacement, number: -1, number2: i, number3: j, number4: Type);
            //    return -1;
            //}
            if(type != TileID.Furnaces)
            {
                return -1;
            }

            return Place(i, j);
        }

        public override void SaveData(TagCompound tag)
        {
            tag["InputItem"] = OreItem;
            tag["FuelItem"] = FuelItem;
            tag["FuelRemaining"] = FuelRemaining;
            tag["SmeltProgress"] = smeltProgress;
        }

        public override void LoadData(TagCompound tag)
        {
            OreItem = tag.Get<Item>("InputItem");
            FuelItem = tag.Get<Item>("FuelItem");
            FuelRemaining = tag.GetInt("FuelRemaining");
            smeltProgress = tag.GetInt("SmeltProgress");
        }
    }
}
