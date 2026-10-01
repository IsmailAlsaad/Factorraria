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
    public class FuelModule
    {
        readonly FuelTable table;
        public int Remaining { get; private set; }   // fuel units left in the "burner"
        public int Capacity { get; private set; } = 1; // units the current fuel item gave (for the flame bar)

        readonly int slotCount;
        public FuelModule(FuelTable table, int slotCount = 1) { this.table = table; this.slotCount = slotCount; }

        static int FirstSlot => BaseMachine.FuelSlotIndex;

        // Lowest-index fuel slot holding something burnable, or -1.
        int FindBurnableSlot(Item[] slots)
        {
            for (int i = 0; i < slotCount; i++)
            {
                Item s = slots[FirstSlot + i];
                if (!s.IsAir && table.Contains(s.type)) return FirstSlot + i;
            }
            return -1;
        }

        public bool CanAccept(int itemType) => table.Contains(itemType);
        public int StackLimit(int itemType) => table.StackLimit(itemType);

        // Non-consuming check: is there anything to burn, now or in the slot?
        public bool HasFuelAvailable(Item[] slots) => Remaining > 0 || FindBurnableSlot(slots) != -1;

        // Burner empty -> eat one item from the fuel slot. Returns false if there is nothing valid to burn.
        public bool TryEnsureFuel(Item[] slots)
        {
            if (Remaining > 0) return true;

            int idx = FindBurnableSlot(slots);
            if (idx == -1) return false;

            Item slot = slots[idx];
            int units = table.UnitsOf(slot.type);
            Remaining += units;
            Capacity = Math.Max(1, units);

            slot.stack--;
            if (slot.stack <= 0) slots[idx] = new Item();
            return true;
        }

        // Conveyor intake: a slot already holding this fuel with room, else the first empty one, else -1.
        public int FindIntakeSlot(Item[] slots, int itemType)
        {
            int limit = StackLimit(itemType);
            int firstEmpty = -1;
            for (int i = 0; i < slotCount; i++)
            {
                int idx = FirstSlot + i;
                Item s = slots[idx];
                if (s.IsAir) { if (firstEmpty == -1) firstEmpty = idx; }
                else if (s.type == itemType && s.stack < limit) return idx;
            }
            return firstEmpty;
        }

        public void Consume(int units = 1) => Remaining = Math.Max(0, Remaining - units);

        // 0..1 for a flame bar, or -1 when not burning. consumedFraction = part of the current craft already "paid for".
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
}
