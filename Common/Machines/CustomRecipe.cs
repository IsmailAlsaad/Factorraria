using Factorraria.Common.Liquids;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.ID;

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
    public readonly struct LiquidIngredient
    {
        public readonly int LiquidType;   // id from LiquidTypeRegistry
        public readonly float Amount;
        public LiquidIngredient(int liquidType, float amount) { LiquidType = liquidType; Amount = amount; }
    }

    // What a browser group is "about": one item type OR one liquid type.
    public readonly struct RecipeOutputKey : IEquatable<RecipeOutputKey>
    {
        public readonly bool IsLiquid;
        public readonly int Id;      // item type, or LiquidTypeRegistry id

        RecipeOutputKey(bool isLiquid, int id) { IsLiquid = isLiquid; Id = id; }
        public static RecipeOutputKey ForItem(int itemType) => new(false, itemType);
        public static RecipeOutputKey ForLiquid(int liquidType) => new(true, liquidType);

        public string DisplayName => IsLiquid
            ? LiquidTypeRegistry.Get(Id).Name
            : ContentSamples.ItemsByType[Id].Name;

        public bool Equals(RecipeOutputKey o) => IsLiquid == o.IsLiquid && Id == o.Id;
        public override bool Equals(object obj) => obj is RecipeOutputKey o && Equals(o);
        public override int GetHashCode() => HashCode.Combine(IsLiquid, Id);
    }

    /// <summary>All CustomRecipes of one machine that produce the same output item type.</summary>
    public class RecipeOutputGroup
    {
        public RecipeOutputKey Key { get; }
        public List<CustomRecipe> Recipes { get; } = new List<CustomRecipe>();

        /// <summary>Most ingredient slots any recipe in this group needs.</summary>
        public int MaxInputCount { get; private set; } = 0;

        public RecipeOutputGroup(RecipeOutputKey key)
        {
            Key = key;
        }

        public void Add(CustomRecipe recipe)
        {
            Recipes.Add(recipe);
            MaxInputCount = Math.Max(MaxInputCount, recipe.Inputs.Count);
        }

        /// <summary>Groups recipes by output type, keeping first-appearance order.</summary>
        public static List<RecipeOutputGroup> Build(IEnumerable<CustomRecipe> recipes)
        {
            var groups = new List<RecipeOutputGroup>();
            var byOutput = new Dictionary<RecipeOutputKey, RecipeOutputGroup>();

            foreach (CustomRecipe recipe in recipes)
            {
                if (!recipe.TryGetPrimaryOutput(out RecipeOutputKey key)) continue;   // nothing to show at all

                if (!byOutput.TryGetValue(key, out RecipeOutputGroup group))
                {
                    group = new RecipeOutputGroup(key);
                    byOutput[key] = group;
                    groups.Add(group);
                }
                group.Add(recipe);
            }

            return groups;
        }
    }
    public class CustomRecipe
    {
        public List<RecipeIngredient> Inputs { get; set; } = new();
        public List<RecipeIngredient> Outputs { get; } = new();
        public List<LiquidIngredient> LiquidInputs { get; } = new();
        public List<LiquidIngredient> LiquidOutputs { get; } = new();
        public int? DurationTicks { get; set; }          // null = the machine's WorkDuration

        // Old API kept so RecipeOutputGroup / RecipeBrowserPanel still compile.
        public RecipeIngredient Output => Outputs.Count > 0 ? Outputs[0] : default;

        public CustomRecipe() { }
        public CustomRecipe(List<RecipeIngredient> inputs, RecipeIngredient output) { Inputs = inputs; Outputs.Add(output); }
        public CustomRecipe(List<Item> inputs, Item output)
        {
            Inputs = inputs.Select(i => new RecipeIngredient(i.type, i.stack)).ToList();
            Outputs.Add(new RecipeIngredient(output.type, output.stack));
        }

        // Readable builder API for new recipes
        public CustomRecipe WithInput(int type, int stack = 1) { Inputs.Add(new RecipeIngredient(type, stack)); return this; }
        public CustomRecipe WithOutput(int type, int stack = 1) { Outputs.Add(new RecipeIngredient(type, stack)); return this; }
        public CustomRecipe WithLiquidInput(int liquidType, float amt) { LiquidInputs.Add(new LiquidIngredient(liquidType, amt)); return this; }
        public CustomRecipe WithLiquidOutput(int liquidType, float amt) { LiquidOutputs.Add(new LiquidIngredient(liquidType, amt)); return this; }
        public CustomRecipe TakesTicks(int ticks) { DurationTicks = ticks; return this; }

        public static bool TryGetRecipeFromList(List<CustomRecipe> recipeList, List<Item> itemSlots,out CustomRecipe outputRecipe, LiquidStack[] liquidSlots = null)
        {
            outputRecipe = null;

            int activeItemCount = 0;
            foreach (Item s in itemSlots)
                if (s != null && !s.IsAir && s.stack > 0) activeItemCount++;

            foreach (CustomRecipe recipe in recipeList)
            {
                if (recipe.IsSatisfiedBy(itemSlots, activeItemCount, liquidSlots))
                {
                    outputRecipe = recipe;
                    return true;
                }
            }
            return false;
        }

        bool IsSatisfiedBy(List<Item> itemSlots, int activeItemCount, LiquidStack[] liquidSlots)
        {
            if (Inputs.Count == 0 && LiquidInputs.Count == 0) return false;  // would match an empty machine
            if (Inputs.Count != activeItemCount) return false;               // same exact-count rule as before

            foreach (RecipeIngredient req in Inputs)
            {
                int total = 0;
                foreach (Item s in itemSlots)
                    if (s != null && !s.IsAir && s.type == req.Type) total += s.stack;
                if (total < req.Stack) return false;
            }

            foreach (LiquidIngredient need in LiquidInputs)
            {
                bool found = false;
                if (liquidSlots != null)
                    foreach (LiquidStack tank in liquidSlots)
                        if (!tank.IsEmpty && tank.LiquidType == need.LiquidType && tank.Amount >= need.Amount) { found = true; break; }
                if (!found) return false;
            }
            return true;
        }

        // What the browser files this recipe under: first item output, else first liquid output.
        public bool TryGetPrimaryOutput(out RecipeOutputKey key)
        {
            if (Outputs.Count > 0) { key = RecipeOutputKey.ForItem(Outputs[0].Type); return true; }
            if (LiquidOutputs.Count > 0) { key = RecipeOutputKey.ForLiquid(LiquidOutputs[0].LiquidType); return true; }
            key = default;
            return false;
        }
    }

    public class FuelTable
    {
        readonly Dictionary<int, int> unitsByItem = new();

        // How many of this fuel the intake will buffer, given its unit value. Default 10.
        public Func<int, int> StackLimitRule = units => Math.Max(10 - units, 1);

        public FuelTable Add(int itemType, int fuelUnits) { unitsByItem[itemType] = fuelUnits; return this; }
        public bool Contains(int itemType) => unitsByItem.ContainsKey(itemType);
        public int UnitsOf(int itemType) => unitsByItem[itemType];
        public int StackLimit(int itemType) => StackLimitRule(UnitsOf(itemType));
        public void Clear() => unitsByItem.Clear();
    }

    // One fuel the machine needs: its own slot, its own item->units table, its own burn rate.
    public class FuelChannel
    {
        public readonly FuelTable Table;
        public readonly int UnitsPerCraft;   // fuel units this channel burns per finished craft

        public FuelChannel(FuelTable table, int unitsPerCraft = 1)
        {
            Table = table;
            UnitsPerCraft = unitsPerCraft;
        }
    }

    public class FuelModule
    {
        readonly FuelTable table;
        public int SlotIndex { get; }        // the input slot that feeds this burner
        public int UnitsPerCraft { get; }    // units one finished craft burns
        public int Remaining { get; private set; }
        public int Capacity { get; private set; } = 1;

        public FuelModule(FuelTable table, int slotIndex = BaseMachine.FuelSlotIndex, int unitsPerCraft = 1)
        {
            this.table = table;
            SlotIndex = slotIndex;
            UnitsPerCraft = unitsPerCraft;
        }

        public bool CanAccept(int itemType) => table.Contains(itemType);
        public int StackLimit(int itemType) => table.StackLimit(itemType);

        public bool HasFuelAvailable(Item[] inputSlots)
        {
            if (Remaining > 0) return true;
            Item slot = inputSlots[SlotIndex];
            return !slot.IsAir && table.Contains(slot.type);
        }

        public bool TryEnsureFuel(Item[] inputSlots)
        {
            if (Remaining > 0) return true;

            Item slot = inputSlots[SlotIndex];
            if (slot.IsAir || !table.Contains(slot.type)) return false;

            int units = table.UnitsOf(slot.type);
            Remaining += units;
            Capacity = Math.Max(1, units);

            slot.stack--;
            if (slot.stack <= 0) inputSlots[SlotIndex] = new Item();
            return true;
        }

        public void ConsumeCraft() => Consume(UnitsPerCraft);
        public void Consume(int units = 1) => Remaining = Math.Max(0, Remaining - units);

        public float GetBurnFraction(float consumedFraction)
        {
            if (Remaining <= 0) return -1f;
            return Math.Clamp((Remaining - consumedFraction) / Capacity, 0f, 1f);
        }

        public void Restore(int remaining, int capacity)
        {
            Remaining = Math.Max(0, remaining);
            Capacity = Math.Max(1, capacity > 0 ? capacity : remaining);
        }
    }

    // Liquid counterpart of FuelTable: which liquids can be burned, and how much of each makes ONE fuel unit.
    // Lower amountPerFuelUnit = better fuel (same idea as coal being worth more units than gel).
    public class LiquidFuelTable
    {
        readonly Dictionary<int, float> amountPerUnitByLiquid = new();

        public LiquidFuelTable Add(int liquidType, float amountPerFuelUnit)
        {
            if (amountPerFuelUnit <= 0f)
                throw new ArgumentOutOfRangeException(nameof(amountPerFuelUnit), "One fuel unit must cost a positive amount of liquid.");

            amountPerUnitByLiquid[liquidType] = amountPerFuelUnit;
            return this;
        }

        public bool Contains(int liquidType) => amountPerUnitByLiquid.ContainsKey(liquidType);
        public float AmountPerUnit(int liquidType) => amountPerUnitByLiquid[liquidType];
        public void Clear() => amountPerUnitByLiquid.Clear();
    }

    // One liquid fuel the machine needs: its own input tank, its own liquid->units table, its own burn rate.
    public class LiquidFuelChannel
    {
        public readonly LiquidFuelTable Table;
        public readonly int UnitsPerCraft;   // fuel units this channel burns per finished craft

        public LiquidFuelChannel(LiquidFuelTable table, int unitsPerCraft = 1)
        {
            Table = table;
            UnitsPerCraft = unitsPerCraft;
        }
    }

    // Burns one channel's fuel. Unlike items, liquid is continuous, so there is no "burn buffer":
    // the input tank IS the buffer, and a craft takes (units * amountPerUnit) straight out of it.
    // Keep AmountPerCraft <= tank Capacity (1000 by default) or the machine can never start a craft.
    public class LiquidFuelModule
    {
        readonly LiquidFuelTable table;

        public int TankIndex { get; }        // index into BaseMachine.InputLiquids (fuel tanks come first)
        public int UnitsPerCraft { get; }    // units one finished craft burns

        public LiquidFuelModule(LiquidFuelTable table, int tankIndex, int unitsPerCraft = 1)
        {
            this.table = table;
            TankIndex = tankIndex;
            UnitsPerCraft = unitsPerCraft;
        }

        // Also used as the tank's pipe filter, so pipes can only put burnable liquids in.
        public bool CanAccept(int liquidType) => table.Contains(liquidType);

        // How much of this liquid one craft burns.
        public float AmountPerCraft(int liquidType) => table.AmountPerUnit(liquidType) * UnitsPerCraft;

        public bool HasFuelAvailable(LiquidStack[] inputTanks)
        {
            LiquidStack tank = inputTanks[TankIndex];
            if (tank.IsEmpty || !table.Contains(tank.LiquidType)) return false;
            return tank.Amount >= AmountPerCraft(tank.LiquidType);
        }

        public void ConsumeCraft(LiquidStack[] inputTanks)
        {
            LiquidStack tank = inputTanks[TankIndex];
            if (tank.IsEmpty || !table.Contains(tank.LiquidType)) return;

            tank.Amount -= Math.Min(tank.Amount, AmountPerCraft(tank.LiquidType));
            if (tank.Amount <= 0f)
            {
                tank.Amount = 0f;
                tank.LiquidType = -1;   // drained: the tank may take any burnable liquid again
            }
        }
    }

}
