using Factorraria.Common.UI;
using Factorraria.Common.UI.RecipeBookUI;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Factorraria.Common.Systems
{
    /// <summary>
    /// Owns the recipe book: the inventory button, the book UI, and open/close rules. Client only (all entry points are no-ops on a server).
    /// Mirrors MachineUISystem's pattern (own UserInterface, layer inserted after "Vanilla: Inventory").
    ///
    /// The book closes on: left click outside it, the inventory closing (this includes Esc, which vanilla treats as
    /// "close inventory"), world unload, player death, and a machine UI opening. A click on the button toggles.
    /// An outside click only closes the book; the click itself is not swallowed.
    /// </summary>
    public class RecipeBookSystem : ModSystem
    {
        UserInterface bookInterface;
        UserInterface buttonInterface;
        RecipeBookState bookState;
        RecipeBookButtonState buttonState;

        bool open;
        bool buttonVisible;
        uint openedAt;
        bool blockingMouse;       // the mouse buttons were cleared for the inventory layer this frame (restored right after it)
        bool prevLeft, prevLeftRelease, prevRight, prevRightRelease;

        static RecipeBookSystem Instance => ModContent.GetInstance<RecipeBookSystem>();

        public static bool IsOpen => Instance != null && Instance.open;

        public static void Open() => Instance?.OpenInternal(null);
        public static void OpenAt(string catalogKey) => Instance?.OpenInternal(catalogKey);
        /// <summary>Opens the book with "#ItemName" in the search bar: lists the recipes that take this item as an ingredient.</summary>
        public static void OpenIngredientSearch(int itemType) => Instance?.OpenIngredientSearchInternal(itemType);
        public static void Close(bool playSound = true) => Instance?.CloseInternal(playSound);
        public static void Toggle() => Instance?.ToggleInternal();

        public override void Load()
        {
            if (Main.dedServ) return;

            bookInterface = new UserInterface();
            buttonInterface = new UserInterface();

            buttonState = new RecipeBookButtonState();
            buttonState.Activate();   // builds the button
            buttonState.Button.OnPressed = ToggleInternal;
            buttonInterface.SetState(buttonState);
        }

        public override void Unload()
        {
            bookInterface = null;
            buttonInterface = null;
            bookState = null;
            buttonState = null;
            RecipeBookArt.Clear();
            RecipeIcon.Clear();
        }

        public override void OnWorldUnload() => CloseInternal(false);

        void ToggleInternal()
        {
            if (open) CloseInternal(true);
            else OpenInternal(null);
        }

        void OpenInternal(string catalogKey)
        {
            if (Main.dedServ || bookInterface == null) return;

            // The book and a machine UI must never overlap.
            ModContent.GetInstance<MachineUISystem>().CloseUI();
            global::Factorraria.Common.Carts.CartChestUISystem.Close(false);

            Main.playerInventory = true;
            if (!open) SoundEngine.PlaySound(SoundID.MenuOpen);

            open = true;
            openedAt = Main.GameUpdateCount;

            bookState ??= new RecipeBookState();
            bookState.ResetSearch();             // opening (incl. from a parchment) always starts unfiltered
            bookInterface.SetState(null);        // clear first, then set: forces a fresh Rebuild via OnActivate
            bookInterface.SetState(bookState);

            if (catalogKey != null) bookState.Select(catalogKey, true);
        }

        void OpenIngredientSearchInternal(int itemType)
        {
            OpenInternal(null);
            if (bookState == null || !ContentSamples.ItemsByType.TryGetValue(itemType, out Item sample)) return;
            bookState.SetSearch("#" + sample.Name);
        }

        void CloseInternal(bool playSound)
        {
            if (!open) return;
            open = false;
            bookInterface?.SetState(null);
            // R2: whatever the book listed at this moment counts as viewed, even entries that were never scrolled into view.
            if (!Main.dedServ && !Main.gameMenu && Main.LocalPlayer.active) Main.LocalPlayer.GetModPlayer<RecipeBookPlayer>().MarkAllViewed();
            if (playSound && !Main.dedServ) SoundEngine.PlaySound(SoundID.MenuClose);
        }

        public override void UpdateUI(GameTime gameTime)
        {
            if (Main.dedServ || bookInterface == null) return;

            Player player = Main.LocalPlayer;
            bool inventoryUp = !Main.gameMenu && Main.playerInventory && player != null && !player.dead
                && !Main.ingameOptionsWindow && !Main.inFancyUI;

            buttonVisible = inventoryUp;
            if (open && !inventoryUp) CloseInternal(false);

            if (buttonVisible) buttonInterface.Update(gameTime);

            if (!open) return;

            bookInterface.Update(gameTime);

            // Click outside the book (and not on the button): close. Skipped right after opening so the opening click cannot close it.
            bool justPressed = Main.mouseLeft && Main.mouseLeftRelease;
            if (open && justPressed && Main.GameUpdateCount - openedAt > 1)
            {
                bool onButton = buttonVisible && buttonState.Button.MouseInside;
                if (!bookState.MouseInsidePanel && !onButton) CloseInternal(true);
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            if (Main.dedServ || bookInterface == null) return;

            int inventoryIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Inventory"));
            if (inventoryIndex == -1) return;

            var bookLayer = new LegacyGameInterfaceLayer(
                "Factorraria: Recipe Book",
                delegate
                {
                    if (open && bookInterface.CurrentState != null)
                        bookInterface.Draw(Main.spriteBatch, new GameTime());
                    return true;
                },
                InterfaceScaleType.UI);

            var buttonLayer = new LegacyGameInterfaceLayer(
                "Factorraria: Recipe Book Button",
                delegate
                {
                    if (buttonVisible && buttonInterface.CurrentState != null)
                        buttonInterface.Draw(Main.spriteBatch, new GameTime());
                    return true;
                },
                InterfaceScaleType.UI);

            // Insert book first, then button at the same index, so the button ends up before the book.
            layers.Insert(inventoryIndex + 1, bookLayer);
            layers.Insert(inventoryIndex + 1, buttonLayer);

            // While the cursor is inside the open book the vanilla inventory (drawn before the book) must not react to it. The inventory handles
            // its clicks while it draws, so the mouse buttons are cleared just before its layer and restored right after it (the book's own
            // input already ran in UpdateUI). Its hover tooltip and cursor override are wiped too, so nothing under the book shows through.
            var blockLayer = new LegacyGameInterfaceLayer(
                "Factorraria: Recipe Book Mouse Block",
                delegate
                {
                    blockingMouse = open && bookState != null && bookState.MouseInsidePanel;
                    if (blockingMouse)
                    {
                        prevLeft = Main.mouseLeft;
                        prevLeftRelease = Main.mouseLeftRelease;
                        prevRight = Main.mouseRight;
                        prevRightRelease = Main.mouseRightRelease;
                        Main.mouseLeft = false;
                        Main.mouseLeftRelease = false;
                        Main.mouseRight = false;
                        Main.mouseRightRelease = false;
                    }
                    return true;
                },
                InterfaceScaleType.UI);

            var unblockLayer = new LegacyGameInterfaceLayer(
                "Factorraria: Recipe Book Mouse Unblock",
                delegate
                {
                    if (blockingMouse)
                    {
                        Main.mouseLeft = prevLeft;
                        Main.mouseLeftRelease = prevLeftRelease;
                        Main.mouseRight = prevRight;
                        Main.mouseRightRelease = prevRightRelease;
                        Main.HoverItem = new Item();
                        Main.hoverItemName = "";
                        Main.cursorOverride = -1;
                        blockingMouse = false;
                    }
                    return true;
                },
                InterfaceScaleType.UI);

            layers.Insert(inventoryIndex + 1, unblockLayer);
            layers.Insert(inventoryIndex, blockLayer);
        }
    }
}