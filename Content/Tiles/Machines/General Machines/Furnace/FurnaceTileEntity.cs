using Factorraria.Common;
using Factorraria.Common.Machines;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader.IO;

namespace Factorraria.Content.Tiles.Machines.Furnace
{
    public class FurnaceTileEntity : BaseMachine
    {
        public override int ValidTileType => TileID.Furnaces;
        protected override int InputSlotCount => 2;

        public int FuelRemaining = 0;

        public override void Update()
        {
            if (!isValidInput())
            {
                WorkProgress = 0; 
                isWorking = false;
                return;
            }

            if (!CanAcceptInput(InputSlots[0].type)) 
            {
                isWorking = false;
                return;
            }

            if (FuelRemaining <= 0)
            {
                if (InputSlots[1].stack <= 0)
                {
                    isWorking = false;
                    return;
                }
                else
                {
                    FuelRemaining += FurnaceRecipeRegistry.ValidFuels[InputSlots[1].type];
                    InputSlots[1].stack--; 
                }
            }

            isWorking = true; 
            WorkProgress++;   

            if (WorkProgress >= WorkDuration) 
            {
                FinishSmelting();
            }
        }

        bool isValidInput()
        {
            return !InputSlots[0].IsAir && InputSlots[0].stack >= FurnaceRecipeRegistry.SmeltingRecipes[InputSlots[0].type].InputItemCount;
        }

        void FinishSmelting() // Later make it output to the productSlot too
        {
            RecipeData data = FurnaceRecipeRegistry.SmeltingRecipes[InputSlots[0].type];
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
            InputSlots[0].stack -= data.InputItemCount; 
            if (InputSlots[0].stack <= 0)
            {
                InputSlots[0] = new Item(); 
            }

            WorkProgress = 0; 
        }

        int fuelSmeltCount = 3;
        public float GetSmeltPercent()
        {
            if (!isValidInput() || FuelRemaining <= 0f)
            {
                return -1f;
            }

            if (InputSlots[1].type != ItemID.None) 
            {
                fuelSmeltCount = FurnaceRecipeRegistry.ValidFuels[InputSlots[1].type];
            }

            float fuelPercent = FuelRemaining / (float)fuelSmeltCount;
            float smeltPercent = (float)WorkProgress / (float)WorkDuration;

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

        public override void SaveData(TagCompound tag)
        {
            base.SaveData(tag); 
            tag["FuelRemaining"] = FuelRemaining;
            tag["WorkProgress"] = WorkProgress;
        }

        public override void LoadData(TagCompound tag)
        {
            base.LoadData(tag);
            FuelRemaining = tag.GetInt("FuelRemaining");
            WorkProgress = tag.GetInt("WorkProgress");
        }
    }
}