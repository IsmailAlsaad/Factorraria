using Factorraria.Common.Systems;
using Factorraria.Common.Liquids;
using Factorraria.Common.UI.CustomUIElements;
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
        public const int Columns = 3;
        public const int Rows = Cart.ChestSlots / Columns;

        private const float SlotSize = 44f;
        private const float Gap = 6f;
        private const float Pad = 12f;
        private const float ButtonHeight = 28f;

        private const float GridWidth = Columns * SlotSize + (Columns - 1) * Gap;
        private const float GridHeight = Rows * SlotSize + (Rows - 1) * Gap;

        private const float ChestPanelWidth = GridWidth + Pad * 2f;
        private const float ChestPanelHeight = GridHeight + Gap + ButtonHeight + Gap + ButtonHeight + Pad * 2f;

        // Terrarium cart: no panel and no text, just the tank element above the cart. The tank art is 16 x 84, drawn at 1.5x.
        private const float TankWidth = 24f;
        private const float TankHeight = 126f;

        // Motor cart: a flame gauge above the single fuel slot.
        private const float FireSize = 54f;
        private const float MotorPanelWidth = SlotSize * 2f;
        private const float MotorPanelHeight = FireSize + Gap + SlotSize;

        private const float ButtonTextScale = 0.8f;
        private const float ButtonPadding = 12f; // UIPanel's default inner padding; scaled with the zoom so buttons keep their proportions

        // UI pixels (at zoom 1) between the top of the cart and the bottom of the UI.
        private const float ChestGapAboveCart = 14f;
        private const float TerrariumGapAboveCart = 6f;

        /// <summary>One element of the layout, stored at zoom = 1 (same idea as MachineUIElementEntry).</summary>
        private sealed class Entry
        {
            public UIElement Element;
            public Vector2 BasePosition;
            public Vector2 BaseSize;
            public UITextPanel<string> Button; // set for text buttons so their text and padding scale too
            public string Text;
        }

        public readonly Cart Cart;

        /// <summary>Root element everything is parented to. A bordered UIPanel for chest carts, a plain invisible element for terrarium carts.</summary>
        public UIElement Panel;

        private readonly bool terrarium;
        private readonly bool motor;
        private readonly float basePanelW;
        private readonly float basePanelH;
        private readonly List<Entry> entries = new List<Entry>();
        private float appliedZoom = -1f;

        /// <summary>UI pixels per world pixel (camera zoom divided by UI scale), as last given to SetZoomScale.</summary>
        public float Zoom { get; private set; } = 1f;

        /// <summary>Current size of the whole UI in UI pixels (follows the zoom).</summary>
        public float PanelW => basePanelW * Zoom;
        public float PanelH => basePanelH * Zoom;

        /// <summary>Gap between the top of the cart and the bottom of the UI, in UI pixels (follows the zoom).</summary>
        public float GapAboveCart => ((terrarium || motor) ? TerrariumGapAboveCart : ChestGapAboveCart) * Zoom;

        public CartChestUIState(Cart cart)
        {
            Cart = cart;

            terrarium = cart.Module == CartModule.Terrarium;
            motor = cart.Module == CartModule.Motor;
            basePanelW = terrarium ? TankWidth : motor ? MotorPanelWidth : ChestPanelWidth;
            basePanelH = terrarium ? TankHeight : motor ? MotorPanelHeight : ChestPanelHeight;
        }

        public override void OnInitialize()
        {
            entries.Clear();

            if (terrarium || motor)
            {
                Panel = new UIElement();
            }
            else
            {
                Panel = new UIPanel();
            }

            Panel.SetPadding(0f);
            Append(Panel);

            if (terrarium)
            {
                BuildTerrarium();
            }
            else if (motor)
            {
                BuildMotor();
            }
            else
            {
                BuildChest();
            }

            appliedZoom = -1f;
            SetZoomScale(Zoom);
        }

        private void BuildChest()
        {
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

                AddEntry(
                    slot,
                    new Vector2(Pad + (i % Columns) * (SlotSize + Gap), Pad + (i / Columns) * (SlotSize + Gap)),
                    new Vector2(SlotSize, SlotSize));
            }

            float buttonTop = Pad + GridHeight + Gap;

            AddButton("Loot All", new Vector2(Pad, buttonTop), LootAll);
            AddButton("Quick Stack", new Vector2(Pad, buttonTop + ButtonHeight + Gap), QuickStack);
        }

        /// <summary>Terrarium cart: only the tank element machines use, nothing else.</summary>
        private void BuildTerrarium()
        {
            AddEntry(new LiquidTankUIElement(() => Cart.Tank), Vector2.Zero, new Vector2(TankWidth, TankHeight) * 0.8f);
        }

        /// <summary>Motor cart: the Gel Burner's flame gauge with one fuel slot underneath. The slot only takes burnable fuel, at most Cart.FuelSlotCap.</summary>
        private void BuildMotor()
        {
            AddEntry(
                new FireUIElement(() => Cart.FuelBurnFraction),
                Vector2.Zero,
                new Vector2(FireSize, FireSize));

            UIItemSlotWrapper slot = new UIItemSlotWrapper(
                ItemSlot.Context.ChestItem,
                () => Cart.Fuel ?? new Item(),
                item => Cart.Fuel = ClampFuelStack(item),
                null,
                Cart.AcceptsFuel);

            AddEntry(slot, new Vector2((MotorPanelWidth - SlotSize) / 2f, FireSize + Gap), new Vector2(SlotSize, SlotSize));
        }

        /// <summary>Keeps the fuel slot at Cart.FuelSlotCap: anything over goes back onto the cursor (or is dropped to the player if the cursor is busy).</summary>
        private static Item ClampFuelStack(Item item)
        {
            if (item == null || item.IsAir || item.stack <= Cart.FuelSlotCap)
            {
                return item;
            }

            Item excess = item.Clone();
            excess.stack = item.stack - Cart.FuelSlotCap;
            item.stack = Cart.FuelSlotCap;

            if (Main.mouseItem.IsAir)
            {
                Main.mouseItem = excess;
            }
            else
            {
                Main.LocalPlayer.QuickSpawnItem(Main.LocalPlayer.GetSource_Misc("FactorrariaCartFuel"), excess, excess.stack);
            }

            return item;
        }

        private void AddEntry(UIElement element, Vector2 basePosition, Vector2 baseSize, UITextPanel<string> button = null, string text = null)
        {
            entries.Add(new Entry { Element = element, BasePosition = basePosition, BaseSize = baseSize, Button = button, Text = text });
            Panel.Append(element);
        }

        private void AddButton(string text, Vector2 basePosition, Action onClick)
        {
            UITextPanel<string> button = new UITextPanel<string>(text, ButtonTextScale, false);
            button.OnLeftClick += (evt, element) => onClick();
            button.OnMouseOver += (evt, element) => SoundEngine.PlaySound(SoundID.MenuTick);

            AddEntry(button, basePosition, new Vector2(GridWidth, ButtonHeight), button, text);
        }

        /// <summary>
        /// Resizes and repositions every element for the current zoom, so the UI stays a fixed size relative to the world
        /// instead of the screen. Same job as MachineUIStateBase.SetZoomScale. zoom = ZoomMatrix.M11 / UIScale.
        /// </summary>
        public void SetZoomScale(float zoom)
        {
            if (Panel == null || Math.Abs(zoom - appliedZoom) < 0.0001f)
            {
                return;
            }

            appliedZoom = zoom;
            Zoom = zoom;

            Panel.Width.Set(basePanelW * zoom, 0f);
            Panel.Height.Set(basePanelH * zoom, 0f);

            foreach (Entry entry in entries)
            {
                entry.Element.Left.Set(entry.BasePosition.X * zoom, 0f);
                entry.Element.Top.Set(entry.BasePosition.Y * zoom, 0f);
                entry.Element.Width.Set(entry.BaseSize.X * zoom, 0f);
                entry.Element.Height.Set(entry.BaseSize.Y * zoom, 0f);

                if (entry.Button != null)
                {
                    entry.Button.SetPadding(ButtonPadding * zoom);
                    entry.Button.SetText(entry.Text, ButtonTextScale * zoom, false);
                }
            }

            Panel.Recalculate();
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
            if (cart == null || !(cart.Module == CartModule.Motor ? cart.TryAddFuel(item) : cart.TryAddToChest(item)))
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

            if (!Main.playerInventory || !cart.Active || (cart.Module != CartModule.Chest && cart.Module != CartModule.Terrarium && cart.Module != CartModule.Motor) || player.dead
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

            // Same zoom factor the machine UIs use: the whole UI is resized so it stays a fixed size relative to the world.
            state.SetZoomScale(Main.GameViewMatrix.ZoomMatrix.M11 / Main.UIScale);

            // Anchor on the top-centre of the cart's box, converted to UI coordinates. Only that one point goes through the
            // zoom; the UI's size and its gap above the cart both scale with it, so it keeps hugging the cart at any zoom or UI scale.
            Cart cart = state.Cart;
            Vector2 anchor = new Vector2(cart.Position.X, cart.Position.Y - CartPhysics.HitboxHeight);
            Vector2 screen = Vector2.Transform(anchor - Main.screenPosition, Main.GameViewMatrix.ZoomMatrix) / Main.UIScale;

            float maxX = Math.Max(0f, Main.screenWidth / Main.UIScale - state.PanelW);
            float maxY = Math.Max(0f, Main.screenHeight / Main.UIScale - state.PanelH);

            // Whole pixels only, so the panel does not shimmer while the cart rolls.
            float left = (float)Math.Round(MathHelper.Clamp(screen.X - state.PanelW / 2f, 0f, maxX));
            float top = (float)Math.Round(MathHelper.Clamp(screen.Y - state.PanelH - state.GapAboveCart, 0f, maxY));

            state.Panel.Left.Set(left, 0f);
            state.Panel.Top.Set(top, 0f);
            state.Recalculate();
        }
    }
}
