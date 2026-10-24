using Factorraria.Common.Knowledge;
using Factorraria.Common.Liquids;
using Factorraria.Common.Net;
using Factorraria.Common.Systems;
using Factorraria.Common.UI;
using Factorraria.Content.VirtualItems;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
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
        // Wire switch (vanilla Terraria wires; electric consumers and producers only, see ToggleByWire). A wire pulse flips WireToggle
        // on the server / in single-player, and it is synced as flag bit 4 and saved. While it is true the machine reads as switched off:
        // isOn and isWorking are MASKED when read, so everything that calculates them keeps writing the raw values as before and
        // everything that reads them sees the override (PowerNetwork demand and supply, the machine's own "if (!isOn) return", visuals,
        // sync). Masked isWorking takes the machine out of the grid's supply / demand; masked isOn freezes it exactly like a brownout.
        bool rawOn;
        bool rawWorking;
        public bool WireToggle;
        public bool isOn { get => rawOn && !WireToggle; set => rawOn = value; }
        public bool isWorking { get => rawWorking && !WireToggle; protected set => rawWorking = value; }
        protected int WorkProgress;
        public int CraftsFinished;   // +1 per finished craft (runtime only); the output slot UI watches it for the finish sound
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

        public int OutputMaxStack = Item.CommonMaxStack;   // fallback output cap for machines or items no recipe knows
        public int OutputBufferMultiplier = 10;            // an output slot holds this many crafts' worth of the product
        public int IngredientBufferMultiplier = 2;         // an input slot holds this many crafts' worth (conveyor intake AND hand deposits)
        public MachineBiome InsideBiome { get; set; } = MachineBiome.Unknown;
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

        /// <summary>The slot array a slot edit (patch 6.2) addresses.</summary>
        public Item[] SlotsOf(MachineSlotKind kind) => kind == MachineSlotKind.Output ? OutputSlots : InputSlots;

        /// <summary>
        /// The one accept filter for putting an item into a slot. The UI (UIItemSlotWrapper) uses it to refuse the cursor item and, on a
        /// multiplayer client, the server uses it to validate a slot edit (patch 6.2), so both always agree. Taking items out is never
        /// filtered. Default: everything goes in, except an ingredient slot that is hidden right now (ReleaseHiddenIngredientSlots would
        /// throw its content out). Override for machines with their own rules (the Robotic Arm).
        /// </summary>
        public virtual bool AcceptsSlotItem(MachineSlotKind kind, int index, Item item)
        {
            if (kind == MachineSlotKind.Input && index >= IngredientStart + ActiveIngredientCount)
                return false;

            return true;
        }

        /// <summary>
        /// The most `item` may hold in this slot when it is put in by hand. Same rules the conveyor intake uses (ingredients: a multiple of
        /// the recipe's count, fuel: the fuel table's StackLimit) plus outputs: a multiple of the product's count per craft. Taking items
        /// out is never limited and a stack that is already over the cap (an old save) is left alone by the callers.
        /// UIItemSlotWrapper clamps the click with it (MachineSlotEdit.ClampAfterClick); the server's slot edit check refuses anything above it.
        /// </summary>
        public virtual int GetSlotCap(MachineSlotKind kind, int index, Item item)
        {
            if (item == null || item.IsAir) return 0;
            int stackMax = Math.Max(1, item.maxStack);

            if (kind == MachineSlotKind.Output)
                return Math.Min(OutputCapFor(item.type), stackMax);

            if (index < IngredientStart)
            {
                foreach (FuelModule f in Fuels)
                    if (f.SlotIndex == index && f.CanAccept(item.type))
                        return Math.Min(Math.Max(f.StackLimit(item.type), 1), stackMax);
                return stackMax;
            }

            if (Recipes == null)   // hand-written machine: whatever its own intake rule allows
            {
                if (TryGetIntakeSlot(item.type, out int intakeSlot, out int intakeLimit) && intakeSlot == index)
                    return Math.Min(Math.Max(intakeLimit, 1), stackMax);
                return stackMax;
            }

            return Math.Min(IngredientCapFor(index, item.type), stackMax);
        }

        Dictionary<int, int> outputCapCache;

        // Output slot cap for a product: OutputBufferMultiplier x the biggest per-craft amount any recipe of this machine makes of it.
        public int OutputCapFor(int itemType)
        {
            outputCapCache ??= new Dictionary<int, int>();
            if (outputCapCache.TryGetValue(itemType, out int cached)) return cached;

            int perCraft = 0;
            if (Recipes != null)
                foreach (CustomRecipe recipe in Recipes.All)
                    foreach (RecipeIngredient o in recipe.Outputs)
                        if (o.Type == itemType) perCraft = Math.Max(perCraft, o.Stack);

            int cap = perCraft > 0 ? perCraft * OutputBufferMultiplier : OutputMaxStack;
            outputCapCache[itemType] = cap;
            return cap;
        }

        // Hand deposit into an ingredient slot. Auto mode detects the recipe from what the slots would hold right after the deposit
        // (the other filled slots plus this item), so the cap is known in the same frame as the click, before anything is taken.
        int IngredientCapFor(int slotIndex, int itemType)
        {
            List<CustomRecipe> pool = ManualGroup != null ? ManualGroup.Recipes : Recipes.All;

            int filled = 1;   // this slot
            for (int s = IngredientStart; s < IngredientStart + ActiveIngredientCount; s++)
            {
                if (s == slotIndex) continue;
                Item held = InputSlots[s];
                if (!held.IsAir && held.stack > 0) filled++;
            }

            int best = ScanIngredientCap(pool, slotIndex, itemType, ManualGroup == null ? filled : -1);
            if (best == 0) best = ScanIngredientCap(pool, slotIndex, itemType, -1);   // no recipe fits yet (more ingredients to come): loosest recipe that uses it
            return Math.Max(best, 1);
        }

        int ScanIngredientCap(List<CustomRecipe> pool, int slotIndex, int itemType, int exactInputCount)
        {
            int best = 0;
            foreach (CustomRecipe recipe in pool)
            {
                if (exactInputCount >= 0 && recipe.Inputs.Count != exactInputCount) continue;

                int need = 0;
                foreach (RecipeIngredient ing in recipe.Inputs)
                    if (ing.Type == itemType) { need = ing.Stack; break; }
                if (need == 0) continue;

                bool fitsHeld = true;
                for (int s = IngredientStart; s < IngredientStart + ActiveIngredientCount && fitsHeld; s++)
                {
                    if (s == slotIndex) continue;
                    Item held = InputSlots[s];
                    if (!held.IsAir && !RecipeUsesItem(recipe, held.type)) fitsHeld = false;
                }
                if (fitsHeld) best = Math.Max(best, need * IngredientBufferMultiplier);
            }
            return best;
        }

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

        // The tanks recipes read and consume: every input tank EXCEPT the liquid-fuel tanks (those sit first in InputLiquids).
        LiquidStack[] recipeInputLiquids;
        public LiquidStack[] RecipeInputLiquids => recipeInputLiquids ??= InputLiquids[LiquidFuelCount..];
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

        // Liquid fuels: same idea as the item fuels above, but burned straight out of a dedicated input tank.
        // Single-fuel machines override AcceptedLiquidFuels (+ LiquidFuelPerCraft); multi-fuel machines override LiquidFuelChannels.
        protected virtual LiquidFuelTable AcceptedLiquidFuels => null;   // null = no liquid fuel tank
        protected virtual int LiquidFuelPerCraft => 1;                   // liquid fuel units one finished craft burns

        protected virtual LiquidFuelChannel[] LiquidFuelChannels => AcceptedLiquidFuels == null
            ? Array.Empty<LiquidFuelChannel>()
            : new[] { new LiquidFuelChannel(AcceptedLiquidFuels, LiquidFuelPerCraft) };

        protected virtual int RecipeDuration(CustomRecipe r) => r.DurationTicks ?? WorkDuration;

        /// <summary>Public read of the time a recipe takes in this machine (used to auto-fill CustomRecipe.DurationTicks at load).</summary>
        public int ResolveRecipeDuration(CustomRecipe r) => RecipeDuration(r);
        protected virtual bool CanStartCraft(CustomRecipe recipe) => true;

        // Pipes treat a world liquid pool as bottomless when a sink machine returns true for that liquid (Magic Materializer + shimmer).
        public virtual bool TreatsWorldLiquidAsInfinite(int liquidType) => false;

        LiquidStack[] CreateInputLiquids()
        {
            int fuelTanks = LiquidFuelCount;   // liquid-fuel tanks first, recipe tanks after
            LiquidStack[] arr = CreateLiquidArray(fuelTanks + InputLiquidCount);
            for (int i = 0; i < arr.Length; i++)
            {
                int tank = i;   // capture a copy per tank
                if (tank < fuelTanks)
                {
                    // A fuel tank only takes liquids its own fuel table can burn.
                    arr[i].Filter = LiquidFuels[tank].CanAccept;
                }
                else
                {
                    arr[i].Filter = type => AcceptsInputLiquid(tank - fuelTanks, type);
                }
            }
            return arr;
        }

        // What may the pipes put into input tank `tankIndex`? Default: anything a recipe consumes.
        // tankIndex counts RECIPE tanks only; liquid-fuel tanks are filtered by their own LiquidFuelTable.
        protected virtual bool AcceptsInputLiquid(int tankIndex, int liquidType) =>
            Recipes == null || Recipes.ConsumesLiquid(liquidType);

        // Slot layout convention: [fuel slot (if any)] [ingredient slots...]
        public const int FuelSlotIndex = 0;
        public const int FirstIngredientSlot = FuelSlotIndex + 1;

        FuelModule[] fuels;
        public FuelModule[] Fuels => fuels ??= BuildFuelModules();
        public FuelModule Fuel => Fuels.Length > 0 ? Fuels[0] : null;   // first/only fuel: the furnace keeps using this
        public int FuelSlotCount => Fuels.Length;                        // one slot per channel

        // Liquid fuels live in the FIRST input tanks (one per channel); recipe tanks come after them.
        LiquidFuelModule[] liquidFuels;
        public LiquidFuelModule[] LiquidFuels => liquidFuels ??= BuildLiquidFuelModules();
        public int LiquidFuelCount => LiquidFuels.Length;

        LiquidFuelModule[] BuildLiquidFuelModules()
        {
            LiquidFuelChannel[] channels = LiquidFuelChannels;
            var arr = new LiquidFuelModule[channels.Length];
            for (int i = 0; i < arr.Length; i++)
                arr[i] = new LiquidFuelModule(channels[i].Table, i, channels[i].UnitsPerCraft);
            return arr;
        }

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

        // Auto mode shows one extra empty slot after the last filled one, so the player can experiment
        // with multi-ingredient recipes without picking a recipe first. Override to false for the old fixed behaviour.
        protected virtual bool ProgressiveIngredientSlots => true;

        // How many ingredient slots are shown/used right now
        public int ActiveIngredientCount
        {
            get
            {
                if (IngredientSlotCount == 0) return 0;
                if (Recipes == null) return IngredientSlotCount;    // hand-written machine: every slot is live
                if (ManualGroup != null) return Math.Min(ManualGroup.MaxInputCount, IngredientSlotCount);

                int auto = Math.Min(AutoIngredientCount, IngredientSlotCount);
                if (!ProgressiveIngredientSlots) return auto;

                // "next available slot": always one past the last filled slot, never fewer than the auto count.
                // Because the count is always above the last filled slot, auto mode can never eject items.
                return Math.Min(IngredientSlotCount, Math.Max(auto, LastFilledIngredientIndex() + 2));
            }
        }

        // Auto mode's "next available" slot: the empty slot shown after the last filled one so the player can try
        // multi-ingredient recipes. 0-based relative to IngredientStart; -1 when there is none (manual recipe,
        // fixed slots, nothing beyond the auto count, or that slot already holds an item).
        public int ExtraIngredientIndex
        {
            get
            {
                if (IngredientSlotCount == 0 || Recipes == null || ManualGroup != null || !ProgressiveIngredientSlots) return -1;

                int active = ActiveIngredientCount;
                if (active <= Math.Min(AutoIngredientCount, IngredientSlotCount)) return -1;

                int last = active - 1;
                int s = IngredientStart + last;
                Item[] slots = InputSlots;
                if (s < slots.Length && slots[s] != null && !slots[s].IsAir && slots[s].stack > 0) return -1;
                return last;
            }
        }

        // inputSlotIndex is an index into InputSlots (the same one InputSlotEntry takes).
        public bool IsExtraIngredientSlot(int inputSlotIndex)
        {
            int extra = ExtraIngredientIndex;
            return extra >= 0 && inputSlotIndex == IngredientStart + extra;
        }

        // Ingredient slots without the empty extra one. UIs that centre things on the slot row use this, so the
        // row only re-centres once the player actually puts an item into the extra slot.
        public int SettledIngredientCount => ActiveIngredientCount - (ExtraIngredientIndex >= 0 ? 1 : 0);

        // Slots conveyors may fill. Conveyors keep the old fixed behaviour in auto mode; the extra
        // progressive slots are for hand experimentation only.
        protected virtual int IntakeIngredientCount
        {
            get
            {
                if (IngredientSlotCount == 0) return 0;
                if (Recipes == null) return IngredientSlotCount;
                return ManualGroup == null
                    ? Math.Min(AutoIngredientCount, IngredientSlotCount)
                    : ActiveIngredientCount;
            }
        }

        // 0-based index (relative to IngredientStart) of the highest non-empty ingredient slot, or -1.
        int LastFilledIngredientIndex()
        {
            Item[] slots = InputSlots;
            for (int i = IngredientSlotCount - 1; i >= 0; i--)
            {
                int s = IngredientStart + i;
                if (s < slots.Length && slots[s] != null && !slots[s].IsAir && slots[s].stack > 0) return i;
            }
            return -1;
        }

        bool IsElectric => this is IElectricConsumer || this is IElectricProducer;

        // Whether a wire signal does anything to this machine (general machines such as the Furnace ignore wires).
        public bool CanBeWireToggled => IsElectric;

        long lastWireTick = -1;

        // Flips the wire switch. Runs where wires run (the server and single-player; vanilla never runs wiring on a client, and a client
        // would be refused here anyway). One flip per game tick, so a wire touching several tiles of this machine, or a switch wired
        // with two colours, counts as one pulse. Returns whether it flipped.
        public bool ToggleByWire()
        {
            if (!IsElectric) return false;
            if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient) return false;
            if (lastWireTick == Main.GameUpdateCount) return false;

            lastWireTick = Main.GameUpdateCount;
            WireToggle = !WireToggle;
            return true;
        }

        #endregion

        #region Tick

        // Patch 4.1 (M2): the footprint numbers come from TileObjectData, which exists on a dedicated server, instead of from draw
        // code. The entity always lives at the top-left corner of its tiles, so the corner is simply Position. Called from
        // OnPlaced, from the start of Update (covers entities loaded from the world, which never run OnPlaced) and from the draw
        // override. Returns false (and stays uninitialised, so it is retried) while the tile data or the position is not usable.
        public bool InitializeGeometry()
        {
            if (MachineInitialized) return true;
            if (Position.X <= 0 && Position.Y <= 0) return false;

            TileObjectData data = TileObjectData.GetTileData(ValidTileType, 0);
            if (data == null) return false;

            cornerPosition = Position;
            MachineWidth = data.Width;
            MachineHeight = data.Height;
            MachineCenter = cornerPosition.ToVector2() * 16 + new Vector2(MachineWidth, MachineHeight) * 8f;
            MachineInitialized = true;

            if (Main.netMode == Terraria.ID.NetmodeID.Server)
                FactorrariaNet.Log("[Machines] geometry initialised: " + GetType().Name + " at (" + Position.X + ", " + Position.Y + ") " + MachineWidth + "x" + MachineHeight);

            return true;
        }

        public override void Update()
        {
            InitializeGeometry();
            ScanVItems();
            ReleaseOutputSlotsToConveyor();
            if (!IsElectric) isOn = true;       // only power-network machines are switched by the grid
            ReleaseHiddenIngredientSlots();
            ProcessRecipes();                   // does nothing unless Recipes is overridden
        }

        CustomRecipe runningRecipe;   // recipe matched on the previous tick (runtime only)

        void ProcessRecipes()
        {
            if (Recipes == null) return;

            bool ready = TryMatchRecipe() && CanStoreOutputs(SelectedRecipe) && CanStartCraft(SelectedRecipe);
            if (!ready) { WorkProgress = 0; isWorking = false; runningRecipe = null; return; }

            // The matched recipe can change mid-craft (a second ingredient was added, or another recipe was picked):
            // progress must not carry over from the old recipe. runningRecipe is not saved, so a world load never resets.
            if (runningRecipe != null && !ReferenceEquals(runningRecipe, SelectedRecipe)) WorkProgress = 0;
            runningRecipe = SelectedRecipe;

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
            foreach (LiquidFuelModule lf in LiquidFuels)
                if (!lf.HasFuelAvailable(InputLiquids)) return false;
            return true;
        }

        void FinishRecipe()
        {
            CustomRecipe recipe = SelectedRecipe;
            ConsumeInputs(recipe);
            ProduceOutputs(recipe);
            foreach (FuelModule f in Fuels) f.ConsumeCraft();
            foreach (LiquidFuelModule lf in LiquidFuels) lf.ConsumeCraft(InputLiquids);
            WorkProgress = 0;

            // Discovery: finishing a recipe marks it crafted for the whole world.
            if (recipe.TryGetPrimaryOutput(out RecipeOutputKey discoveredKey))
            {
                // Recipe book: credit the craft to this machine's recipe. The server (or single-player) marks it and tells every
                // client; a multiplayer client does nothing here and learns it from that broadcast (patch 3.2).
                string machineName = RecipeCatalog.NameOf(Recipes);
                if (machineName != null)
                    RecipeKnowledgeNet.ServerMarkCrafted(RecipeCatalog.KeyFor(machineName, discoveredKey));
            }

            CraftsFinished++;
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
            MachineDetailSystem.NotifyMachineKilled(this);   // patch 5.3: server only, closes the UI of every client that has this machine open
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
            return CustomRecipe.TryGetRecipeFromList(candidates, GetIngredientItems(), out SelectedRecipe, RecipeInputLiquids);
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
                foreach (LiquidStack tank in RecipeInputLiquids)
                {
                    if (tank.IsEmpty || !need.Matches(tank.LiquidType) || tank.Amount < need.Amount) continue;
                    tank.Amount -= need.Amount;
                    if (tank.Amount <= 0f) tank.LiquidType = -1;
                    break;
                }
            }
        }

        // Item type the current recipe will put into output slot `slotIndex`, or -1 if none.
        // Uses the same placement rules as CanStoreOutputs/ProduceOutputs (claimed bitmask).
        public int GetPendingOutputType(int slotIndex)
        {
            // On a conveyor the output can ride the belt or fall back to a slot depending on traffic,
            // so just show recipe output i in slot i.
            if (IsOnConveyorFloor() && SelectedRecipe != null)
                return slotIndex < SelectedRecipe.Outputs.Count ? SelectedRecipe.Outputs[slotIndex].Type : -1;

            CustomRecipe recipe = SelectedRecipe;
            if (recipe == null)
            {
                // Manual pick with no matching ingredients yet: show it in the first slot
                if (slotIndex == 0 && ManualGroup != null && !ManualGroup.Key.IsLiquid) return ManualGroup.Key.Id;
                return -1;
            }

            int claimed = 0;
            foreach (RecipeIngredient o in recipe.Outputs)
            {
                int slot = FindOutputSlot(o, claimed);
                if (slot == -1) return -1;
                if (slot == slotIndex) return o.Type;
                claimed |= 1 << slot;
            }
            return -1;
        }

        // "claimed" is a bitmask of output slots an earlier output of the same recipe already reserved,
        // so two outputs can't both count on the same empty slot.
        int FindOutputSlot(RecipeIngredient output, int claimed)
        {
            int cap = Math.Max(OutputCapFor(output.Type), output.Stack);   // one craft must always fit an empty slot
            int firstEmpty = -1;
            for (int i = 0; i < OutputSlots.Length; i++)
            {
                if ((claimed & (1 << i)) != 0) continue;
                Item slot = OutputSlots[i];
                if (slot.IsAir) { if (firstEmpty == -1) firstEmpty = i; }
                else if (slot.type == output.Type && slot.stack + output.Stack <= cap) return i;
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
            int freeTiles = IsOnConveyorFloor() ? CountFreeFootprintTiles() : 0;   // on a conveyor, each output can take one free footprint tile
            foreach (RecipeIngredient o in recipe.Outputs)
            {
                if (freeTiles > 0) { freeTiles--; continue; }

                int slot = FindOutputSlot(o, claimed);
                if (slot == -1) return false;                                       // footprint clogged AND slot full -> stop
                claimed |= 1 << slot;
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
            bool onConveyor = IsOnConveyorFloor();
            foreach (RecipeIngredient o in recipe.Outputs)
            {
                // Spawning registers the tile, so the next output automatically sees it as taken.
                if (onConveyor && TryFindConveyorOutputTile(out Point outTile)
                    && VirtualItemSystem.SpawnVirtualItem(o.Type, o.Stack, outTile.X, outTile.Y) != null)
                    continue;

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
                Item.NewItem(new EntitySource_TileEntity(this), Position.X * 16 + 16, Position.Y * 16 + 8, 16, 16, item);
                InputSlots[s] = new Item();
            }
        }

        #endregion

        #region Conveyor intake

        // On a conveyor, items that piled up in the output slots while the footprint was clogged are released
        // back into the world, one at a time, as soon as a footprint tile is free again.
        void ReleaseOutputSlotsToConveyor()
        {
            if (OutputSlotCount == 0) return;

            Item[] slots = OutputSlots;
            for (int i = 0; i < slots.Length; i++)
            {
                Item slot = slots[i];
                if (slot.IsAir) continue;
                if (!IsOnConveyorFloor()) return;
                if (!TryFindConveyorOutputTile(out Point tile)) return;   // still clogged

                Item one = slot.Clone();
                one.stack = 1;
                if (VirtualItemSystem.SpawnVirtualItem(one, tile.X, tile.Y) == null) continue;

                slot.stack--;
                if (slot.stack <= 0) slots[i] = new Item();
            }
        }

        void ScanVItems()
        {
            if (InputSlotCount == 0) return;

            // Look at every intake tile and take from the first vItem this machine can actually use,
            // so an unusable vItem (e.g. a fresh product) never blocks the ones behind it.
            foreach (Point tile in IntakeTiles())
            {
                VirtualItem vItem = VirtualItemSystem.GetVirtualItemAtTile(tile.X, tile.Y);
                if (vItem == null || vItem.stackSize <= 0) continue;
                if (!TryGetIntakeSlot(vItem.itemType, out int slot, out int limit)) continue;

                int before = vItem.stackSize;
                MoveVItemIntoSlot(vItem, slot, limit);
                if (vItem.stackSize != before) return;   // took something; one vItem per tick
            }
        }

        // The tiles ScanVItems pulls vItems from. Default: the machine's own footprint. Override for machines
        // that sit outside the conveyor (e.g. a 1x1 solid tile that reads its neighbours).
        protected virtual IEnumerable<Point> IntakeTiles()
        {
            for (int i = cornerPosition.X; i < cornerPosition.X + MachineWidth; i++)
                for (int j = cornerPosition.Y; j < cornerPosition.Y + MachineHeight; j++)
                    yield return new Point(i, j);
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
            if (IntakeIngredientCount == 0) return false;

            foreach (CustomRecipe recipe in GetPickupRecipes())
            {
                if (ManualGroup == null && recipe.Inputs.Count != IntakeIngredientCount) continue;
                foreach (RecipeIngredient ing in recipe.Inputs)
                {
                    if (ing.Type != itemType) continue;
                    limit = Math.Max(limit, ing.Stack * IngredientBufferMultiplier);
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

            // Same type AND same prefix/mod data, otherwise the vItem stays where it is (same as a full slot)
            if (!target.IsAir && !VItemStacking.CanMerge(target, vItem.Payload)) return;

            int space = limit - target.stack;
            if (space <= 0) return;

            int amount = Math.Min(space, vItem.stackSize);

            if (target.IsAir)
            {
                Item placed = vItem.Payload.Clone();
                placed.stack = amount;
                InputSlots[slot] = placed;
            }
            else
            {
                Item merged = target.Clone();
                merged.stack = target.stack + amount;
                InputSlots[slot] = merged;
            }

            vItem.stackSize -= amount;
        }

        int FindIngredientSlotFor(int itemType)
        {
            if (!FitsWithHeldIngredients(itemType)) return -1;

            int firstEmpty = -1;
            for (int s = IngredientStart; s < IngredientStart + IntakeIngredientCount; s++)
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

        // Output tile for vItems this machine produces. Bottom row first; inside a row the columns go outward
        // from the bottom-center (odd width: the single center tile, even width: the two middle tiles, left then right);
        // then the rows above. A tile qualifies when no vItem is on or heading to it.
        // Returns false when the whole footprint is full, which means "clogged" (outputs fall back to the slots).
        protected bool TryFindConveyorOutputTile(out Point result)
        {
            result = default;
            if (!MachineInitialized || MachineWidth <= 0 || MachineHeight <= 0) return false;

            int left = (MachineWidth - 1) / 2;
            int right = MachineWidth / 2;

            for (int row = MachineHeight - 1; row >= 0; row--)
            {
                for (int k = 0; k < MachineWidth; k++)
                {
                    if (TryGetFreeFootprintTile(left - k, row, out result)) return true;
                    if (right + k != left - k && TryGetFreeFootprintTile(right + k, row, out result)) return true;
                }
            }
            return false;
        }

        bool TryGetFreeFootprintTile(int col, int row, out Point tile)
        {
            tile = default;
            if (col < 0 || col >= MachineWidth) return false;
            tile = new Point(cornerPosition.X + col, cornerPosition.Y + row);
            return !VirtualItemSystem.IsTileOccupied(tile.X, tile.Y);
        }

        int CountFreeFootprintTiles()
        {
            if (!MachineInitialized) return 0;

            int free = 0;
            for (int i = cornerPosition.X; i < cornerPosition.X + MachineWidth; i++)
                for (int j = cornerPosition.Y; j < cornerPosition.Y + MachineHeight; j++)
                    if (!VirtualItemSystem.IsTileOccupied(i, j)) free++;
            return free;
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

        // Single "player just placed this machine" entry point. Called for BOTH ModTile machines
        // (via Hook_AfterPlacement) and vanilla-tile machines (via MachineVisualOverride.PlaceInWorld).
        // i, j is always the entity's top-left corner.
        public virtual void OnPlaced(int i, int j)
        {
            InitializeGeometry();
            InsideBiome = DetermineBiomeAt(i, j);
            MachinePlacementInfo.OnServerPlaced(this);   // patch 4.3: on a dedicated server the placing client's biome arrives in a follow-up packet
            //Main.NewText(InsideBiome);
        }

        /// <summary>
        /// Plain Water arriving from a pipe becomes this machine's BIOME water (Snow Water in a snow biome, ...)
        /// when one of its recipes actually uses that variant (directly, or through AnyWater).
        /// Everything else (lava, oil, already-specific waters, machines without recipes) passes through unchanged.
        /// </summary>
        public int ResolveIncomingLiquid(int liquidType)
        {
            if (liquidType != LiquidTypeRegistry.Water || Recipes == null) return liquidType;

            int biomeWater = LiquidTypeRegistry.WaterFor(InsideBiome);
            return biomeWater != liquidType && Recipes.ConsumesLiquid(biomeWater) ? biomeWater : liquidType;
        }

        public virtual void OnRightClick(int i, int j)
        {
            if (MachineUIRegistry.Definitions.ContainsKey(ValidTileType))
            {
                ModContent.GetInstance<MachineUISystem>().OpenUI(i, j, ValidTileType, this);
            }
        }

        protected virtual void OnAnimationFrameChanged(int newFrame, int previousFrame) { }

        // ---- Startup / shutdown (patch: machine startup + shutdown animations) ----
        // "Running" means isOn && isWorking. Both are already synced to clients (flags in the snapshot and the light state), so every
        // process that draws the machine (single-player and each multiplayer client) detects the change itself, from MachineTransitionSystem.
        // Nothing here is saved or sent. A dedicated server has no screen and never runs it.
        public MachineTransition Transition { get; private set; }   // the one-shot animation playing right now (None = normal On/Off art)
        public uint TransitionStartTick { get; private set; }       // Main.GameUpdateCount when it began
        bool transitionSeeded;
        bool lastRunningState;

        // Override for custom sounds, dust or particles. Called once per start / stop, client-side only, whether or not
        // a startup / shutdown animation is registered for the machine. It fires wherever the machine is, so check
        // BaseMachine.IsOnScreen(...) yourself if an effect should only happen near the player. Don't change gameplay state here.
        protected virtual void OnStartup() { }
        protected virtual void OnShutdown() { }

        // Called every tick by MachineTransitionSystem. The first observation of a machine (world load, joining a server,
        // a machine placed while off) only records its state, so nothing plays for a machine that was already running.
        internal void UpdateVisualTransition(MachineVisualDefinition def)
        {
            bool running = isOn && isWorking;

            if (!transitionSeeded)
            {
                transitionSeeded = true;
                lastRunningState = running;
                return;
            }

            if (running != lastRunningState)
            {
                lastRunningState = running;
                Transition = running ? MachineTransition.Startup : MachineTransition.Shutdown;
                TransitionStartTick = Main.GameUpdateCount;

                if (running) OnStartup();
                else OnShutdown();
            }

            if (Transition == MachineTransition.None)
                return;

            // Flipping again mid-animation simply starts the opposite one from its first frame (handled above).
            int duration = def?.GetTransitionDuration(Transition) ?? 0;
            if (duration <= 0 || Main.GameUpdateCount - TransitionStartTick >= (uint)duration)
                Transition = MachineTransition.None;
        }
        
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

            tag["InsideBiome"] = (int)InsideBiome;
            if (WireToggle) tag["WireToggle"] = true;   // written only when switched off, so old and new saves both load

            for (int i = 0; i < Fuels.Length; i++)
            {
                tag[FuelKey("FuelRemaining", i)] = Fuels[i].Remaining;
                tag[FuelKey("FuelCapacity", i)] = Fuels[i].Capacity;
            }
        }
        public override void LoadData(TagCompound tag)
        {
            if (tag.ContainsKey("InsideBiome"))
                InsideBiome = (MachineBiome)tag.GetInt("InsideBiome");

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
            WireToggle = tag.ContainsKey("WireToggle") && tag.GetBool("WireToggle");
            Fuel?.Restore(tag.GetInt("FuelRemaining"), tag.GetInt("FuelCapacity"));

            for (int i = 0; i < Fuels.Length; i++)
                Fuels[i].Restore(tag.GetInt(FuelKey("FuelRemaining", i)), tag.GetInt(FuelKey("FuelCapacity", i)));
        }
        
        #region Network snapshot (patch 5.1)

        // Server -> client copy of this machine, written whenever tModLoader sends the entity: with the tile section data (a client
        // joins or reaches a section it has not received) and with vanilla's tile-entity sharing message 86 (MachinePlacement.Share).
        // NetSend runs on the server only, NetReceive on a client only, never in single-player. The payload is length-prefixed so a short
        // or unreadable payload cannot shift the rest of the stream. Geometry is derived, not sent: NetReceive recomputes it.
        // Subclasses with extra state override WriteSnapshotExtra / ReadSnapshotExtra (call base first, read in the order written),
        // never NetSend itself.
        public override void NetSend(BinaryWriter writer)
        {
            byte[] payload = BuildSnapshot();
            writer.Write7BitEncodedInt(payload.Length);
            writer.Write(payload);
            MachineSnapshot.CountSent(payload.Length);
        }

        // Called on a NEW instance that replaces any entity at this position (ID is not assigned yet, Position is). May run on the
        // network thread: no UI, textures or world access in here or in ApplySnapshot.
        public override void NetReceive(BinaryReader reader)
        {
            int length = reader.Read7BitEncodedInt();
            byte[] payload = length > 0 ? reader.ReadBytes(length) : Array.Empty<byte>();
            MachineSnapshot.CountReceived(payload.Length);

            try
            {
                using var stream = new MemoryStream(payload);
                using var payloadReader = new BinaryReader(stream);
                ApplySnapshot(payloadReader);
            }
            catch (Exception e)
            {
                MachineSnapshot.CountFailed();
                Mod?.Logger.Warn("[Machines] snapshot of " + GetType().Name + " at (" + Position.X + ", " + Position.Y + ") could not be read: " + e.Message);
            }

            InitializeGeometry();
        }

        public byte[] BuildSnapshot()
        {
            var stream = new MemoryStream();
            var w = new BinaryWriter(stream);

            w.Write((byte)1);   // snapshot format version

            byte flags = 0;
            if (isOn) flags |= 1;
            if (isWorking) flags |= 2;
            if (WireToggle) flags |= 4;
            w.Write(flags);
            w.Write((byte)InsideBiome);
            w.Write7BitEncodedInt(Math.Max(0, WorkProgress));
            w.Write7BitEncodedInt(Math.Max(0, CraftsFinished));
            if (IsElectric) w.Write(GetSnapshotUseFraction());

            MachineSnapshot.WriteRecipes(this, w);
            MachineSnapshot.WriteItems(InputSlots, w);
            MachineSnapshot.WriteItems(OutputSlots, w);
            MachineSnapshot.WriteLiquids(InputLiquids, w);
            MachineSnapshot.WriteLiquids(OutputLiquids, w);

            w.Write7BitEncodedInt(Fuels.Length);
            foreach (FuelModule fuel in Fuels)
            {
                w.Write7BitEncodedInt(fuel.Remaining);
                w.Write7BitEncodedInt(fuel.Capacity);
            }

            WriteSnapshotExtra(w);
            w.Flush();
            return stream.ToArray();
        }

        void ApplySnapshot(BinaryReader r)
        {
            int version = r.ReadByte();
            if (version != 1)
                return;   // a format this build does not know: keep the defaults

            byte flags = r.ReadByte();
            isOn = (flags & 1) != 0;
            isWorking = (flags & 2) != 0;
            WireToggle = (flags & 4) != 0;

            int biome = r.ReadByte();
            if (Enum.IsDefined(typeof(MachineBiome), biome))
                InsideBiome = (MachineBiome)biome;

            WorkProgress = r.Read7BitEncodedInt();
            CraftsFinished = r.Read7BitEncodedInt();
            if (IsElectric) SetSnapshotUseFraction(r.ReadSingle());

            MachineSnapshot.ReadRecipes(this, r);
            MachineSnapshot.ReadItems(InputSlots, r);
            MachineSnapshot.ReadItems(OutputSlots, r);
            MachineSnapshot.ReadLiquids(InputLiquids, r);
            MachineSnapshot.ReadLiquids(OutputLiquids, r);

            int fuelCount = r.Read7BitEncodedInt();
            for (int i = 0; i < fuelCount; i++)
            {
                int remaining = r.Read7BitEncodedInt();
                int capacity = r.Read7BitEncodedInt();
                if (i < Fuels.Length)
                    Fuels[i].Restore(remaining, capacity);
            }

            ReadSnapshotExtra(r);
        }

        /// <summary>Extra state a subclass wants in the snapshot (motor facing, Materializer toggle, arm area). Call base first.</summary>
        protected virtual void WriteSnapshotExtra(BinaryWriter writer) { }

        /// <summary>Reads what WriteSnapshotExtra wrote, in the same order. Call base first.</summary>
        protected virtual void ReadSnapshotExtra(BinaryReader reader) { }

        /// <summary>Test aid for /fsnap (temporary): what a subclass adds to the one-line description.</summary>
        protected virtual string DescribeSnapshotExtra() => "";

        // ---- Light updates (patch 5.2) ----
        // The small, frequently changing part of the machine state. BuildLightState runs on the server (MachineLightSyncSystem), and
        // ApplyLightState on a client replica, in place: it never replaces the entity and never sends anything. Values are QUANTIZED on
        // purpose (progress rounded down to progressStep ticks, tank amounts in 1/32 of capacity, use fraction in 1/255) so the payload
        // only changes when something visible changes; the system compares payload bytes to decide whether to send. Slots are not part of it.
        // The running recipe travels as a content fingerprint (0 = none), the same fingerprint the 5.1 snapshot uses.
        public byte[] BuildLightState(int progressStep, uint recipeFingerprint)
        {
            var stream = new MemoryStream();
            var w = new BinaryWriter(stream);

            byte flags = 0;
            if (isOn) flags |= 1;
            if (isWorking) flags |= 2;
            if (WireToggle) flags |= 4;
            w.Write(flags);

            int step = Math.Max(1, progressStep);
            w.Write7BitEncodedInt(Math.Max(0, WorkProgress) / step * step);
            w.Write7BitEncodedInt(Math.Max(0, CraftsFinished));
            if (IsElectric) w.Write(MachineLightSync.QuantizeUse(GetSnapshotUseFraction()));
            w.Write(recipeFingerprint);

            w.Write7BitEncodedInt(Fuels.Length);
            foreach (FuelModule fuel in Fuels)
            {
                w.Write7BitEncodedInt(fuel.Remaining);
                w.Write7BitEncodedInt(fuel.Capacity);
            }

            MachineLightSync.WriteTanks(InputLiquids, w);
            MachineLightSync.WriteTanks(OutputLiquids, w);
            w.Flush();
            return stream.ToArray();
        }

        public void ApplyLightState(BinaryReader r)
        {
            // Patch 5.3: while this replica gets detail packets (its UI is open) the exact values win over the rounded light values.
            if (LastDetailTick != 0 && Main.GameUpdateCount - LastDetailTick < (uint)MachineDetail.FreshTicks)
                return;

            byte flags = r.ReadByte();
            int progress = r.Read7BitEncodedInt();
            int crafts = r.Read7BitEncodedInt();
            byte use = IsElectric ? r.ReadByte() : (byte)0;
            uint fingerprint = r.ReadUInt32();

            isOn = (flags & 1) != 0;
            isWorking = (flags & 2) != 0;
            WireToggle = (flags & 4) != 0;
            WorkProgress = progress;
            CraftsFinished = crafts;

            // Leave the exact value from the snapshot alone while it still quantizes to what the server sent.
            if (IsElectric && MachineLightSync.QuantizeUse(GetSnapshotUseFraction()) != use)
                SetSnapshotUseFraction(use / 255f);

            CustomRecipe resolved = MachineLightSync.FindRecipe(Recipes, fingerprint);
            if (!ReferenceEquals(SelectedRecipe, resolved))
                SelectedRecipe = resolved;

            int fuelCount = r.Read7BitEncodedInt();
            for (int i = 0; i < fuelCount; i++)
            {
                int remaining = r.Read7BitEncodedInt();
                int capacity = r.Read7BitEncodedInt();
                if (i < Fuels.Length && (Fuels[i].Remaining != remaining || Fuels[i].Capacity != capacity))
                    Fuels[i].Restore(remaining, capacity);
            }

            MachineLightSync.ReadTanks(InputLiquids, r);
            MachineLightSync.ReadTanks(OutputLiquids, r);
        }

        // ---- Detail channel (patch 5.3) ----
        // The full, exact state a player with this machine's UI open needs: flags, exact progress, crafts, use fraction, manual group and
        // running recipe, every input and output slot, exact tank amounts and fuel. Same codecs as the 5.1 snapshot, but without the
        // biome and the subclass extras (arm area, motor facing, Materializer toggle: they have their own requests later), so applying it
        // never touches those. BuildDetail runs on the server (MachineDetailSystem), ApplyDetail on a client replica, in place: it never
        // replaces the entity and never sends anything.
        public uint LastDetailTick;   // client replica: Main.GameUpdateCount when the last detail arrived (0 = never)

        public byte[] BuildDetail()
        {
            var stream = new MemoryStream();
            var w = new BinaryWriter(stream);

            w.Write((byte)1);   // detail format version

            byte flags = 0;
            if (isOn) flags |= 1;
            if (isWorking) flags |= 2;
            if (WireToggle) flags |= 4;
            w.Write(flags);
            w.Write7BitEncodedInt(Math.Max(0, WorkProgress));
            w.Write7BitEncodedInt(Math.Max(0, CraftsFinished));
            if (IsElectric) w.Write(GetSnapshotUseFraction());

            MachineSnapshot.WriteRecipes(this, w);
            MachineSnapshot.WriteItems(InputSlots, w);
            MachineSnapshot.WriteItems(OutputSlots, w);
            MachineSnapshot.WriteLiquids(InputLiquids, w);
            MachineSnapshot.WriteLiquids(OutputLiquids, w);

            w.Write7BitEncodedInt(Fuels.Length);
            foreach (FuelModule fuel in Fuels)
            {
                w.Write7BitEncodedInt(fuel.Remaining);
                w.Write7BitEncodedInt(fuel.Capacity);
            }

            w.Flush();
            return stream.ToArray();
        }

        public void ApplyDetail(BinaryReader r)
        {
            int version = r.ReadByte();
            if (version != 1)
                return;   // a format this build does not know: keep what the replica has

            byte flags = r.ReadByte();
            isOn = (flags & 1) != 0;
            isWorking = (flags & 2) != 0;
            WireToggle = (flags & 4) != 0;
            WorkProgress = r.Read7BitEncodedInt();
            CraftsFinished = r.Read7BitEncodedInt();
            if (IsElectric) SetSnapshotUseFraction(r.ReadSingle());

            MachineSnapshot.ReadRecipes(this, r);
            MachineSnapshot.ReadItems(InputSlots, r);
            MachineSnapshot.ReadItems(OutputSlots, r);
            MachineSnapshot.ReadLiquids(InputLiquids, r);
            MachineSnapshot.ReadLiquids(OutputLiquids, r);

            int fuelCount = r.Read7BitEncodedInt();
            for (int i = 0; i < fuelCount; i++)
            {
                int remaining = r.Read7BitEncodedInt();
                int capacity = r.Read7BitEncodedInt();
                if (i < Fuels.Length)
                    Fuels[i].Restore(remaining, capacity);
            }
        }

        float GetSnapshotUseFraction() => this is IElectricConsumer consumer ? consumer.useFraction : this is IElectricProducer producer ? producer.useFraction : 0f;

        void SetSnapshotUseFraction(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) value = 0f;
            if (this is IElectricConsumer consumer) consumer.useFraction = value;
            else if (this is IElectricProducer producer) producer.useFraction = value;
        }

        // Test aid for /fsnap (temporary, remove in Phase 10): one readable line with everything the snapshot carries, ending in a digest of
        // the snapshot bytes, so a server console line and a client chat line for the same machine can be compared.
        public string DescribeSnapshot()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(GetType().Name).Append(" (").Append(Position.X).Append(", ").Append(Position.Y).Append(") id ").Append(ID);
            sb.Append(' ').Append(MachineWidth).Append('x').Append(MachineHeight).Append(MachineInitialized ? "" : " NO-GEOMETRY");
            sb.Append(" | ").Append(isOn ? "on" : "off").Append(isWorking ? " working" : "").Append(WireToggle ? " WIRE-OFF" : "").Append(" prog=").Append(WorkProgress);
            sb.Append(" crafts=").Append(CraftsFinished).Append(" biome=").Append(InsideBiome);
            if (IsElectric) sb.Append(" use=").Append(GetSnapshotUseFraction().ToString("0.##"));
            sb.Append(" | in[").Append(MachineSnapshot.DescribeItems(InputSlots)).Append("] out[").Append(MachineSnapshot.DescribeItems(OutputSlots));
            sb.Append("] liqIn[").Append(MachineSnapshot.DescribeLiquids(InputLiquids)).Append("] liqOut[").Append(MachineSnapshot.DescribeLiquids(OutputLiquids));
            sb.Append("] fuel[");
            for (int i = 0; i < Fuels.Length; i++)
                sb.Append(i > 0 ? " " : "").Append(i).Append(':').Append(Fuels[i].Remaining).Append('/').Append(Fuels[i].Capacity);
            sb.Append("] | ").Append(MachineSnapshot.DescribeRecipe(this));
            string extra = DescribeSnapshotExtra();
            if (extra.Length > 0) sb.Append(" |").Append(extra);
            sb.Append(" | digest=").Append(MachineSnapshot.Digest(BuildSnapshot()).ToString("X8"));
            sb.Append(" light=").Append(MachineSnapshot.Digest(BuildLightState(ModContent.GetInstance<MachineLightSyncSystem>().ProgressStepTicks, MachineLightSync.FingerprintOrZero(SelectedRecipe))).ToString("X8"));
            return sb.ToString();
        }

        #endregion

        public override bool IsTileValidForEntity(int x, int y)
        {
            Tile tile = Framing.GetTileSafely(x, y);
            return tile.HasTile && tile.TileType == ValidTileType;
        }
        // Patch 4.2 (M1). Runs on the process that placed the tile: the placing client (it asks the server and creates nothing itself),
        // single-player, or the server (Robotic Arm placements). The entity always lives at the top-left corner of the multitile.
        public override int Hook_AfterPlacement(int i, int j, int type, int style, int direction, int alternate)
        {
            Point16 topLeft = TileObjectData.TopLeft(i, j);

            if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient)
            {
                // Same as tModLoader's Generic_Hook_AfterPlacement: send the tile square, then ask the server to place the entity.
                // The server answers with the entity (message 86), so there is no local entity and no id to return.
                TileObjectData tileData = TileObjectData.GetTileData(type, style, alternate);
                NetMessage.SendTileSquare(Main.myPlayer, topLeft.X, topLeft.Y, tileData.Width, tileData.Height);
                NetMessage.SendData(Terraria.ID.MessageID.TileEntityPlacement, number: topLeft.X, number2: topLeft.Y, number3: Type);
                MachinePlacementInfo.SendBiome(topLeft.X, topLeft.Y);   // patch 4.3: the server cannot detect the biome itself
                return -1;
            }

            Tile tile = Framing.GetTileSafely(topLeft.X, topLeft.Y);
            if (!tile.HasTile || tile.TileType != ValidTileType)
                return -1;

            // Single-player and server: creates, registers, runs OnPlaced and (on a server) shares the entity with the clients.
            BaseMachine machine = MachinePlacement.ServerPlace(type, topLeft.X, topLeft.Y);
            if (machine != null)
                return machine.ID;

            // Not in the visual registry (should not happen for a machine tile): plain placement as before.
            int id = Place(topLeft.X, topLeft.Y);
            if (id != -1 && ByID.TryGetValue(id, out TileEntity entity) && entity is BaseMachine fallback)
            {
                fallback.OnPlaced(topLeft.X, topLeft.Y);
                MachinePlacement.Register(fallback);
                MachinePlacement.Share(fallback);
            }
            return id;
        }

        // Patch 4.2: tModLoader calls this on the server when a client asks for a tile entity (message 87). The base version places a
        // new entity every time, so a repeated or late request would create a second entity at the same position. Refuse when a
        // machine is already there, and refuse when the tile is not there (the tile square always arrives first).
        public override void NetPlaceEntityAttempt(int i, int j)
        {
            // TEMPORARY (patch 7b.6, remove in Phase 10): /farmsend placereject in the server console refuses the next request, so the test
            // can see what a refused placement leaves behind (the tile, no entity; right-click or the bootstrap scan creates it later).
            if (MachinePlacement.RejectNextEntityRequest)
            {
                MachinePlacement.RejectNextEntityRequest = false;
                FactorrariaNet.Log("[Machines] entity request for " + GetType().Name + " at (" + i + ", " + j + ") refused (test: /farmsend placereject)");
                return;
            }

            if (ByPosition.TryGetValue(new Point16(i, j), out TileEntity existing) && existing is BaseMachine)
                return;

            if (!IsTileValidForEntity(i, j))
                return;

            base.NetPlaceEntityAttempt(i, j);
        }

        // Patch 4.2: server only, between Place and message 86. The entity exists but is not shared yet.
        public override void OnNetPlace()
        {
            OnPlaced(Position.X, Position.Y);
            MachinePlacement.Register(this);

            FactorrariaNet.Log("[Machines] placed " + GetType().Name + " at (" + Position.X + ", " + Position.Y + "), id " + ID + " (client request)");
        }

        // Server-safe version of IsOnScreen (patch 2.7, A3): true when any active player's screen-sized rectangle (centred on the player)
        // contains the position. It reads the synced player positions, so it works on a dedicated server, where IsOnScreen is always false.
        // Client-only drawing code keeps using IsOnScreen. Use this for logic that asks "is anyone watching".
        public float NearPlayerWidth = 1920f;
        public float NearPlayerHeight = 1080f;

        public bool IsAnyPlayerNear(Vector2 worldPos, float margin = 100f)
        {
            float halfWidth = NearPlayerWidth / 2f + margin;
            float halfHeight = NearPlayerHeight / 2f + margin;

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];
                if (player == null || !player.active)
                    continue;

                if (MathF.Abs(player.Center.X - worldPos.X) <= halfWidth && MathF.Abs(player.Center.Y - worldPos.Y) <= halfHeight)
                    return true;
            }

            return false;
        }

        public static bool IsOnScreen(Vector2 worldPos, float margin = 100f)
        {
            if (Main.dedServ) return false;

            Rectangle screen = new Rectangle(
                (int)(Main.screenPosition.X - margin),
                (int)(Main.screenPosition.Y - margin),
                (int)(Main.screenWidth + margin * 2f),
                (int)(Main.screenHeight + margin * 2f));

            return screen.Contains(worldPos.ToPoint());
        }

        public static MachineBiome DetermineBiomeAt(int i, int j)
        {
            // Reads the placing player's active Zone flags — these are already computed from nearby
            // tile counts each frame, so they correctly describe the biome at the placement point.
            // Priority order matters: special biomes (Shimmer, Dungeon) are checked before generic
            // height zones so that e.g. a Dungeon underground returns Dungeon, not Cavern.
            // On a dedicated server (no LocalPlayer) we leave InsideBiome as Unknown.
            
            if (Main.netMode == Terraria.ID.NetmodeID.Server || Main.LocalPlayer == null || !Main.LocalPlayer.active)
                return MachineBiome.Unknown;

            Player player = Main.LocalPlayer;

            if (player.ZoneShimmer)           return MachineBiome.Aether;
            if (player.ZoneMeteor)            return MachineBiome.Meteor;
            if (player.ZoneDungeon)           return MachineBiome.Dungeon;
            if (player.ZoneLihzhardTemple)    return MachineBiome.Temple;
            if (player.ZoneGlowshroom)        return MachineBiome.GlowingMushroom;
            if (player.ZoneCorrupt)           return MachineBiome.Corruption;
            if (player.ZoneCrimson)           return MachineBiome.Crimson;
            if (player.ZoneHallow)            return MachineBiome.Hallow;
            if (player.ZoneJungle)            return MachineBiome.Jungle;
            if (player.ZoneSnow)              return MachineBiome.Snow;
            if (player.ZoneUndergroundDesert) return MachineBiome.UndergroundDesert;
            if (player.ZoneDesert)            return MachineBiome.Desert;
            if (player.ZoneGraveyard)         return MachineBiome.Graveyard;
            if (player.ZoneBeach)             return MachineBiome.Ocean;
            if (player.ZoneUnderworldHeight)  return MachineBiome.Underworld;
            if (player.ZoneRockLayerHeight)   return MachineBiome.Cavern;
            if (player.ZoneDirtLayerHeight)   return MachineBiome.Underground;
            if (player.ZoneSkyHeight)         return MachineBiome.Sky;
            if (player.ZoneOverworldHeight)   return MachineBiome.Forest;

            return MachineBiome.Unknown;
        }

    }
}
