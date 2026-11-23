using Factorraria.Common.UI.CustomUIElements;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Factorraria.Common.Crafting
{
    /// <summary>
    /// Makes crafting from the inventory list take time. Three hooks on vanilla:
    ///   Main.CraftItem                  - a fresh click on the focused product toggles the timed craft instead of crafting instantly.
    ///   Main.HoverOverCraftingItemButton - a click that moves focus to another product while crafting = "switch": cancel only (the next click on the new product starts it).
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

        // Hammer hits: a craft is split into chunks of hitTicks; every hammer hit lands one chunk of progress.
        public int hitTicks = 30;                        // 0.5 s per hit; every craft time is rounded to a multiple of this

        // Hammer sprite. Pivot = bottom-left corner of the texture moved toward the top-right by hammerPivotNudge pixels.
        // Angles are radians relative to the texture as drawn; positive = clockwise on screen.
        public string hammerTexturePath = "Factorraria/Common/Crafting/Crafting_Hammer";
        public Vector2 hammerPivotNudge = new Vector2(4f, 4f);   // texture pixels (right, up)
        public Vector2 hammerAnchor = Vector2.Zero;              // where the pivot sits on the slot, from its top-left corner (unscaled pixels)
        public float hammerScale = 1f;                           // on top of the slot scale
        public float hammerRaisedAngle = 0f;                     // pose while winding up (the texture as drawn)
        public float hammerHitAngle = 1.1f;                      // pose at the moment of impact
        public float hammerWindupExtra = 0.25f;                  // how far past the raised pose the hammer strains back before the swing (radians)
        public float hammerHangCreep = 0.08f;                    // extra slow drift back while hanging at the top (radians)
        public float hammerHitStop = 0.1f;                       // cycle fraction the hammer stays frozen on the slot after impact
        public float hammerLiftEnd = 0.6f;                       // cycle fraction where the slow lift reaches the top
        public float hammerSwingStart = 0.78f;                   // cycle fraction where the fast downswing starts (between = hang at the top)
        public float hammerSquash = 0.3f;                        // squash on impact (0.3 = 30 % flatter along the axis below)
        public float hammerSquashEnd = 0.3f;                     // cycle fraction where the squash has fully recovered
        public float hammerStretch = 0.2f;                       // stretch reached at the end of the downswing
        public Vector2 hammerSquashAxis = new Vector2(-0.5f, 1f);        // scale change per unit of squash/stretch (x, y in texture space); the hammer's long side should be y
        public Vector2 hammerStrikePoint = new Vector2(0.85f, 0.15f);    // where the hammer face is, as a fraction of the texture (from its top-left); sparks spawn here at impact

        // Iron sparks (UI space, drawn in their own interface layer so they outlive the craft's last hit).
        public int sparkCountMin = 5;
        public int sparkCountMax = 8;
        public float sparkAngleMin = -2.4f;                      // radians, screen space: -pi/2 = straight up, -0.3 = mostly right
        public float sparkAngleMax = -0.3f;
        public float sparkSpeedMin = 1.5f;                       // px per tick
        public float sparkSpeedMax = 4f;
        public int sparkLifeMin = 14;                            // ticks
        public int sparkLifeMax = 26;
        public float sparkGravity = 0.28f;
        public float sparkDrag = 0.96f;
        public float sparkStreak = 0.5f;                         // how much faster sparks stretch into streaks
        public Color[] sparkColors = { new Color(205, 208, 218), new Color(205, 208, 218), new Color(150, 154, 166), new Color(255, 196, 96) };

        public SoundStyle tinkSound = SoundID.Tink with { Volume = 0.6f, Pitch = -0.2f, PitchVariance = 0.1f };
        public string completeSoundPath = "Factorraria/Assets/Sounds/CraftingComplete";
        public float completeSoundVolume = 0.15f;

        /// <summary>True while the player's own finish step runs vanilla CraftItem, so our hook lets it through.</summary>
        public bool deliveringCraft;

        OutputSlotAnimation slotAnimation;
        Asset<Texture2D> hammerAsset;
        bool hasCompleteSound;

        class CraftSpark
        {
            public Vector2 pos;
            public Vector2 vel;
            public int life;
            public int maxLife;
            public float size;
            public Color color;
        }

        readonly List<CraftSpark> sparks = new List<CraftSpark>();
        Vector2 lastSlotPos;                                     // where the active product slot was last drawn (UI space)
        float lastSlotScale = 1f;
        bool hasSlot;

        public delegate void DrawSlotOrig(SpriteBatch sb, ref Item inv, int context, Vector2 position, Color lightColor);
        public delegate void DrawSlotHook(DrawSlotOrig orig, SpriteBatch sb, ref Item inv, int context, Vector2 position, Color lightColor);

        public override void Load()
        {
            if (Main.dedServ) return;

            slotAnimation = new OutputSlotAnimation(() => null, 0);

            // RequestIfExists so a missing PNG / sound file only skips that part instead of crashing.
            ModContent.RequestIfExists<Texture2D>(hammerTexturePath, out hammerAsset);
            hasCompleteSound = ModContent.RequestIfExists<SoundEffect>(completeSoundPath, out _);

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

        /// <summary>unitSum = sum(stack * rarityMult); ticks = base + unitSum * perUnit * (1 + bonus * (distinct - 1)), clamped,
        /// then rounded to the closest multiple of hitTicks (at least one hit).</summary>
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
            int clamped = (int)MathHelper.Clamp(MathF.Round(ticks), minTicks, maxTicks);
            int step = Math.Max(1, hitTicks);
            int hits = Math.Max(1, (int)MathF.Round(clamped / (float)step, MidpointRounding.AwayFromZero));
            return hits * step;
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

            bool repeat = !(Main.mouseLeft && Main.mouseLeftRelease);   // left = single craft, right = repeat (left wins if both are fresh)
            Main.LocalPlayer.GetModPlayer<TimedCraftingPlayer>().Toggle(r, repeat);
        }

        void HoverHook(Action<int> orig, int recipeIndex)
        {
            int focusBefore = Main.focusRecipe;
            orig(recipeIndex);

            if (Main.focusRecipe == focusBefore || Main.LocalPlayer == null) return;
            if (Main.focusRecipe < 0 || Main.focusRecipe >= Main.numAvailableRecipes) return;

            Main.LocalPlayer.GetModPlayer<TimedCraftingPlayer>().NotifyFocusClick(Main.recipe[Main.availableRecipe[Main.focusRecipe]]);
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
                    lastSlotPos = position;
                    lastSlotScale = Main.inventoryScale;
                    hasSlot = true;
                    DrawHammer(sb, position, Main.inventoryScale, crafter);
                    return;
                }
            }

            orig(sb, ref inv, context, position, lightColor);
        }

        // ---------------------------------------------------------------- hammer

        /// <summary>
        /// One hit cycle (hitTicks long) with the impact on its last tick, shaped to feel heavy:
        ///   [0, hitStop]          frozen on the slot at the hit pose (hit-stop), squashed flat
        ///   [hitStop, liftEnd]    slow, strained lift (ease in-out) past the raised pose by hammerWindupExtra
        ///   [liftEnd, swingStart] hang at the top, creeping back a little more
        ///   [swingStart, 1]       fast downswing (cubic ease-in), stretched
        /// A fresh craft skips the hit-stop and starts at the lift, so the first hit still takes the full 0.5 s.
        /// </summary>
        float CyclePhase(TimedCraftingPlayer crafter)
        {
            float p = MathHelper.Clamp(crafter.HitTimer / (float)Math.Max(1, hitTicks), 0f, 1f);
            if (crafter.FreshStart) p = hammerHitStop + (1f - hammerHitStop) * p;
            return p;
        }

        public float HammerAngle(TimedCraftingPlayer crafter)
        {
            float p = CyclePhase(crafter);
            float top = hammerRaisedAngle - hammerWindupExtra;

            if (p < hammerHitStop) return hammerHitAngle;

            if (p < hammerLiftEnd)
            {
                float t = (p - hammerHitStop) / MathF.Max(0.0001f, hammerLiftEnd - hammerHitStop);
                return MathHelper.Lerp(hammerHitAngle, top, MathHelper.SmoothStep(0f, 1f, t));
            }

            if (p < hammerSwingStart)
            {
                float h = (p - hammerLiftEnd) / MathF.Max(0.0001f, hammerSwingStart - hammerLiftEnd);
                return top - hammerHangCreep * h;
            }

            float s = MathHelper.Clamp((p - hammerSwingStart) / MathF.Max(0.0001f, 1f - hammerSwingStart), 0f, 1f);
            return MathHelper.Lerp(top - hammerHangCreep, hammerHitAngle, s * s * s);
        }

        /// <summary>Negative = squashed (right after impact), positive = stretched (end of the downswing), 0 = normal.</summary>
        float HammerSquashStretch(TimedCraftingPlayer crafter)
        {
            float p = CyclePhase(crafter);

            if (p < hammerSquashEnd)
            {
                float t = 1f - p / MathF.Max(0.0001f, hammerSquashEnd);
                return -hammerSquash * t * t;
            }
            if (p >= hammerSwingStart)
            {
                float s = MathHelper.Clamp((p - hammerSwingStart) / MathF.Max(0.0001f, 1f - hammerSwingStart), 0f, 1f);
                return hammerStretch * s * s;
            }
            return 0f;
        }

        void DrawHammer(SpriteBatch sb, Vector2 slotPos, float slotScale, TimedCraftingPlayer crafter)
        {
            if (hammerAsset == null || !hammerAsset.IsLoaded) return;

            Texture2D tex = hammerAsset.Value;
            Vector2 origin = new Vector2(hammerPivotNudge.X, tex.Height - hammerPivotNudge.Y);
            Vector2 pivotPos = slotPos + hammerAnchor * slotScale;
            Vector2 scale = (Vector2.One + HammerSquashStretch(crafter) * hammerSquashAxis) * (slotScale * hammerScale);

            sb.Draw(tex, pivotPos, null, Color.White, HammerAngle(crafter), origin, scale, SpriteEffects.None, 0f);
        }

        // ---------------------------------------------------------------- sparks

        /// <summary>Where the hammer face is at the moment of impact (UI space).</summary>
        Vector2 HammerStrikePosition()
        {
            Vector2 pivot = lastSlotPos + hammerAnchor * lastSlotScale;

            if (hammerAsset != null && hammerAsset.IsLoaded)
            {
                Texture2D tex = hammerAsset.Value;
                Vector2 origin = new Vector2(hammerPivotNudge.X, tex.Height - hammerPivotNudge.Y);
                Vector2 local = (new Vector2(tex.Width * hammerStrikePoint.X, tex.Height * hammerStrikePoint.Y) - origin) * (lastSlotScale * hammerScale);
                return pivot + local.RotatedBy(hammerHitAngle);
            }

            // No hammer art yet: sparks fly from the middle of the slot.
            Texture2D slot = TextureAssets.InventoryBack.Value;
            return lastSlotPos + new Vector2(slot.Width, slot.Height) * 0.5f * lastSlotScale;
        }

        /// <summary>Called by the player on the tick a hammer hit lands.</summary>
        public void SpawnHitSparks()
        {
            if (Main.dedServ || !hasSlot) return;

            Vector2 at = HammerStrikePosition() + new Vector2(0f,8f);
            int count = Main.rand.Next(Math.Min(sparkCountMin, sparkCountMax), Math.Max(sparkCountMin, sparkCountMax) + 1);
            for (int i = 0; i < count; i++)
            {
                int life = Main.rand.Next(Math.Min(sparkLifeMin, sparkLifeMax), Math.Max(sparkLifeMin, sparkLifeMax) + 1);
                float angle = Main.rand.NextFloat(sparkAngleMin, sparkAngleMax);
                float speed = Main.rand.NextFloat(sparkSpeedMin, sparkSpeedMax);

                sparks.Add(new CraftSpark
                {
                    pos = at + Main.rand.NextVector2Circular(2f, 2f),
                    vel = angle.ToRotationVector2() * speed,
                    life = life,
                    maxLife = life,
                    size = Main.rand.NextFloat(1.5f, 2.5f),
                    color = sparkColors[Main.rand.Next(sparkColors.Length)]
                });
            }
        }

        public override void PostUpdateEverything()
        {
            if (Main.dedServ || sparks.Count == 0) return;

            if (!Main.playerInventory)
            {
                sparks.Clear();
                return;
            }

            for (int i = sparks.Count - 1; i >= 0; i--)
            {
                CraftSpark s = sparks[i];
                s.pos += s.vel;
                s.vel.Y += sparkGravity;
                s.vel *= sparkDrag;
                if (--s.life <= 0) sparks.RemoveAt(i);
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int index = layers.FindIndex(layer => layer.Name == "Vanilla: Inventory");
            if (index < 0) return;

            layers.Insert(index + 1, new LegacyGameInterfaceLayer("Factorraria: Crafting Sparks", () =>
            {
                DrawSparks(Main.spriteBatch);
                return true;
            }, InterfaceScaleType.UI));
        }

        void DrawSparks(SpriteBatch sb)
        {
            if (sparks.Count == 0) return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle source = new Rectangle(0, 0, 1, 1);      // MagicPixel is not 1x1, so always draw from a 1x1 corner
            foreach (CraftSpark s in sparks)
            {
                float fade = MathHelper.Clamp(s.life / (float)s.maxLife * 2f, 0f, 1f);
                float length = s.size * (1f + s.vel.Length() * sparkStreak);
                sb.Draw(pixel, s.pos, source, s.color * fade, s.vel.ToRotation(), new Vector2(0.5f, 0.5f), new Vector2(length, s.size), SpriteEffects.None, 0f);
            }
        }

        /// <summary>Custom completion sound if Assets/Sounds/CraftingComplete exists, otherwise nothing extra (vanilla CraftItem still plays its own grab sound).</summary>
        public void PlayCompleteSound()
        {
            if (!hasCompleteSound) return;
            //SoundEngine.PlaySound(new SoundStyle(completeSoundPath) { Volume = completeSoundVolume });
            SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.15f , PitchVariance = 0.15f });
        }
    }
}