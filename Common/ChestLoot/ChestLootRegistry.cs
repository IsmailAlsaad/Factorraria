using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace Factorraria.Common.ChestLoot
{
    /// <summary>Vanilla TileID.Containers styles. Run with LogFoundChests = true to discover others.</summary>
    public enum VanillaChest
    {
        Wood = 0,
        Gold = 1,
        GoldLocked = 2,
        Shadow = 3,
        ShadowLocked = 4,
        Ivy = 10,
        Frozen = 11,
        LivingWood = 12,
        Skyware = 13,
        Lihzahrd = 16,
        Water = 17,
    }

    /// <summary>One candidate item for OneOf(...). Max &lt; Min (default -1) means "exactly Min".</summary>
    public readonly struct LootItem
    {
        public readonly int Type;
        public readonly int Min;
        public readonly int Max;
        public readonly float Weight;

        public LootItem(int type, int min = 1, int max = -1, float weight = 1f)
        {
            Type = type;
            Min = Math.Max(1, min);
            Max = max < Min ? Min : max;
            Weight = weight;
        }
    }

    /// <summary>Ready-made Where(...) predicates. Combine your own with lambdas: c => c.y > 300.</summary>
    public static class ChestConditions
    {
        public static readonly Func<Chest, bool> Surface = c => c.y < Main.worldSurface;
        public static readonly Func<Chest, bool> Underground = c => c.y >= Main.worldSurface && c.y < Main.rockLayer;
        public static readonly Func<Chest, bool> Cavern = c => c.y >= Main.rockLayer && c.y < Main.UnderworldLayer;
        public static readonly Func<Chest, bool> Underworld = c => c.y >= Main.UnderworldLayer;
        public static readonly Func<Chest, bool> LeftHalf = c => c.x < Main.maxTilesX / 2;
        public static readonly Func<Chest, bool> RightHalf = c => c.x >= Main.maxTilesX / 2;
    }

    /// <summary>
    /// A set of loot steps applied, in the order written, to every chest the rule matches.
    /// Build with the fluent methods; nothing here touches the world until ChestLootRegistry.ApplyAll runs.
    /// </summary>
    public class ChestLootRule
    {
        private readonly Func<int, int, bool> match;
        private readonly List<Action<Chest, UnifiedRandom>> steps = new List<Action<Chest, UnifiedRandom>>();
        private Func<Chest, bool> condition;

        internal ChestLootRule(Func<int, int, bool> match)
        {
            this.match = match;
        }

        // ---------------- filters ----------------

        /// <summary>Only apply to chests passing this predicate (stackable: all conditions must pass).</summary>
        public ChestLootRule Where(Func<Chest, bool> extra)
        {
            Func<Chest, bool> previous = condition;
            condition = previous == null ? extra : (c => previous(c) && extra(c));
            return this;
        }

        // ---------------- steps ----------------

        /// <summary>Empty the chest. Put this first for "replace vanilla loot"; omit it to just add.</summary>
        public ChestLootRule Clear()
        {
            steps.Add((chest, rand) =>
            {
                for (int i = 0; i < Chest.maxItems; i++)
                    chest.item[i].TurnToAir();
            });
            return this;
        }

        /// <summary>Remove every stack of one item type (use to strip a single vanilla drop).</summary>
        public ChestLootRule Remove(int itemType)
        {
            steps.Add((chest, rand) =>
            {
                for (int i = 0; i < Chest.maxItems; i++)
                    if (!chest.item[i].IsAir && chest.item[i].type == itemType)
                        chest.item[i].TurnToAir();
            });
            return this;
        }

        /// <summary>Turn existing stacks of one item into another, keeping the stack size.</summary>
        public ChestLootRule ReplaceItem(int fromType, int toType)
        {
            steps.Add((chest, rand) =>
            {
                for (int i = 0; i < Chest.maxItems; i++)
                {
                    Item slot = chest.item[i];
                    if (slot.IsAir || slot.type != fromType) continue;
                    int stack = slot.stack;
                    slot.SetDefaults(toType);
                    slot.stack = Math.Clamp(stack, 1, Math.Max(1, slot.maxStack));
                }
            });
            return this;
        }

        /// <summary>Always add this item (stack random in [min, max]; max omitted = exactly min).</summary>
        public ChestLootRule Always(int itemType, int min = 1, int max = -1)
        {
            return Chance(1f, itemType, min, max);
        }

        /// <summary>Add this item with the given probability (0..1).</summary>
        public ChestLootRule Chance(float chance, int itemType, int min = 1, int max = -1)
        {
            int lo = Math.Max(1, min);
            int hi = max < lo ? lo : max;
            steps.Add((chest, rand) =>
            {
                if (chance < 1f && rand.NextFloat() >= chance) return;
                ChestLootRegistry.TryAdd(chest, itemType, rand.Next(lo, hi + 1));
            });
            return this;
        }

        /// <summary>Always add exactly one of the options, picked by weight.</summary>
        public ChestLootRule OneOf(params LootItem[] options)
        {
            return OneOf(1f, options);
        }

        /// <summary>With the given probability, add exactly one of the options, picked by weight.</summary>
        public ChestLootRule OneOf(float chance, params LootItem[] options)
        {
            steps.Add((chest, rand) =>
            {
                if (options == null || options.Length == 0) return;
                if (chance < 1f && rand.NextFloat() >= chance) return;

                float total = 0f;
                foreach (LootItem o in options) total += Math.Max(0f, o.Weight);
                if (total <= 0f) return;

                float roll = rand.NextFloat() * total;
                foreach (LootItem o in options)
                {
                    roll -= Math.Max(0f, o.Weight);
                    if (roll < 0f)
                    {
                        ChestLootRegistry.TryAdd(chest, o.Type, rand.Next(o.Min, o.Max + 1));
                        return;
                    }
                }
            });
            return this;
        }

        /// <summary>Escape hatch: run any code on the chest.</summary>
        public ChestLootRule Custom(Action<Chest, UnifiedRandom> action)
        {
            steps.Add(action);
            return this;
        }

        // ---------------- internals ----------------

        internal bool Matches(Chest chest, int tileType, int style)
        {
            if (!match(tileType, style)) return false;
            return condition == null || condition(chest);
        }

        internal void Apply(Chest chest, UnifiedRandom rand)
        {
            foreach (Action<Chest, UnifiedRandom> step in steps)
                step(chest, rand);
        }
    }

    /// <summary>
    /// Mini chest-loot library. Define rules in ChestLootDefinitions.Register(); ChestLootSystem applies them
    /// after world generation. Rules run in registration order, each on top of the previous result.
    /// </summary>
    public static class ChestLootRegistry
    {
        private static readonly List<ChestLootRule> rules = new List<ChestLootRule>();

        /// <summary>When true, ApplyAll logs how many chests of each tile/style it found (use it to look up style numbers).</summary>
        public static bool LogFoundChests = false;

        public static IReadOnlyList<ChestLootRule> Rules => rules;

        // ---------------- rule factories ----------------

        /// <summary>Every chest in the world, vanilla or modded.</summary>
        public static ChestLootRule ForAll() => Add(new ChestLootRule((t, s) => true));

        /// <summary>One vanilla chest style (TileID.Containers).</summary>
        public static ChestLootRule For(VanillaChest style) => For(TileID.Containers, (int)style);

        /// <summary>Several vanilla chest styles at once.</summary>
        public static ChestLootRule For(params VanillaChest[] styles)
        {
            HashSet<int> set = new HashSet<int>();
            foreach (VanillaChest s in styles) set.Add((int)s);
            return Add(new ChestLootRule((t, s) => t == TileID.Containers && set.Contains(s)));
        }

        /// <summary>One exact tile + style. Use for Containers2 styles or modded chests: For(ModContent.TileType&lt;MyChest&gt;(), 0).</summary>
        public static ChestLootRule For(int tileType, int style) =>
            Add(new ChestLootRule((t, s) => t == tileType && s == style));

        /// <summary>Every style of one chest tile.</summary>
        public static ChestLootRule ForTile(int tileType) =>
            Add(new ChestLootRule((t, s) => t == tileType));

        /// <summary>Custom match on (tileType, style).</summary>
        public static ChestLootRule ForMatching(Func<int, int, bool> predicate) =>
            Add(new ChestLootRule(predicate));

        private static ChestLootRule Add(ChestLootRule rule)
        {
            rules.Add(rule);
            return rule;
        }

        public static void Clear() => rules.Clear();

        // ---------------- applying ----------------

        /// <summary>Apply every rule to every chest in the world. Safe to call from worldgen or in-game.</summary>
        public static void ApplyAll(UnifiedRandom rand, Action<string> log = null)
        {
            Dictionary<string, int> found = LogFoundChests ? new Dictionary<string, int>() : null;

            for (int c = 0; c < Main.maxChests; c++)
            {
                Chest chest = Main.chest[c];
                if (chest == null) continue;
                if (!WorldGen.InWorld(chest.x, chest.y, 5)) continue;

                Tile tile = Main.tile[chest.x, chest.y];
                if (!tile.HasTile || !TileID.Sets.BasicChest[tile.TileType]) continue;

                int style = tile.TileFrameX / 36;

                if (found != null)
                {
                    string key = $"tile {tile.TileType} style {style}";
                    found[key] = found.TryGetValue(key, out int n) ? n + 1 : 1;
                }

                foreach (ChestLootRule rule in rules)
                    if (rule.Matches(chest, tile.TileType, style))
                        rule.Apply(chest, rand);
            }

            if (found != null && log != null)
                foreach (KeyValuePair<string, int> kv in found)
                    log($"[ChestLoot] {kv.Value}x {kv.Key}");
        }

        /// <summary>Put an item in the first empty slot. Returns false if the chest is full.</summary>
        public static bool TryAdd(Chest chest, int itemType, int stack)
        {
            for (int i = 0; i < Chest.maxItems; i++)
            {
                if (!chest.item[i].IsAir) continue;
                chest.item[i].SetDefaults(itemType);
                chest.item[i].stack = Math.Clamp(stack, 1, Math.Max(1, chest.item[i].maxStack));
                return true;
            }
            return false;
        }
    }
}