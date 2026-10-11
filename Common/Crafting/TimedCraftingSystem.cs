using Factorraria.Common.UI.CustomUIElements;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Factorraria.Common.Crafting
{
    /// <summary>
    /// Makes crafting from the inventory list take time. Three hooks on vanilla:
    ///   Main.CraftItem                  - a fresh click on the focused product toggles the timed craft instead of crafting instantly.
    ///   Main.HoverOverCraftingItemButton - a click that moves focus to another product while crafting = "switch": cancel + start the new one.
    ///   ItemSlot.Draw (list context)    - the crafting product slot is drawn with the machine output-slot animation.
    /// The per-player state and tick logic live in TimedCraftingPlayer.
    /// </summary>
    public class TimedCraftingSystem : ModSystem
    {
        // Tweakables (instance fields on purpose).
        public float baseTicks = 30f;
        public float ticksPerUnit = 6f;
        public float countBonus = 0.15f;                 // extra time per distinct ingredient beyond the first
        public int minTicks = 6;                         // 0.1 s
        public int maxTicks = 1200;                      // 20 s
        public float grayMultiplier = 0.5f;              // rarity -1
        public float[] rarityMultipliers = { 1.25f, 1.5f, 2f, 2.5f, 3f, 3.5f, 4f, 5f, 6f, 8f }; // rarity 1..10, above 10 uses the last
        public int craftingListContext = 22;             // ItemSlot context vanilla uses for the recipe list products

        /// <summary>True while the player's own finish step runs vanilla CraftItem, so our hook lets it through.</summary>
        public bool deliveringCraft;

        OutputSlotAnimation slotAnimation;

        public delegate void DrawSlotOrig(SpriteBatch sb, ref Item inv, int context, Vector2 position, Color lightColor);
        public delegate void DrawSlotHook(DrawSlotOrig orig, SpriteBatch sb, ref Item inv, int context, Vector2 position, Color lightColor);

        public override void Load()
        {
            if (Main.dedServ) return;

            slotAnimation = new OutputSlotAnimation(() => null, 0);

            AddHook(typeof(Main).GetMethod("CraftItem", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Recipe) }, null),
                new Action<Action<Recipe>, Recipe>(CraftItemHook), "Main.CraftItem");

            AddHook(typeof(Main).GetMethod("HoverOverCraftingItemButton", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(int) }, null),
                new Action<Action<int>, int>(HoverHook), "Main.HoverOverCraftingItemButton");

            AddHook(typeof(ItemSlot).GetMethod("Draw", BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(SpriteBatch), typeof(Item).MakeByRefType(), typeof(int), typeof(Vector2), typeof(Color) }, null),
                new DrawSlotHook(DrawSlotHooked), "ItemSlot.Draw(ref Item)");
        }

        void AddHook(MethodInfo target, Delegate hook, string label)
        {
            if (target == null)
            {
                Mod.Logger.Warn($"TimedCrafting: could not find {label}; that part of timed crafting is disabled.");
                return;
            }
            MonoModHooks.Add(target, hook);
        }

        // ---------------------------------------------------------------- duration

        public float RarityMultiplier(int rare)
        {
            if (rare == ItemRarityID.Gray) return grayMultiplier;
            if (rare <= ItemRarityID.White) return 1f;                      // white, quest, expert, master
            int i = Math.Min(rare, rarityMultipliers.Length) - 1;
            return rarityMultipliers[i];
        }

        /// <summary>unitSum = sum(stack * rarityMult); ticks = base + unitSum * perUnit * (1 + bonus * (distinct - 1)), clamped.</summary>
        public int GetDurationTicks(Recipe recipe)
        {
            float units = 0f;
            int distinct = 0;
            foreach (Item ingredient in recipe.requiredItem)
            {
                if (ingredient == null || ingredient.IsAir) continue;
                distinct++;
                units += ingredient.stack * RarityMultiplier(ingredient.rare);
            }

            float countMult = 1f + countBonus * Math.Max(0, distinct - 1);
            float ticks = baseTicks + units * ticksPerUnit * countMult;
            return (int)MathHelper.Clamp(MathF.Round(ticks), minTicks, maxTicks);
        }

        // ---------------------------------------------------------------- hooks

        void CraftItemHook(Action<Recipe> orig, Recipe r)
        {
            if (deliveringCraft || Main.InGuideCraftMenu || Main.LocalPlayer == null)
            {
                orig(r);
                return;
            }

            // Vanilla re-fires CraftItem every few ticks while the button is held; only a fresh press toggles.
            bool freshClick = (Main.mouseLeft && Main.mouseLeftRelease) || (Main.mouseRight && Main.mouseRightRelease);
            if (!freshClick) return;

            bool repeat = Main.mouseLeft && Main.mouseLeftRelease;   // left = repeat, right = single craft
            Main.LocalPlayer.GetModPlayer<TimedCraftingPlayer>().Toggle(r, repeat);
        }

        void HoverHook(Action<int> orig, int recipeIndex)
        {
            int focusBefore = Main.focusRecipe;
            orig(recipeIndex);

            if (Main.focusRecipe == focusBefore || Main.LocalPlayer == null) return;
            if (Main.focusRecipe < 0 || Main.focusRecipe >= Main.numAvailableRecipes) return;

            Main.LocalPlayer.GetModPlayer<TimedCraftingPlayer>().NotifyFocusClick(Main.recipe[Main.availableRecipe[Main.focusRecipe]], Main.mouseLeft);
        }

        void DrawSlotHooked(DrawSlotOrig orig, SpriteBatch sb, ref Item inv, int context, Vector2 position, Color lightColor)
        {
            if (context == craftingListContext && !Main.gameMenu && Main.LocalPlayer != null && slotAnimation != null)
            {
                TimedCraftingPlayer crafter = Main.LocalPlayer.GetModPlayer<TimedCraftingPlayer>();
                // Vanilla draws the list with the recipe's own createItem object, so reference equality finds the active product
                // in both list modes without needing to know the slot position.
                if (crafter.Active != null && ReferenceEquals(inv, crafter.Active.createItem))
                {
                    ItemSlot.DrawGoldBGForCraftingMaterial = false;
                    slotAnimation.DrawProgress(sb, position, Main.inventoryScale, inv.type, inv.stack, crafter.Fraction);
                    return;
                }
            }

            orig(sb, ref inv, context, position, lightColor);
        }
    }
}