using Factorraria.Common.Systems;
using Factorraria.Content.UI;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Factorraria.Common.Carts
{
    /// <summary>
    /// Inventory panel of a chest cart: a grid of item slots plus Loot All and Quick Stack. Custom UI (not a vanilla
    /// Main.chest) because the vanilla chest UI is tied to tile coordinates and closes when the "chest" moves.
    /// </summary>
    public class CartChestUIState : UIState
    {
        public const int Columns = 5;
        public const int Rows = Cart.ChestSlots / Columns;

        private const float SlotSize = 44f;
        private const float Gap = 6f;
        private const float Pad = 12f;
        private const float ButtonHeight = 28f;

        private const float GridWidth = Columns * SlotSize + (Columns - 1) * Gap;
        private const float GridHeight = Rows * SlotSize + (Rows - 1) * Gap;

        public const float PanelWidth = GridWidth + Pad * 2f;
        public const float PanelHeight = GridHeight + Gap + ButtonHeight + Pad * 2f;

        public readonly Cart Cart;
        public UIPanel Panel;

        public CartChestUIState(Cart cart)
        {
            Cart = cart;
        }

        public override void OnInitialize()
        {
            Panel = new UIPanel();
            Panel.Width.Set(PanelWidth, 0f);
            Panel.Height.Set(PanelHeight, 0f);
            Panel.SetPadding(0f);
            Append(Panel);

            for (int i = 0; i < Cart.ChestSlots; i++)
            {
                int index = i;

                UIItemSlotWrapper slot = new UIItemSlotWrapper(
                    ItemSlot.Context.ChestItem,
                    () => Cart.ChestItems != null ? Cart.ChestItems[index] : new Item(),
                    item =>
                    {
                        if (Cart.ChestItems != null)
                        {
                            Cart.ChestItems[index] = item;
                        }
                    });

                slot.Width.Set(SlotSize, 0f);
                slot.Height.Set(SlotSize, 0f);
                slot.Left.Set(Pad + (i % Columns) * (SlotSize + Gap), 0f);
                slot.Top.Set(Pad + (i / Columns) * (SlotSize + Gap), 0f);
                Panel.Append(slot);
            }

            float buttonTop = Pad + GridHeight + Gap;
            float buttonWidth = (GridWidth - Gap) / 2f;

            Panel.Append(MakeButton("Loot All", Pad, buttonTop, buttonWidth, LootAll));
            Panel.Append(MakeButton("Quick Stack", Pad + buttonWidth + Gap, buttonTop, buttonWidth, QuickStack));
        }

        private static UITextPanel<string> MakeButton(string text, float left, float top, float width, Action onClick)
        {
            UITextPanel<string> button = new UITextPanel<string>(text, 0.8f, false);
            button.Width.Set(width, 0f);
            button.Height.Set(ButtonHeight, 0f);
            button.Left.Set(left, 0f);
            button.Top.Set(top, 0f);
            button.OnLeftClick += (evt, element) => onClick();
            button.OnMouseOver += (evt, element) => SoundEngine.PlaySound(SoundID.MenuTick);
            return button;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // Keep clicks on the panel from reaching the world (placing, shoving, using items).
            if (Panel != null && Panel.ContainsPoint(Main.MouseScreen))
            {
                Main.LocalPlayer.mouseInterface = true;
            }
        }

        /// <summary>Moves every cart item into the player's inventory (what fits).</summary>
        private void LootAll()
        {
            if (Cart.ChestItems == null)
            {
                return;
            }

            Player player = Main.LocalPlayer;
            bool any = false;

            for (int i = 0; i < Cart.ChestItems.Length; i++)
            {
                Item item = Cart.ChestItems[i];
                if (item == null || item.IsAir)
                {
                    continue;
                }

                any = true;
                Cart.ChestItems[i] = player.GetItem(player.whoAmI, item, GetItemSettings.LootAllSettings) ?? new Item();
            }

            if (any)
            {
                SoundEngine.PlaySound(SoundID.Grab);
            }
        }

        /// <summary>Like vanilla quick stack: inventory items the cart already holds move into it. Hotbar and favorites stay.</summary>
        private void QuickStack()
        {
            Player player = Main.LocalPlayer;
            bool moved = false;

            for (int i = 10; i < 50; i++)
            {
                Item item = player.inventory[i];
                if (item == null || item.IsAir || item.favorited)
                {
                    continue;
                }

                if (Cart.TryAddToChest(item, true))
                {
                    moved = true;
                }
            }

            if (moved)
            {
                SoundEngine.PlaySound(SoundID.Grab);
            }
        }
    }

    /// <summary>Owns the chest cart panel: opening, closing, following the cart on screen and drawing. Same pattern as MachineUISystem.</summary>
    public class CartChestUISystem : ModSystem
    {
        private const float CloseRange = 10f * 16f; // the panel closes when the player walks this far from the cart
        private const float AboveCart = 70f;        // world pixels between the cart and the bottom of the panel

        private UserInterface chestInterface;
        private CartChestUIState state;

        private static CartChestUISystem Instance
        {
            get { return ModContent.GetInstance<CartChestUISystem>(); }
        }

        public static bool IsOpen
        {
            get { return Instance != null && Instance.state != null; }
        }

        public static Cart OpenCart
        {
            get { return Instance != null && Instance.state != null ? Instance.state.Cart : null; }
        }

        public override void Load()
        {
            if (Main.dedServ)
            {
                return;
            }

            chestInterface = new UserInterface();
        }

        public override void Unload()
        {
            chestInterface = null;
            state = null;
        }

        public override void OnWorldUnload()
        {
            CloseInternal(false);
        }

        public static void Toggle(Cart cart)
        {
            if (Instance != null)
            {
                Instance.ToggleInternal(cart);
            }
        }

        public static void Close(bool playSound = false)
        {
            if (Instance != null)
            {
                Instance.CloseInternal(playSound);
            }
        }

        public static void NotifyCartRemoved(Cart cart)
        {
            if (OpenCart == cart)
            {
                Close(false);
            }
        }

        /// <summary>Shift-click from the player's inventory: sends the item into the open cart. True if anything moved.</summary>
        public static bool TryShiftInsert(Item item)
        {
            Cart cart = OpenCart;
            if (cart == null || !cart.TryAddToChest(item))
            {
                return false;
            }

            SoundEngine.PlaySound(SoundID.Grab);
            return true;
        }

        private void ToggleInternal(Cart cart)
        {
            if (chestInterface == null)
            {
                return;
            }

            if (state != null && state.Cart == cart)
            {
                CloseInternal(true);
                return;
            }

            // Only one inventory-style panel at a time.
            ModContent.GetInstance<MachineUISystem>().CloseUI();
            RecipeBookSystem.Close(false);

            SoundEngine.PlaySound(SoundID.MenuOpen);
            Main.playerInventory = true; // same as vanilla chests: opening one opens the inventory too

            state = new CartChestUIState(cart);
            chestInterface.SetState(null);
            chestInterface.SetState(state);
            Reposition();
        }

        private void CloseInternal(bool playSound)
        {
            if (state == null)
            {
                return;
            }

            if (playSound)
            {
                SoundEngine.PlaySound(SoundID.MenuClose);
            }

            if (chestInterface != null)
            {
                chestInterface.SetState(null);
            }

            state = null;
        }

        public override void UpdateUI(GameTime gameTime)
        {
            if (state == null)
            {
                return;
            }

            Cart cart = state.Cart;
            Player player = Main.LocalPlayer;

            if (!Main.playerInventory || !cart.Active || cart.Module != CartModule.Chest || player.dead
                || Vector2.Distance(player.Center, cart.Position) > CloseRange)
            {
                CloseInternal(false);
                return;
            }

            if (chestInterface != null)
            {
                chestInterface.Update(gameTime);
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int inventoryIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Inventory"));
            if (inventoryIndex == -1)
            {
                return;
            }

            layers.Insert(inventoryIndex + 1, new LegacyGameInterfaceLayer(
                "Factorraria: Cart Chest UI",
                delegate
                {
                    if (chestInterface != null && chestInterface.CurrentState != null && state != null)
                    {
                        Reposition();
                        chestInterface.Draw(Main.spriteBatch, new GameTime());
                    }

                    return true;
                },
                InterfaceScaleType.UI));
        }

        /// <summary>Puts the panel above the cart (it follows the cart as it rolls), kept inside the screen.</summary>
        private void Reposition()
        {
            if (state == null || state.Panel == null)
            {
                return;
            }

            Vector2 world = state.Cart.Position + new Vector2(-CartChestUIState.PanelWidth / 2f, -CartChestUIState.PanelHeight - AboveCart);
            Vector2 screen = Vector2.Transform(world - Main.screenPosition, Main.GameViewMatrix.ZoomMatrix) / Main.UIScale;

            float maxX = Math.Max(0f, Main.screenWidth / Main.UIScale - CartChestUIState.PanelWidth);
            float maxY = Math.Max(0f, Main.screenHeight / Main.UIScale - CartChestUIState.PanelHeight);

            state.Panel.Left.Set(MathHelper.Clamp(screen.X, 0f, maxX), 0f);
            state.Panel.Top.Set(MathHelper.Clamp(screen.Y, 0f, maxY), 0f);
            state.Recalculate();
        }
    }
}
