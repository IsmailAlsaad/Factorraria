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

        public const int FuelSlot = 0;
        public const int FirstIngredientSlot = 1;
        protected override int InputSlotCount => FirstIngredientSlot + FurnaceRecipeRegistry.MaxIngredientCount;
        public int ActiveIngredientCount => ManualGroup == null ? 1 : Math.Clamp(ManualGroup.MaxInputCount, 1, FurnaceRecipeRegistry.MaxIngredientCount);
        protected override List<RecipeOutputGroup> RecipeGroups => FurnaceRecipeRegistry.SmeltingGroups;

        protected override int OutputSlotCount => 1;


        public int FuelRemaining = 0;
        int fuelSmeltCount = 3;

        public override void Update()
        {
            base.Update();

            ReleaseHiddenIngredientSlots();

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
                if (InputSlots[FuelSlot].stack <= 0)
                {
                    isWorking = false;
                    return;
                }
                else if(CanAcceptFuel(InputSlots[FuelSlot].type))
                {
                    FuelRemaining += FurnaceRecipeRegistry.ValidFuels[InputSlots[FuelSlot].type];
                    InputSlots[FuelSlot].stack--;
                    if (InputSlots[FuelSlot].stack <= 0)
                    {
                        InputSlots[FuelSlot] = new Item();
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

        void ReleaseHiddenIngredientSlots()
        {
            for (int s = FirstIngredientSlot + ActiveIngredientCount; s < InputSlots.Length; s++)
            {
                Item item = InputSlots[s];
                if (item.IsAir) continue;

                Item.NewItem(new EntitySource_TileEntity(this),
                    Position.X * 16 + 16, Position.Y * 16 + 8, 16, 16, item.type, item.stack);
                InputSlots[s] = new Item();
            }
        }

        public override void PickUpVItems(VirtualItem vItem, int ValidSlotIndex, bool isItemInRecipe, int maxStack)
        {
            int currentMaxStack = maxStack;
            if (!isItemInRecipe)
            {
                if (!CanAcceptFuel(vItem.itemType)) return;
                currentMaxStack = Math.Max(10 - FurnaceRecipeRegistry.ValidFuels[vItem.itemType], 1);
                ValidSlotIndex = FuelSlot;
            }
            else
            {
                ValidSlotIndex = FindIngredientSlotFor(vItem.itemType);
                if (ValidSlotIndex == -1) return;
            }

            Item target = InputSlots[ValidSlotIndex];
            if (!target.IsAir && target.type != vItem.itemType)
                return;   // slot already holds something else

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
        int FindIngredientSlotFor(int itemType)
        {
            if (!FitsWithHeldIngredients(itemType)) return -1;

            int firstEmpty = -1;
            for (int s = FirstIngredientSlot; s < FirstIngredientSlot + ActiveIngredientCount; s++)
            {
                Item slot = InputSlots[s];
                if (slot.IsAir) { if (firstEmpty == -1) firstEmpty = s; }
                else if (slot.type == itemType) return s;   // matching slot wins, even if full
            }
            return firstEmpty;
        }

        // Manual group: only take an item if some recipe in the group uses it together with
        // everything already sitting in the ingredient slots. Auto mode has one slot, so it always fits.
        bool FitsWithHeldIngredients(int itemType)
        {
            if (ManualGroup == null) return true;

            foreach (CustomRecipe recipe in ManualGroup.Recipes)
            {
                if (!RecipeUsesItem(recipe, itemType)) continue;

                bool fitsHeld = true;
                for (int s = FirstIngredientSlot; s < FirstIngredientSlot + ActiveIngredientCount && fitsHeld; s++)
                {
                    Item held = InputSlots[s];
                    if (!held.IsAir && !RecipeUsesItem(recipe, held.type))
                        fitsHeld = false;
                }
                if (fitsHeld) return true;
            }
            return false;
        }

        static bool RecipeUsesItem(CustomRecipe recipe, int itemType)
        {
            foreach (RecipeIngredient input in recipe.Inputs)
                if (input.Type == itemType) return true;
            return false;
        }

        List<Item> ingredientBuffer;
        List<Item> GetIngredientItems()
        {
            ingredientBuffer ??= new List<Item>();
            ingredientBuffer.Clear();
            for (int s = FirstIngredientSlot; s < FirstIngredientSlot + ActiveIngredientCount; s++)
                ingredientBuffer.Add(InputSlots[s]);
            return ingredientBuffer;
        }

        bool isValidInput()
        {
            List<Item> items = GetIngredientItems();
            if (items.TrueForAll(i => i.IsAir)) return false;

            // Manual group: only that group's recipes are candidates. Auto: every recipe.
            // SelectedRecipe is always set from the actual match, since a group holds several recipes.
            List<CustomRecipe> candidates = ManualGroup != null ? ManualGroup.Recipes : FurnaceRecipeRegistry.SmeltingRecipes;
            return CustomRecipe.TryGetRecipeFromList(candidates, items, out SelectedRecipe);
        }

        bool isValidOutput()
        {
            return OutputSlots[0].stack < OutputMaxStack && (OutputSlots[0].type == SelectedRecipe.Output.Type || OutputSlots[0].IsAir) || IsOnConveyorFloor();
        }

        public bool CanAcceptFuel(int itemID)
        {
            return FurnaceRecipeRegistry.ValidFuels.ContainsKey(itemID);
        }

        protected override IEnumerable<CustomRecipe> GetPickupRecipes()
        {
            return ManualGroup != null ? ManualGroup.Recipes : FurnaceRecipeRegistry.SmeltingRecipes;
        }

        void FinishSmelting()
        {
            if (IsOnConveyorFloor())
            {
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
            foreach (RecipeIngredient req in SelectedRecipe.Inputs)
            {
                int remaining = req.Stack;
                for (int s = FirstIngredientSlot; s < FirstIngredientSlot + ActiveIngredientCount && remaining > 0; s++)
                {
                    Item slot = InputSlots[s];
                    if (slot.IsAir || slot.type != req.Type) continue;

                    int take = Math.Min(remaining, slot.stack);
                    slot.stack -= take;
                    remaining -= take;
                    if (slot.stack <= 0)
                    {
                        InputSlots[s] = new Item();
                    }
                }
            }

            WorkProgress = 0; 
        }

        public float GetSmeltPercent()
        {
            if (!isValidInput() || FuelRemaining <= 0f)
            {
                return -1f;
            }

            if (InputSlots[FuelSlot].type != ItemID.None && InputSlots[FuelSlot].stack > 0 && FurnaceRecipeRegistry.ValidFuels.TryGetValue(InputSlots[FuelSlot].type, out int fuelValue))
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