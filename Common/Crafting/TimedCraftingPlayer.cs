using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Crafting
{
    /// <summary>
    /// Local player's timed-craft state. One craft at a time: Active recipe + Progress/Duration ticks.
    /// Progress is not gradual: it jumps one chunk (TimedCraftingSystem.hitTicks, 0.5 s) per hammer hit.
    /// Repeats until cancelled: click the product again, switch product, close the inventory, die, or the recipe stops
    /// being available (station out of range, condition lost, ingredients gone - all folded into Main.availableRecipe).
    /// </summary>
    public class TimedCraftingPlayer : ModPlayer
    {
        public Recipe Active { get; private set; }
        public int Progress { get; private set; }
        public int Duration { get; private set; }
        public float Fraction => Duration <= 0 ? 0f : MathHelper.Clamp(Progress / (float)Duration, 0f, 1f);

        public bool Repeat { get; private set; }

        /// <summary>Ticks since the last hammer hit (0..hitTicks). Drives the hammer animation and decides when the next chunk lands.</summary>
        public int HitTimer { get; private set; }

        /// <summary>True until the first hit of a freshly started craft, so the hammer starts raised instead of recoiling from a hit that never happened.</summary>
        public bool FreshStart { get; private set; }

        Recipe pendingSwitch;
        bool pendingSwitchRepeat;

        public void Toggle(Recipe recipe, bool repeat)
        {
            if (ReferenceEquals(Active, recipe)) Cancel();
            else Start(recipe, repeat);
        }

        /// <summary>A click moved the list focus to another product. If a craft is running this becomes cancel + start.</summary>
        public void NotifyFocusClick(Recipe recipe, bool repeat)
        {
            if (Active == null || ReferenceEquals(Active, recipe)) return;
            pendingSwitch = recipe;
            pendingSwitchRepeat = repeat;
        }

        public void Cancel()
        {
            Active = null;
            Progress = 0;
            Duration = 0;
            HitTimer = 0;
            FreshStart = false;
            pendingSwitch = null;
        }

        void Start(Recipe recipe, bool repeat)
        {
            Active = recipe;
            Repeat = repeat;
            Progress = 0;
            Duration = ModContent.GetInstance<TimedCraftingSystem>().GetDurationTicks(recipe);
            HitTimer = 0;
            FreshStart = true;
            SoundEngine.PlaySound(SoundID.MenuTick);
        }

        bool IsFocusedAndAvailable(Recipe recipe)
        {
            if (Main.numAvailableRecipes <= 0 || Main.focusRecipe < 0 || Main.focusRecipe >= Main.numAvailableRecipes) return false;
            return ReferenceEquals(Main.recipe[Main.availableRecipe[Main.focusRecipe]], recipe);
        }

        public override void PostUpdate()
        {
            if (Player.whoAmI != Main.myPlayer || Main.dedServ) return;

            if (pendingSwitch != null)
            {
                Recipe next = pendingSwitch;
                bool nextRepeat = pendingSwitchRepeat;
                Cancel();
                if (Main.playerInventory && IsFocusedAndAvailable(next)) Start(next, nextRepeat);
            }

            if (Active == null) return;

            if (!Main.playerInventory || Player.dead || Main.InGuideCraftMenu || !IsFocusedAndAvailable(Active))
            {
                Cancel();
                return;
            }

            TimedCraftingSystem system = ModContent.GetInstance<TimedCraftingSystem>();
            int hitTicks = Math.Max(1, system.hitTicks);

            HitTimer++;
            if (HitTimer < hitTicks) return;

            // The hammer lands: one chunk of progress, one tink.
            HitTimer = 0;
            FreshStart = false;
            Progress = Math.Min(Progress + hitTicks, Duration);
            SoundEngine.PlaySound(system.tinkSound);
            if (Progress >= Duration) Finish();
        }

        /// <summary>Runs vanilla CraftItem with an empty cursor (consumes ingredients, fires OnCraft/OnCreated, popup, sound), then moves the result into the inventory.</summary>
        void Finish()
        {
            TimedCraftingSystem system = ModContent.GetInstance<TimedCraftingSystem>();
            Recipe recipe = Active;

            Item held = Main.mouseItem;
            Main.mouseItem = new Item();
            system.deliveringCraft = true;
            try
            {
                Main.CraftItem(recipe);
            }
            finally
            {
                system.deliveringCraft = false;
            }
            Item made = Main.mouseItem;
            Main.mouseItem = held;

            if (!made.IsAir)
            {
                Item rest = Player.GetItem(Player.whoAmI, made, new GetItemSettings(LongText: false, NoText: true));
                if (!rest.IsAir) Player.QuickSpawnItem(Player.GetSource_Misc("Factorraria/TimedCrafting"), rest, rest.stack);
            }

            system.PlayCompleteSound();

            Recipe.FindRecipes();
            if (Repeat)
            {
                Progress = 0;
                HitTimer = 0;
                FreshStart = false;
            }
            else Cancel();
        }
    }
}