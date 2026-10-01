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
    public abstract class BaseMachine : ModTileEntity
    {
        #region Variables
        public abstract int ValidTileType { get; }
        public bool isOn { get; set; }
        public bool isWorking { get; protected set; }
        protected int WorkProgress;
        protected virtual int WorkDuration => 120; // 60 ticks = 1 second, override per machine
        protected virtual int OutputSlotCount => 0;

        public CustomRecipe SelectedRecipe;    // the recipe the machine is actually running (matched from the slots each tick)
        public RecipeOutputGroup ManualGroup;  // what the player picked in the browser; null = auto-detect

        public void SetManualGroup(RecipeOutputGroup group)
        {
            ManualGroup = group;
            SelectedRecipe = null;   // the concrete recipe is re-detected from the slots on the next update
        }

        // The recipe groups this machine's browser shows (e.g. FurnaceRecipeRegistry.SmeltingGroups).
        // Used to save and restore the player's manual selection. null = machine has no recipe list.
        protected virtual List<RecipeOutputGroup> RecipeGroups => null;

        public int OutputMaxStack = Item.CommonMaxStack;
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
        public LiquidStack[] InputLiquids => inputLiquids ??= CreateInputLiquids();
        public LiquidStack[] OutputLiquids => outputLiquids ??= CreateLiquidArray(OutputLiquidCount);

        static LiquidStack[] CreateLiquidArray(int count)
        {
            var arr = new LiquidStack[count];
            for (int i = 0; i < count; i++) arr[i] = new LiquidStack();
            return arr;
        }

        // The list this machine's recipes come from (e.g. FurnaceRecipeRegistry.SmeltingRecipes).
        // Used to save and restore the player's manual recipe. null = machine has no recipe list.

        #endregion

        #region Capabilities (override to opt in)

        protected virtual FuelTable AcceptedFuels => null;          // null = no fuel slot
        public virtual RecipeBook Recipes => null;                  // null = no recipe processing
        protected virtual int FuelPerCraft => 1;                    // fuel units one finished craft burns

        // Multi-fuel machines override THIS instead: one channel per fuel needed at the same time.
        protected virtual FuelChannel[] FuelChannels => AcceptedFuels == null
            ? Array.Empty<FuelChannel>()
            : new[] { new FuelChannel(AcceptedFuels, FuelPerCraft) };

        protected virtual int RecipeDuration(CustomRecipe r) => r.DurationTicks ?? WorkDuration;
        protected virtual bool CanStartCraft(CustomRecipe recipe) => true;

        LiquidStack[] CreateInputLiquids()
        {
            LiquidStack[] arr = CreateLiquidArray(InputLiquidCount);
            for (int i = 0; i < arr.Length; i++)
            {
                int tank = i;   // capture a copy per tank
                arr[i].Filter = type => AcceptsInputLiquid(tank, type);
            }
            return arr;
        }

        // What may the pipes put into input tank `tankIndex`? Default: anything a recipe consumes.
        protected virtual bool AcceptsInputLiquid(int tankIndex, int liquidType) =>
            Recipes == null || Recipes.ConsumesLiquid(liquidType);

        // Slot layout convention: [fuel slot (if any)] [ingredient slots...]
        public const int FuelSlotIndex = 0;
        public const int FirstIngredientSlot = FuelSlotIndex + 1;

        FuelModule[] fuels;
        public FuelModule[] Fuels => fuels ??= BuildFuelModules();
        public FuelModule Fuel => Fuels.Length > 0 ? Fuels[0] : null;   // first/only fuel: the furnace keeps using this
        public int FuelSlotCount => Fuels.Length;                        // one slot per channel

        FuelModule[] BuildFuelModules()
        {
            FuelChannel[] channels = FuelChannels;
            var arr = new FuelModule[channels.Length];
            for (int i = 0; i < arr.Length; i++)
                arr[i] = new FuelModule(channels[i].Table, FuelSlotIndex + i, channels[i].UnitsPerCraft);
            return arr;
        }

        protected virtual int InputSlotCount => FuelSlotCount + (Recipes?.MaxIngredientCount ?? 0);
        public int IngredientStart => FuelSlotCount;
        public int IngredientSlotCount => Math.Max(0, InputSlotCount - FuelSlotCount);

        // Item-ingredient slots auto mode (nothing picked in the browser) reads.
        // Recipes with a different item-input count are manual-only.
        protected virtual int AutoIngredientCount => Recipes?.MinIngredientCount ?? 0;

        // How many ingredient slots are shown/used right now
        public int ActiveIngredientCount
        {
            get
            {
                if (IngredientSlotCount == 0) return 0;
                if (Recipes == null) return IngredientSlotCount;    // hand-written machine: every slot is live
                return ManualGroup == null
                    ? Math.Min(AutoIngredientCount, IngredientSlotCount)
                    : Math.Min(ManualGroup.MaxInputCount, IngredientSlotCount);
            }
        }

        bool IsElectric => this is IElectricConsumer || this is IElectricProducer;

        #endregion

        #region Tick

        public override void Update()
        {
            ScanVItems();
            if (!IsElectric) isOn = true;       // only power-network machines are switched by the grid
            ReleaseHiddenIngredientSlots();
            ProcessRecipes();                   // does nothing unless Recipes is overridden
        }

        void ProcessRecipes()
        {
            if (Recipes == null) return;

            bool ready = TryMatchRecipe() && CanStoreOutputs(SelectedRecipe) && CanStartCraft(SelectedRecipe);
            if (!ready) { WorkProgress = 0; isWorking = false; return; }

            if (!AllFuelsAvailable()) { isWorking = false; return; }

            isWorking = true;     // counts toward the grid's demand even during a brownout
            if (!isOn) return;    // browned out: freeze progress, burn nothing

            foreach (FuelModule f in Fuels) f.TryEnsureFuel(InputSlots);

            WorkProgress++;
            if (WorkProgress >= RecipeDuration(SelectedRecipe)) FinishRecipe();
        }

        bool AllFuelsAvailable()
        {
            foreach (FuelModule f in Fuels)
                if (!f.HasFuelAvailable(InputSlots)) return false;
            return true;
        }

        void FinishRecipe()
        {
            CustomRecipe recipe = SelectedRecipe;
            ConsumeInputs(recipe);
            ProduceOutputs(recipe);
            foreach (FuelModule f in Fuels) f.ConsumeCraft();
            WorkProgress = 0;
            OnRecipeFinished(recipe);
        }

        protected virtual void OnRecipeFinished(CustomRecipe recipe) { }   // sounds, dust, extra effects

        public float WorkFraction => SelectedRecipe == null ? 0f : Math.Clamp(WorkProgress / (float)RecipeDuration(SelectedRecipe), 0f, 1f);
        public float GetFuelBurnFraction(int channel = 0)
        {
            if (channel >= Fuels.Length || SelectedRecipe == null) return -1f;
            FuelModule f = Fuels[channel];
            return f.GetBurnFraction(WorkFraction * f.UnitsPerCraft);
        }

        public override void OnKill()
        {
            LiquidNetworkSystem.networkNeedsRebuilding = true;
            if (IsElectric)
            {
                PowerGridSystem.AllMachines.Remove(this);
                PowerGridSystem.gridNeedsRebuilding = true;
            }
            ModContent.GetInstance<MachineUISystem>().NotifyMachineKilled(this);
        }

        #endregion

        #region Recipe matching, consuming, producing

        List<Item> ingredientBuffer;
        List<Item> GetIngredientItems()
        {
            ingredientBuffer ??= new List<Item>();
            ingredientBuffer.Clear();
            for (int s = IngredientStart; s < IngredientStart + ActiveIngredientCount; s++)
                ingredientBuffer.Add(InputSlots[s]);
            return ingredientBuffer;
        }

        bool TryMatchRecipe()
        {
            List<CustomRecipe> candidates = ManualGroup != null ? ManualGroup.Recipes : Recipes.All;
            return CustomRecipe.TryGetRecipeFromList(candidates, GetIngredientItems(), out SelectedRecipe, InputLiquids);
        }

        void ConsumeInputs(CustomRecipe recipe)
        {
            foreach (RecipeIngredient req in recipe.Inputs)
            {
                int remaining = req.Stack;
                for (int s = IngredientStart; s < IngredientStart + ActiveIngredientCount && remaining > 0; s++)
                {
                    Item slot = InputSlots[s];
                    if (slot.IsAir || slot.type != req.Type) continue;

                    int take = Math.Min(remaining, slot.stack);
                    slot.stack -= take;
                    remaining -= take;
                    if (slot.stack <= 0) InputSlots[s] = new Item();
                }
            }

            foreach (LiquidIngredient need in recipe.LiquidInputs)
            {
                foreach (LiquidStack tank in InputLiquids)
                {
                    if (tank.IsEmpty || tank.LiquidType != need.LiquidType || tank.Amount < need.Amount) continue;
                    tank.Amount -= need.Amount;
                    if (tank.Amount <= 0f) tank.LiquidType = -1;
                    break;
                }
            }
        }

        // "claimed" is a bitmask of output slots an earlier output of the same recipe already reserved,
        // so two outputs can't both count on the same empty slot.
        int FindOutputSlot(RecipeIngredient output, int claimed)
        {
            if (output.Stack > OutputMaxStack) return -1;
            int firstEmpty = -1;
            for (int i = 0; i < OutputSlots.Length; i++)
            {
                if ((claimed & (1 << i)) != 0) continue;
                Item slot = OutputSlots[i];
                if (slot.IsAir) { if (firstEmpty == -1) firstEmpty = i; }
                else if (slot.type == output.Type && slot.stack + output.Stack <= OutputMaxStack) return i;
            }
            return firstEmpty;
        }

        int FindLiquidOutputSlot(LiquidIngredient output, int claimed)
        {
            for (int i = 0; i < OutputLiquids.Length; i++)
            {
                if ((claimed & (1 << i)) != 0) continue;
                LiquidStack tank = OutputLiquids[i];
                if (tank.IsEmpty || (tank.LiquidType == output.LiquidType && tank.Amount + output.Amount <= tank.Capacity))
                    return i;
            }
            return -1;
        }

        bool CanStoreOutputs(CustomRecipe recipe)
        {
            int claimed = 0;
            if (!IsOnConveyorFloor())        // a conveyor floor swallows item outputs, so slots don't matter
            {
                foreach (RecipeIngredient o in recipe.Outputs)
                {
                    int slot = FindOutputSlot(o, claimed);
                    if (slot == -1) return false;
                    claimed |= 1 << slot;
                }
            }

            claimed = 0;
            foreach (LiquidIngredient o in recipe.LiquidOutputs)
            {
                int slot = FindLiquidOutputSlot(o, claimed);
                if (slot == -1) return false;
                claimed |= 1 << slot;
            }
            return true;
        }

        void ProduceOutputs(CustomRecipe recipe)
        {
            foreach (RecipeIngredient o in recipe.Outputs)
            {
                if (IsOnConveyorFloor())
                {
                    Vector2 tile = MachineCenter / 16f;
                    VirtualItemSystem.SpawnVirtualItem(o.Type, o.Stack, (int)tile.X, (int)tile.Y);
                    continue;
                }

                int slot = FindOutputSlot(o, 0);
                if (slot == -1) continue;
                int existing = OutputSlots[slot].IsAir ? 0 : OutputSlots[slot].stack;
                OutputSlots[slot] = new Item(o.Type, o.Stack + existing);
            }

            foreach (LiquidIngredient o in recipe.LiquidOutputs)
            {
                int slot = FindLiquidOutputSlot(o, 0);
                if (slot == -1) continue;
                LiquidStack tank = OutputLiquids[slot];
                if (tank.IsEmpty) tank.Amount = 0f;
                tank.LiquidType = o.LiquidType;
                tank.Amount += o.Amount;
            }
        }

        void ReleaseHiddenIngredientSlots()
        {
            for (int s = IngredientStart + ActiveIngredientCount; s < InputSlots.Length; s++)
            {
                Item item = InputSlots[s];
                if (item.IsAir) continue;
                Item.NewItem(new EntitySource_TileEntity(this), Position.X * 16 + 16, Position.Y * 16 + 8, 16, 16, item.type, item.stack);
                InputSlots[s] = new Item();
            }
        }

        #endregion

        #region Conveyor intake

        void ScanVItems()
        {
            if (InputSlotCount == 0) return;

            VirtualItem vItem = null;
            for (int i = cornerPosition.X; i < cornerPosition.X + MachineWidth && vItem == null; i++)
                for (int j = cornerPosition.Y; j < cornerPosition.Y + MachineHeight && vItem == null; j++)
                    vItem = VirtualItemSystem.GetVirtualItemAtTile(i, j);

            if (vItem == null) return;
            if (!TryGetIntakeSlot(vItem.itemType, out int slot, out int limit)) return;
            MoveVItemIntoSlot(vItem, slot, limit);
        }

        // Override for special intake rules (e.g. a machine that only takes items when a tank is full).
        protected virtual bool TryGetIntakeSlot(int itemType, out int slot, out int limit)
        {
            if (TryGetIngredientIntake(itemType, out slot, out limit)) return true;   // ingredients first (same priority the furnace had)

            foreach (FuelModule f in Fuels)
            {
                if (!f.CanAccept(itemType)) continue;
                slot = f.SlotIndex;
                limit = f.StackLimit(itemType);
                return true;
            }

            slot = -1; limit = 0;
            return false;
        }

        bool TryGetIngredientIntake(int itemType, out int slot, out int limit)
        {
            slot = -1; limit = 0;
            if (ActiveIngredientCount == 0) return false;

            foreach (CustomRecipe recipe in GetPickupRecipes())
            {
                if (ManualGroup == null && recipe.Inputs.Count != ActiveIngredientCount) continue;
                foreach (RecipeIngredient ing in recipe.Inputs)
                {
                    if (ing.Type != itemType) continue;
                    limit = Math.Max(limit, ing.Stack * 2);
                    break;
                }
            }
            if (limit == 0) return false;

            slot = FindIngredientSlotFor(itemType);
            return slot != -1;
        }

        protected virtual IEnumerable<CustomRecipe> GetPickupRecipes()
        {
            if (Recipes != null) return ManualGroup != null ? ManualGroup.Recipes : Recipes.All;
            return SelectedRecipe != null ? new[] { SelectedRecipe } : Array.Empty<CustomRecipe>();
        }

        void MoveVItemIntoSlot(VirtualItem vItem, int slot, int limit)
        {
            Item target = InputSlots[slot];
            if (!target.IsAir && target.type != vItem.itemType) return;

            int space = limit - target.stack;
            if (space <= 0) return;

            int amount = Math.Min(space, vItem.stackSize);
            InputSlots[slot] = new Item(vItem.itemType, target.stack + amount);
            vItem.stackSize -= amount;
        }

        int FindIngredientSlotFor(int itemType)
        {
            if (!FitsWithHeldIngredients(itemType)) return -1;

            int firstEmpty = -1;
            for (int s = IngredientStart; s < IngredientStart + ActiveIngredientCount; s++)
            {
                Item slot = InputSlots[s];
                if (slot.IsAir) { if (firstEmpty == -1) firstEmpty = s; }
                else if (slot.type == itemType) return s;
            }
            return firstEmpty;
        }

        bool FitsWithHeldIngredients(int itemType)
        {
            if (ManualGroup == null) return true;

            foreach (CustomRecipe recipe in ManualGroup.Recipes)
            {
                if (!RecipeUsesItem(recipe, itemType)) continue;

                bool fitsHeld = true;
                for (int s = IngredientStart; s < IngredientStart + ActiveIngredientCount && fitsHeld; s++)
                {
                    Item held = InputSlots[s];
                    if (!held.IsAir && !RecipeUsesItem(recipe, held.type)) fitsHeld = false;
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

        #endregion

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
        
        static string FuelKey(string name, int i) => i == 0 ? name : name + i;
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

            if (ManualGroup != null)
            {
                if (ManualGroup.Key.IsLiquid) tag["ManualGroupLiquid"] = LiquidTypeRegistry.Get(ManualGroup.Key.Id).Name;
                else tag["ManualGroupOutput"] = new Item(ManualGroup.Key.Id, 1);   // old key, old worlds still load
            }

            tag["WorkProgress"] = WorkProgress;
            if (Fuel != null) 
            { 
                tag["FuelRemaining"] = Fuel.Remaining; 
                tag["FuelCapacity"] = Fuel.Capacity; 
            }

            for (int i = 0; i < Fuels.Length; i++)
            {
                tag[FuelKey("FuelRemaining", i)] = Fuels[i].Remaining;
                tag[FuelKey("FuelCapacity", i)] = Fuels[i].Capacity;
            }
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

            if (Recipes != null)
            {
                RecipeOutputGroup group = null;
                if (tag.ContainsKey("ManualGroupOutput"))
                {
                    Item saved = tag.Get<Item>("ManualGroupOutput");
                    if (!saved.IsAir)
                        group = Recipes.Groups.FirstOrDefault(g => !g.Key.IsLiquid && g.Key.Id == saved.type);
                }
                else if (tag.ContainsKey("ManualGroupLiquid"))
                {
                    string name = tag.GetString("ManualGroupLiquid");
                    group = Recipes.Groups.FirstOrDefault(g => g.Key.IsLiquid && LiquidTypeRegistry.Get(g.Key.Id).Name == name);
                }
                if (group != null) SetManualGroup(group);
            }

            WorkProgress = tag.GetInt("WorkProgress");
            Fuel?.Restore(tag.GetInt("FuelRemaining"), tag.GetInt("FuelCapacity"));

            for (int i = 0; i < Fuels.Length; i++)
                Fuels[i].Restore(tag.GetInt(FuelKey("FuelRemaining", i)), tag.GetInt(FuelKey("FuelCapacity", i)));
        }
        
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

    }
}
