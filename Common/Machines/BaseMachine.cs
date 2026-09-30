using Factorraria.Common.Liquids;
using Factorraria.Common.Systems;
using Factorraria.Common.UI;
using Factorraria.Content.VirtualItems;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;

namespace Factorraria.Common.Machines
{
    public readonly struct RecipeIngredient
    {
        public readonly int Type;
        public readonly int Stack;

        public RecipeIngredient(int type, int stack)
        {
            Type = type;
            Stack = stack;
        }
    }

    public class CustomRecipe
    {
        public List<RecipeIngredient> Inputs { get; set; }
        public RecipeIngredient Output { get; set; }

        public CustomRecipe(List<RecipeIngredient> inputs, RecipeIngredient output)
        {
            Inputs = inputs;
            Output = output;
        }

        public CustomRecipe(List<Item> inputs, Item output)
        {
            Inputs = inputs.Select(item => new RecipeIngredient(item.type, item.stack)).ToList();
            Output = new RecipeIngredient(output.type, output.stack);
        }

        public static bool TryGetRecipeFromList(List<CustomRecipe> recipeList, List<Item> InputSlots, out CustomRecipe outputRecipe)
        {
            outputRecipe = null;

            int activeSlotCount = 0;
            for (int i = 0; i < InputSlots.Count; i++)
            {
                Item slot = InputSlots[i];
                if (slot != null && !slot.IsAir && slot.stack > 0)
                {
                    activeSlotCount++;
                }
            }

            foreach (CustomRecipe recipe in recipeList)
            {
                if (recipe.Inputs.Count != activeSlotCount)
                    continue;

                bool isMatch = true;

                foreach (RecipeIngredient req in recipe.Inputs)
                {
                    int totalFoundInMachine = 0;

                    for (int i = 0; i < InputSlots.Count; i++)
                    {
                        Item slot = InputSlots[i];
                        if (slot != null && !slot.IsAir && slot.type == req.Type)
                        {
                            totalFoundInMachine += slot.stack;
                        }
                    }

                    if (totalFoundInMachine < req.Stack)
                    {
                        isMatch = false;
                        break;
                    }
                }

                if (isMatch)
                {
                    outputRecipe = recipe;
                    return true;
                }
            }

            return false;
        }
    }

    public abstract class BaseMachine : ModTileEntity
    {
        #region Variables
        public abstract int ValidTileType { get; }
        public bool isOn;
        public bool isWorking { get; protected set; }
        protected int WorkProgress;
        protected virtual int WorkDuration => 120; // 60 ticks = 1 second, override per machine

        protected virtual int InputSlotCount => 0;
        protected virtual int OutputSlotCount => 0;

        public CustomRecipe SelectedRecipe;   // what the machine is actually running (manual OR auto-detected)
        public CustomRecipe ManualRecipe;     // only what the player picked; null = auto-detect

        public void SetManualRecipe(CustomRecipe recipe)
        {
            ManualRecipe = recipe;
            SelectedRecipe = recipe;   // null on un-toggle, so auto-detect starts fresh
        }

        public int OutputMaxStack = 10;
        public Point16 cornerPosition;
        public Vector2 MachineCenter;
        public int MachineWidth;
        public int MachineHeight;
        public bool MachineInitialized;

        int lastAnimationFrame = -1;

        Item[] inputSlots;
        Item[] outputSlots;
        public Item[] InputSlots => inputSlots ??= CreateItemArray(InputSlotCount);
        public Item[] OutputSlots => outputSlots ??= CreateItemArray(OutputSlotCount);

        static Item[] CreateItemArray(int count)
        {
            var arr = new Item[count];
            for (int i = 0; i < count; i++) arr[i] = new Item();
            return arr;
        }

        protected virtual int InputLiquidCount => 0;
        protected virtual int OutputLiquidCount => 0;

        LiquidStack[] inputLiquids;
        LiquidStack[] outputLiquids;
        public LiquidStack[] InputLiquids => inputLiquids ??= CreateLiquidArray(InputLiquidCount);
        public LiquidStack[] OutputLiquids => outputLiquids ??= CreateLiquidArray(OutputLiquidCount);

        static LiquidStack[] CreateLiquidArray(int count)
        {
            var arr = new LiquidStack[count];
            for (int i = 0; i < count; i++) arr[i] = new LiquidStack();
            return arr;
        }
        #endregion

        public override bool IsTileValidForEntity(int x, int y)
        {
            Tile tile = Framing.GetTileSafely(x, y);
            return tile.HasTile && tile.TileType == ValidTileType;
        }

        public override int Hook_AfterPlacement(int i, int j, int type, int style, int direction, int alternate)
        {
            // Multiplayer stuff I don't know
            //if (Main.netMode == NetmodeID.MultiplayerClient)
            //{
            //    // Synchronize the 3x2 tile area across the network // should somehow make it per tileArea
            //    NetMessage.SendTileSquare(Main.myPlayer, i, j, 3, 2);
            //    NetMessage.SendData(MessageID.TileEntityPlacement, number: -1, number2: i, number3: j, number4: Type);
            //    return -1;
            //}

            Tile tile = Framing.GetTileSafely(i, j);
            if (!tile.HasTile || tile.TileType != ValidTileType)
                return -1;

            int id = Place(i, j);
            if (id != -1 && ByID.TryGetValue(id, out TileEntity entity))
            {
                PowerGridSystem.RegisterMachineToMasterList(entity);
                LiquidNetworkSystem.networkNeedsRebuilding = true;
            }
            return id;
        }
        public override void OnKill()
        {
            LiquidNetworkSystem.networkNeedsRebuilding = true;
            ModContent.GetInstance<MachineUISystem>().NotifyMachineKilled(this);
        }

        public override void Update()
        {
            ScanVItems();
        }

        void ScanVItems()
        {
            if (InputSlotCount == 0 || SelectedRecipe == null)
            {
                return;
            }

            VirtualItem vItem = null;

            // scan hitbox for vItems
            for (int i = cornerPosition.X; i < cornerPosition.X + MachineWidth; i++)
            {
                for (int j = cornerPosition.Y; j < cornerPosition.Y + MachineHeight; j++)
                {
                    vItem = VirtualItemSystem.GetVirtualItemAtTile(i, j);
                }
            }
            
            if(vItem == null)
            {
                return;
            }

            bool isItemInRecipe = false;
            int maxStack = 0;

            foreach (RecipeIngredient ingredient in SelectedRecipe.Inputs)
            {
                if (vItem.itemType == ingredient.Type)
                {
                    isItemInRecipe = true;
                    maxStack = ingredient.Stack * 2;
                    break;
                }
            }

            int ValidSlotIndex = -1;
            int EmptySlotIndex = -1;
            bool foundMatching = false;

            for (int i = 0; i < InputSlotCount; i++)
            {
                if (InputSlots[i].IsAir)
                {
                    EmptySlotIndex = i;
                }

                if (InputSlots[i].type == vItem.itemType)
                {
                    ValidSlotIndex = i;
                    foundMatching = true;
                    break;
                }
            }

            if (!foundMatching)
            {
                ValidSlotIndex = EmptySlotIndex;
            }

            //Main.NewText("FOUND ITEM");

            PickUpVItems(vItem,ValidSlotIndex,isItemInRecipe,maxStack);
        }

        public virtual void PickUpVItems(VirtualItem vItem, int ValidSlotIndex, bool isItemInRecipe, int maxStack)
        {
            if (!isItemInRecipe)
            {
                return;
            }

            if (InputSlots[ValidSlotIndex].stack < maxStack)
            {
                int spaceLeft = maxStack - InputSlots[ValidSlotIndex].stack;
                int amountToAdd = Math.Min(spaceLeft, vItem.stackSize);

                InputSlots[ValidSlotIndex] = new Item(vItem.itemType, amountToAdd + InputSlots[ValidSlotIndex].stack);
                vItem.stackSize -= amountToAdd;

                // play pickup animation for vItem to machineCenter

                return;
            }
        }

        public bool IsOnConveyorFloor()
        {
            for (int i = cornerPosition.X; i < cornerPosition.X + MachineWidth; i++)
            {
                if(VirtualItemSystem.IsConveyorTile(i, cornerPosition.Y + MachineHeight, out _, out _))
                {
                    return true;
                }
            }
            return false;
        }

        public void NotifyAnimationFrame(int frame)
        {
            if (frame == lastAnimationFrame || frame == -1) return;
            OnAnimationFrameChanged(frame, lastAnimationFrame);
            lastAnimationFrame = frame;
        }

        public virtual void OnRightClick(int i, int j)
        {
            if (MachineUIRegistry.Definitions.ContainsKey(ValidTileType))
            {
                ModContent.GetInstance<MachineUISystem>().OpenUI(i, j, ValidTileType, this);
            }
        }

        protected virtual void OnAnimationFrameChanged(int newFrame, int previousFrame) { }

        public override void SaveData(TagCompound tag)
        {
            for (int i = 0; i < InputSlots.Length; i++)
            {
                tag[$"Input{i}"] = InputSlots[i];
            }
            for (int i = 0; i < OutputSlots.Length; i++)
            {
                tag[$"Output{i}"] = OutputSlots[i];
            }
            for (int i = 0; i < InputLiquids.Length; i++)
            {
                tag[$"InputLiquidType{i}"] = InputLiquids[i].LiquidType;
                tag[$"InputLiquidAmount{i}"] = InputLiquids[i].Amount;
            }
            for (int i = 0; i < OutputLiquids.Length; i++)
            {
                tag[$"OutputLiquidType{i}"] = OutputLiquids[i].LiquidType;
                tag[$"OutputLiquidAmount{i}"] = OutputLiquids[i].Amount;
            }

            tag["MachineCenter"] = MachineCenter;
            tag["CornerPosition"] = cornerPosition;
            tag["MachineWidth"] = MachineWidth;
            tag["MachineHeight"] = MachineHeight;
        }

        public override void LoadData(TagCompound tag)
        {
            for (int i = 0; i < InputSlots.Length; i++)
            {
                InputSlots[i] = tag.Get<Item>($"Input{i}");
            }
            for (int i = 0; i < OutputSlots.Length; i++)
            {
                OutputSlots[i] = tag.Get<Item>($"Output{i}");
            }
            for (int i = 0; i < InputLiquids.Length; i++)
            {
                InputLiquids[i].LiquidType = tag.GetInt($"InputLiquidType{i}");
                InputLiquids[i].Amount = tag.GetFloat($"InputLiquidAmount{i}");
            }
            for (int i = 0; i < OutputLiquids.Length; i++)
            {
                OutputLiquids[i].LiquidType = tag.GetInt($"OutputLiquidType{i}");
                OutputLiquids[i].Amount = tag.GetFloat($"OutputLiquidAmount{i}");
            }

            MachineCenter = tag.Get<Vector2>("MachineCenter");
            cornerPosition = tag.Get<Point16>("CornerPosition");
            MachineHeight = tag.GetInt("MachineHeight");
            MachineWidth = tag.GetInt("MachineWidth");
        }
    }
}
