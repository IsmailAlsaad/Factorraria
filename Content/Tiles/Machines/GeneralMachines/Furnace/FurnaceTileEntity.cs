using Factorraria.Common;
using Factorraria.Common.Machines;
using Factorraria.Content.VirtualItems;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader.IO;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.Furnace
{
    public class FurnaceTileEntity : BaseMachine
    {
        public override int ValidTileType => TileID.Furnaces;

        // InputSlots[0] = Fuel, InputSlots[1] = Input Item
        protected override int InputSlotCount => 2; // should be set dynamically after recipe selection
        protected override int OutputSlotCount => 1;

        public int FuelRemaining = 0;
        int fuelSmeltCount = 3;

        public override void Update()
        {
            base.Update();

            // MANUAL RECIPE SELECTED
            //SelectedRecipe = new CustomRecipe(new List<RecipeIngredient> { new(ItemID.Wood, 3) }, new(ItemID.Coal,1));
            // TESTING

            // doesn't need electricity -> always on
            isOn = true;

            if (!isValidInput() || !isValidOutput())
            {
                WorkProgress = 0; 
                isWorking = false;
                return;
            }

            //if (!CanAcceptInput(InputSlots[1].type)) 
            //{
            //    isWorking = false;
            //    return;
            //}

            if (FuelRemaining <= 0)
            {
                if (InputSlots[0].stack <= 0)
                {
                    isWorking = false;
                    return;
                }
                else if(CanAcceptFuel(InputSlots[0].type))
                {
                    FuelRemaining += FurnaceRecipeRegistry.ValidFuels[InputSlots[0].type];
                    InputSlots[0].stack--;
                    if (InputSlots[0].stack <= 0)
                    {
                        InputSlots[0] = new Item();
                    }
                }
            }

            isWorking = true; 
            WorkProgress++;   

            if (WorkProgress >= WorkDuration) 
            {
                FinishSmelting();
            }
        }

        public override void PickUpVItems(VirtualItem vItem, int ValidSlotIndex, bool isItemInRecipe, int maxStack)
        {
            int currentMaxStack = maxStack;

            ValidSlotIndex = Math.Clamp(ValidSlotIndex + 1, 0, InputSlotCount - 1);
            if (!isItemInRecipe)
            {
                if(CanAcceptFuel(vItem.itemType))
                {
                    currentMaxStack = Math.Max(10 - FurnaceRecipeRegistry.ValidFuels[vItem.itemType],1);
                    ValidSlotIndex = 0; // Fuel InputSlot index
                }
                else
                {
                    return;
                }
            }

            if (InputSlots[ValidSlotIndex].stack < currentMaxStack)
            {
                int spaceLeft = currentMaxStack - InputSlots[ValidSlotIndex].stack;
                int amountToAdd = Math.Min(spaceLeft, vItem.stackSize);

                InputSlots[ValidSlotIndex] = new Item(vItem.itemType, amountToAdd + InputSlots[ValidSlotIndex].stack);
                vItem.stackSize -= amountToAdd;

                // play pickup animation for vItem to machineCenter

                return;
            }
        }

        bool isValidInput() // should later check for each input slot
        {
            if (ManualRecipe != null)
            {
                return !InputSlots[1].IsAir
                    && CustomRecipe.TryGetRecipeFromList(
                        new List<CustomRecipe> { ManualRecipe },
                        new List<Item> { InputSlots[1] },
                        out _);
            }

            // auto-detect, same as your original
            return !InputSlots[1].IsAir
                && CustomRecipe.TryGetRecipeFromList(
                    FurnaceRecipeRegistry.SmeltingRecipes,
                    new List<Item> { InputSlots[1] },
                    out SelectedRecipe);
        }

        bool isValidOutput()
        {
            return OutputSlots[0].stack < OutputMaxStack && (OutputSlots[0].type == SelectedRecipe.Output.Type || OutputSlots[0].IsAir) || IsOnConveyorFloor();
        }

        public bool CanAcceptFuel(int itemID)
        {
            return FurnaceRecipeRegistry.ValidFuels.ContainsKey(itemID);
        }

        void FinishSmelting() // Later make it output to the productSlot too, and spawn a vItem instead of a regular item
        {
            if (IsOnConveyorFloor())
            {
                // needs to check the whole SmeltingRecipe
                //Vector2 spawnPosition = Position.ToWorldCoordinates();
                //int ProductIndex = Item.NewItem(
                //    new EntitySource_TileEntity(this),
                //    (int)spawnPosition.X + 16,
                //    (int)spawnPosition.Y,
                //    16, 16,
                //    SelectedRecipe.Output.Type,
                //    SelectedRecipe.Output.Stack);
                //Main.item[ProductIndex].velocity = new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(2f, 3f));

                Vector2 position = (MachineCenter / 16f);
                VirtualItemSystem.SpawnVirtualItem(SelectedRecipe.Output.Type, SelectedRecipe.Output.Stack, (int)position.X, (int)position.Y);
            }
            else
            {
                OutputSlots[0] = OutputSlots[0] == null || OutputSlots[0].IsAir ?
                    new Item(SelectedRecipe.Output.Type, SelectedRecipe.Output.Stack) :
                    new Item(SelectedRecipe.Output.Type, SelectedRecipe.Output.Stack + OutputSlots[0].stack);
            }


            FuelRemaining--;
            InputSlots[1].stack -= SelectedRecipe.Inputs[0].Stack; // must do another foreach -> match the input item id with the recipe item ids -> then subtract the stack 
            if (InputSlots[1].stack <= 0)
            {
                InputSlots[1] = new Item(); 
            }

            WorkProgress = 0; 
        }

        public float GetSmeltPercent()
        {
            if (!isValidInput() || FuelRemaining <= 0f)
            {
                return -1f;
            }

            if (InputSlots[0].type != ItemID.None && InputSlots[0].stack > 0 && FurnaceRecipeRegistry.ValidFuels.TryGetValue(InputSlots[0].type, out int fuelValue))
            {
                fuelSmeltCount = fuelValue;
            }

            float fuelPercent = FuelRemaining / (float)fuelSmeltCount;
            float smeltPercent = (float)WorkProgress / (float)WorkDuration;

            return fuelPercent - smeltPercent / (float)fuelSmeltCount;
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