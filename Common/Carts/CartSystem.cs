using Factorraria.Common.Liquids;
using Factorraria.Content.Configs;
using Factorraria.Content.Items.Carts;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using ReLogic.Content;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Factorraria.Common.Net;

namespace Factorraria.Common.Carts
{
    /// <summary>
    /// Owns every cart in the world. Same idea as VirtualItemSystem: carts are plain data that this system
    /// simulates, draws and saves. Single player only for now (no netcode yet).
    /// </summary>
    public class CartSystem : ModSystem
    {
        public static readonly List<Cart> Carts = new List<Cart>();

        private const float PlaceRange = 6f * 16f;
        private const float InteractRange = 7f * 16f;

        private static bool reportedFailure;
        private static bool chainsNeedRebuild; // set by LoadWorldData, handled on the first tick the track tables are ready

        #region Ids (patch 9.1)

        // Every cart has a stable Id (Cart.Id): assigned from this counter when the cart is created or loaded, saved with the cart,
        // never reused (the counter is saved too and only goes up). Only the server and single-player create carts; a client replica
        // carries the id the server sent (patch 9.2) and never touches this counter.
        private static int nextCartId = 1;

        /// <summary>The id the next new cart will get.</summary>
        public static int NextCartId => nextCartId;

        /// <summary>Gives the cart the next id if it has none yet.</summary>
        public static void AssignId(Cart cart)
        {
            if (cart.Id <= 0)
            {
                cart.Id = nextCartId++;
            }
        }

        /// <summary>The live cart with this id, or null (unknown id, or the cart is gone). Use this instead of holding a Cart across ticks.</summary>
        public static Cart FindById(int id)
        {
            if (id <= 0)
            {
                return null;
            }

            for (int i = 0; i < Carts.Count; i++)
            {
                if (Carts[i].Id == id)
                {
                    return Carts[i].Active ? Carts[i] : null;
                }
            }

            return null;
        }

        #endregion

        #region Lifecycle / saving

        public override void Load()
        {
            Cart.Bumped += OnCartBumped;
            Cart.Collided += OnCartCollide;
            Cart.FrameChanged += OnCartFrameChanged;
            CartChain.Coupled += OnCartCoupled;
            Cart.Driving += OnCartDriving;
        }

        public override void Unload()
        {
            Cart.Bumped -= OnCartBumped;
            Cart.Collided -= OnCartCollide;
            Cart.FrameChanged -= OnCartFrameChanged;
            CartChain.Coupled -= OnCartCoupled;
            Cart.Driving -= OnCartDriving;
            terrariumLiquidAsset = null;
            chainAssets.Clear();
            chainTexturesFailed.Clear();
        }

        /// <summary>Placeholder bumper "boing". Swap the sound here once there is a real one.</summary>
        private static void OnCartBumped(Cart cart)
        {
            CartSound.Play(CartSoundKind.Bump, cart.Position);

            CartCargo.OnBump(cart); // chest: spill items as vItems, terrarium: pour the tank out
        }

        /// <summary>
        /// Customisable hook: a free cart just hooked onto a motor's chain and snapped into its slot.
        /// Currently a placeholder sound. Put sparks, a UI message etc. here later.
        /// </summary>
        private static void OnCartCoupled(Cart motor, Cart carriage)
        {
            CartSound.Play(CartSoundKind.Couple, carriage.Position);
        }

        /// <summary>
        /// Customisable cart-vs-cart collision hook. Runs after the elastic exchange; left/right are ordered by X.
        /// Currently just plays a sound. Put sparks, damage, cargo spill etc. here later.
        /// </summary>
        private static void OnCartCollide(Cart left, Cart right)
        {
            Vector2 midPoint = (left.Position + right.Position) * 0.5f;
            CartSound.Play(CartSoundKind.Collide, midPoint);
        }

        /// <summary>
        /// Customisable hook: runs every tick a motor cart has power (accelerating or cruising at its top speed).
        /// Fuel carts, the liquid carts and the mechanical cart all call it: switch on cart.FuelLiquid (LiquidTypeRegistry.Lava / Honey / Water), cart.AlwaysRuns or cart.SkinType.
        /// Put particles and sounds here. Currently does nothing.
        /// </summary>
        private static void OnCartDriving(Cart cart)
        {
            if (Main.dedServ)
            {
                return;
            }
        }

        // Where the smoke leaves the motor cart, in px from the rail point: along the floor in the facing direction, and up.
        private const float SmokeForward = 12f;
        private const float SmokeLift = 30f;

        /// <summary>
        /// Customisable animation hook: runs whenever any cart's frame changes. Currently only gives a burning motor cart
        /// train-style smoke. Put per-skin particles or sounds here (switch on cart.SkinType / cart.AnimFrame).
        /// </summary>
        private static void OnCartFrameChanged(Cart cart, int previousFrame, int currentFrame)
        {
            if (Main.dedServ || cart.Module != CartModule.Motor || !cart.IsBurning || !Main.rand.NextBool(3))
            {
                return;
            }

            const float onScreenPadding = 300f;
            if (cart.Position.X < Main.screenPosition.X - onScreenPadding || cart.Position.X > Main.screenPosition.X + Main.screenWidth + onScreenPadding
                || cart.Position.Y < Main.screenPosition.Y - onScreenPadding || cart.Position.Y > Main.screenPosition.Y + Main.screenHeight + onScreenPadding)
            {
                return;
            }
            
            Vector2 spawn = cart.Position + CartGeometry.Up(cart) * SmokeLift * 1.2f + CartGeometry.Right(cart) * (cart.Facing * SmokeForward * -1f);

            for (int i = 0; i < 3; i++)
            {
                Vector2 velocity = new Vector2(Main.WindForVisuals * 1.5f + Main.rand.NextFloat(-0.2f, 0.2f), -Main.rand.NextFloat(0.6f, 1.2f));
                velocity += new Vector2(Main.rand.NextFloat(-1f, 1f),0f);
                // Primary smoke particle
                Dust smoke1 = Dust.NewDustPerfect(
                    spawn,
                    DustID.Smoke,
                    velocity,
                    Alpha: Main.rand.Next(0, 50),
                    newColor: default,
                    Scale: Main.rand.NextFloat(1f, 2f)
                );
                smoke1.noGravity = true;
                smoke1.velocity.Y -= 1f;

                // Optional larger, puffier secondary smoke particle
                if (Main.rand.NextBool(2))
                {
                    Dust smoke2 = Dust.NewDustPerfect(
                        spawn,
                        DustID.Smoke,
                        velocity,
                        Alpha: Main.rand.Next(150, 200),
                        newColor: default,
                        Scale: Main.rand.NextFloat(2f, 3f)
                    );
                    smoke2.noGravity = true;

                    smoke1.velocity.Y -= 1f;
                }                
            }

        }

        public override void OnWorldLoad()
        {
            // OnWorldLoad runs BEFORE LoadWorldData (WorldFile.LoadWorld), so clearing here is safe. It drops carts a
            // just-generated world (CartWorldGen) left in memory, which would otherwise leak into the next world loaded.
            Carts.Clear();
            nextCartId = 1;
            TrackData.Reset();
            reportedFailure = false;
        }

        /// <summary>Start of every world generation: forget carts from any earlier world so CartWorldGen starts from nothing.</summary>
        public override void ClearWorld()
        {
            Carts.Clear();
            nextCartId = 1;
        }

        public override void OnWorldUnload()
        {
            Carts.Clear();
            nextCartId = 1;
            TrackData.Reset();
        }

        public override void SaveWorldData(TagCompound tag)
        {
            int nextChainId = 1;
            for (int i = 0; i < Carts.Count; i++)
            {
                if (Carts[i].Active && Carts[i].Chain != null && Carts[i].Chain.Motor == Carts[i])
                {
                    Carts[i].Chain.SaveId = nextChainId++;
                }
            }

            // Defensive: every cart that is saved has an id (carts get theirs when created, so this normally does nothing).
            for (int i = 0; i < Carts.Count; i++)
            {
                if (Carts[i].Active)
                {
                    AssignId(Carts[i]);
                }
            }

            List<TagCompound> saved = new List<TagCompound>();

            for (int i = 0; i < Carts.Count; i++)
            {
                if (Carts[i].Active)
                {
                    saved.Add(Carts[i].Save());
                }
            }

            if (saved.Count > 0)
            {
                tag["carts"] = saved;
            }

            if (nextCartId > 1)
            {
                tag["nextCartId"] = nextCartId; // saved even with no carts left, so an id is never handed out twice
            }
        }

        public override void LoadWorldData(TagCompound tag)
        {
            Carts.Clear();
            nextCartId = Math.Max(1, tag.GetInt("nextCartId"));

            foreach (TagCompound cartTag in tag.GetList<TagCompound>("carts"))
            {
                Carts.Add(Cart.Load(cartTag));
            }

            // The counter must be above every saved id, then carts without an id (saved before 9.1) and duplicates (a damaged save)
            // get fresh ones in list order.
            for (int i = 0; i < Carts.Count; i++)
            {
                if (Carts[i].Id >= nextCartId)
                {
                    nextCartId = Carts[i].Id + 1;
                }
            }

            HashSet<int> usedIds = new HashSet<int>();
            int assigned = 0;

            for (int i = 0; i < Carts.Count; i++)
            {
                if (Carts[i].Id <= 0 || !usedIds.Add(Carts[i].Id))
                {
                    Carts[i].Id = nextCartId++;
                    usedIds.Add(Carts[i].Id);
                    assigned++;
                }
            }

            Mod.Logger.Info("[Carts] loaded " + Carts.Count + " cart(s), next id " + nextCartId + ", " + assigned + " id(s) assigned on load");

            chainsNeedRebuild = true;
        }

        #endregion

        #region Simulation

        public override void PostUpdateWorld()
        {
            // Patch 9.1 (C2): the server (or single-player) owns the whole simulation; a client only eases its replicas toward the
            // last state the server sent. With no replicas yet (until 9.2) the client loop does nothing.
            // PostUpdateWorld (WorldGen.UpdateWorld) never runs on a multiplayer client (see patch 7.3b), so the client side is driven by
            // CartNet.ClientTick from PostUpdateEverything. 9.1 had the client branch here, which therefore never ticked.
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                return;
            }

            ServerSimulate();
            CartNet.ServerTick();
        }

        /// <summary>Client only, called every tick by CartNet.ClientTick: eases every replica toward its last server state.</summary>
        public static void ClientInterpolate()
        {
            for (int i = 0; i < Carts.Count; i++)
            {
                Carts[i].ClientStep();
            }
        }

        private static void ServerSimulate()
        {
            if (Carts.Count == 0)
            {
                return;
            }

            if (!TrackData.EnsureLoaded())
            {
                ReportFailureOnce();
                return;
            }

            if (chainsNeedRebuild)
            {
                chainsNeedRebuild = false;
                CartChain.RebuildFromSave(Carts);
            }

            // Free carts and motors move on their own. Carriages are placed by their chain just below.
            for (int i = 0; i < Carts.Count; i++)
            {
                if (!Carts[i].IsCarriage)
                {
                    Carts[i].Update();
                }
            }

            // Each chain places its carriages on the trail its motor just recorded. Only a dead motor drops the chain (a derailed one keeps it).
            for (int i = 0; i < Carts.Count; i++)
            {
                Cart motor = Carts[i];

                if (motor.Chain == null || motor.Chain.Motor != motor)
                {
                    continue;
                }

                if (!motor.Active)
                {
                    motor.Chain.Dissolve();
                    continue;
                }

                motor.Chain.UpdateCarriages();
            }

            for (int i = Carts.Count - 1; i >= 0; i--)
            {
                Cart cart = Carts[i];

                if (cart.Active)
                {
                    CartCargo.Update(cart);
                }

                if (!cart.Active)
                {
                    if (cart.Chain != null)
                    {
                        cart.Chain.Remove(cart);
                    }

                    Carts.RemoveAt(i);
                }
            }

            // Gentle touches hook a free cart onto a chain; anything harder is a collision (linked carts never collide with each other).
            CartChain.AutoAttach(Carts);

            // Cart-vs-cart collisions (after everyone has moved this tick)
            for (int i = 0; i < Carts.Count; i++)
            {
                for (int j = i + 1; j < Carts.Count; j++)
                {
                    Cart.TryCollide(Carts[i], Carts[j]);
                }
            }
        }

        private static void ReportFailureOnce()
        {
            if (reportedFailure || TrackData.FailureReason == null)
            {
                return;
            }

            reportedFailure = true;
            string message = "Factorraria carts are disabled: could not read the vanilla minecart tables (" + TrackData.FailureReason + ")";
            ModContent.GetInstance<CartSystem>().Mod.Logger.Error(message);
            Main.NewText(message, Color.OrangeRed);
        }

        #endregion

        #region Placing / removing

        /// <summary>Checks that a cart can be placed on the track tile at (tileX, tileY) and returns the rail point.</summary>
        public static bool CanPlaceAt(Player player, int tileX, int tileY, out Vector2 railPoint)
        {
            bool onTrack;
            return CheckPlacement(player, tileX, tileY, out railPoint, out onTrack);
        }

        /// <summary>
        /// Same rules as CanPlaceAt, but also reports whether there is track under the cursor at all, and fills
        /// railPoint whenever there is (even if the spot is rejected for range/overlap), so the hologram can draw there.
        /// </summary>
        public static bool CheckPlacement(Player player, int tileX, int tileY, out Vector2 railPoint, out bool onTrack)
        {
            railPoint = Vector2.Zero;
            onTrack = false;

            if (!TrackData.EnsureLoaded())
            {
                return false;
            }

            float x = tileX * 16f + 8f;

            TrackSample sample;
            if (!TrackData.TrySample(x, tileY * 16f + 8f, out sample) || !sample.HasSurface)
            {
                return false;
            }

            onTrack = true;
            railPoint = new Vector2(x, sample.SurfaceY);

            if (Vector2.Distance(player.Center, railPoint) > PlaceRange)
            {
                return false;
            }

            for (int i = 0; i < Carts.Count; i++)
            {
                Vector2 other = Carts[i].Position;
                if (Math.Abs(other.X - railPoint.X) < CartPhysics.HitboxWidth && Math.Abs(other.Y - railPoint.Y) < CartPhysics.HitboxHeight)
                {
                    return false; // already a cart here
                }
            }

            return true;
        }

        public static Cart SpawnCart(Vector2 railPoint, int skinItemType = ItemID.Minecart, CartModule module = CartModule.Empty)
        {
            Cart cart = new Cart();
            cart.Skin = CartSkins.KeyOf(skinItemType);
            if (module == CartModule.Empty && Cart.IsMotorSkin(skinItemType))
            {
                module = CartModule.Motor; // the Steampunk Minecart is always a motor cart
            }

            cart.SetModule(module);
            cart.Position = railPoint;
            cart.OnTrack = true;
            AssignId(cart);
            Carts.Add(cart);
            return cart;
        }

        #endregion

        #region Interaction (right click on a cart)

        /// <summary>
        /// Click rules. Left click: with a cart item = place; on a cart while holding a chest = install it; on a cart = shove.
        /// Right click on a chest or terrarium cart = open its panel (shift + right click picks it up); on any other cart = pick it up.
        /// Fuel will take over right click on motor carts later.
        /// A cart under the cursor beats other uses of the click unless a cart item is held.
        /// </summary>
        public override void PostUpdatePlayers()
        {
            if (Main.dedServ || Main.gameMenu)
            {
                return;
            }

            Player player = Main.LocalPlayer;

            // Inventory open is fine: mouseInterface is already true while the cursor is over any inventory panel.
            if (player.dead || player.mouseInterface || player.lastMouseInterface)
            {
                return;
            }

            bool left = Main.mouseLeft && Main.mouseLeftRelease;
            bool right = Main.mouseRight && Main.mouseRightRelease;

            if (!left && !right)
            {
                return;
            }

            Item held = CartSkinTable.GetHeldItem(player);

            if (left)
            {
                if (CartSkinTable.IsSkinItem(held))
                {
                    if (TryPlaceFromItem(player, held))
                    {
                        Main.mouseLeftRelease = false;
                    }

                    return;
                }

                if (CartSkinTable.IsPlaceable(held))
                {
                    return; // legacy Track Cart item places itself through its own use
                }

                Cart target = FindCartAtCursor(player);
                if (target == null)
                {
                    return;
                }

                Main.mouseLeftRelease = false; // we used this click

                if (Main.netMode == NetmodeID.MultiplayerClient)
                {
                    CartInputNet.RequestInstallOrShove(held, target);
                    return;
                }

                if (TryInstallModule(held, target))
                {
                    SoundEngine.PlaySound(SoundID.Dig, target.Position);
                    return;
                }

                // Shove away from the player. Test hook until motor carts exist.
                float away = target.Position.X >= player.Center.X ? 1f : -1f;
                target.Shove(away * CartPhysics.ShoveSpeed);
                return;
            }

            if (right)
            {
                Cart target = FindCartAtCursor(player);
                if (target == null)
                {
                    return;
                }

                Main.mouseRightRelease = false;

                if (target.HasPanel && !Terraria.UI.ItemSlot.ShiftInUse)
                {
                    if (Main.netMode == NetmodeID.MultiplayerClient)
                    {
                        CartInputNet.PanelNotReady(); // chest and fuel contents are not synced yet (patch 9.5)
                        return;
                    }

                    CartChestUISystem.Toggle(target);
                    return;
                }

                if (target.AlwaysRuns && !Terraria.UI.ItemSlot.ShiftInUse)
                {
                    return; // the mechanical cart has no panel: only Shift + Right click picks it up
                }

                if (Main.netMode == NetmodeID.MultiplayerClient)
                {
                    CartInputNet.RequestPickUp(target);
                    return;
                }

                PickUp(player, target);
            }
        }

        /// <summary>Nearest cart under the mouse (hit-tested on the rotated hitbox) that is within reach of the player.</summary>
        public static Cart FindCartAtCursor(Player player)
        {
            if (Carts.Count == 0)
            {
                return null;
            }

            Vector2 mouse = GetZoomCorrectedMouseWorld();
            Cart best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < Carts.Count; i++)
            {
                Cart cart = Carts[i];

                if (!CartGeometry.Contains(cart, mouse, 4f))
                {
                    continue;
                }

                float distance = Vector2.Distance(player.Center, cart.Position);
                if (distance <= InteractRange && distance < bestDistance)
                {
                    bestDistance = distance;
                    best = cart;
                }
            }

            return best;
        }

        private static bool TryPlaceFromItem(Player player, Item held)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                // The server re-checks everything and spawns the cart; the item is consumed when it answers "accepted".
                return CartInputNet.RequestPlace(player, held);
            }

            Vector2 railPoint;
            if (!CheckPlacement(player, Player.tileTargetX, Player.tileTargetY, out railPoint, out _))
            {
                return false;
            }

            Cart cart = SpawnCart(railPoint, CartSkinTable.SkinTypeFor(held));
            cart.Facing = player.direction >= 0 ? 1 : -1;
            SoundEngine.PlaySound(SoundID.Dig, railPoint);

            held.stack--;
            if (held.stack <= 0)
            {
                held.TurnToAir();
            }

            return true;
        }

        /// <summary>Installs the held item as a module if it is one. Chest and Terrarium exist so far (Motor comes later).</summary>
        private static bool TryInstallModule(Item held, Cart cart)
        {
            CartModule module;
            if (!GetInstallModule(held, cart, out module))
            {
                return false;
            }

            cart.SetModule(module, held.type);

            held.stack--;
            if (held.stack <= 0)
            {
                held.TurnToAir();
            }

            return true;
        }

        /// <summary>Which module the held item would install in this cart, without changing anything (false: not a module, or the cart cannot take one).</summary>
        public static bool GetInstallModule(Item held, Cart cart, out CartModule module)
        {
            module = CartModule.Empty;

            if (held == null || held.IsAir || cart == null || cart.Module != CartModule.Empty || Cart.IsMotorSkin(cart.SkinType))
            {
                return false;
            }

            if (Cart.IsChestItem(held))
            {
                module = CartModule.Chest;
                return true;
            }

            if (Cart.IsTerrariumItem(held))
            {
                module = CartModule.Terrarium;
                return true;
            }

            return false;
        }

        /// <summary>True when the player is within clicking range of the cart (the client's own check, with slackPixels extra for the server's lagging copy of the player).</summary>
        public static bool IsInInteractRange(Player player, Cart cart, float slackPixels)
        {
            return player != null && player.active && !player.dead && cart != null && cart.Active
                && Vector2.Distance(player.Center, cart.Position) <= InteractRange + slackPixels;
        }

        /// <summary>Removes the cart and gives the player its skin item, module item and contents.</summary>
        public static void PickUp(Player player, Cart cart)
        {
            CartChestUISystem.NotifyCartRemoved(cart);

            if (cart.Chain != null)
            {
                cart.Chain.Remove(cart); // a middle carriage: the rest close the gap. The motor: the whole chain breaks up
            }
            if (cart.Module == CartModule.Terrarium)
            {
                CartCargo.PourTank(cart); // a terrarium cart empties back into the world instead of voiding its liquid
            }
            cart.Active = false;

            List<Item> items = cart.CollectPickupItems();
            for (int i = 0; i < items.Count; i++)
            {
                player.QuickSpawnItem(player.GetSource_Misc("FactorrariaCartPickup"), items[i], items[i].stack);
            }
        }

        private static Vector2 GetZoomCorrectedMouseWorld()
        {
            Vector2 mouseScreen = new Vector2(Main.mouseX, Main.mouseY);
            Vector2 transformed = Vector2.Transform(mouseScreen, Matrix.Invert(Main.GameViewMatrix.TransformationMatrix));
            return transformed + Main.screenPosition;
        }

        #endregion

        #region Drawing

        public override void PostDrawTiles()
        {
            Vector2 holoPoint;
            bool holoValid;
            int holoSkin;
            bool hologram = TryGetHologram(out holoPoint, out holoValid, out holoSkin);

            if (Carts.Count == 0 && !hologram)
            {
                return;
            }

            FurnaceOffsetConfig config = ModContent.GetInstance<FurnaceOffsetConfig>();
            bool debug = config != null && config.EnableDebugs;

            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                Main.DefaultSamplerState,
                DepthStencilState.None,
                RasterizerState.CullCounterClockwise,
                null,
                Main.GameViewMatrix.TransformationMatrix
            );

            const float padding = 64f;
            float minX = Main.screenPosition.X - padding;
            float maxX = Main.screenPosition.X + Main.screenWidth + padding;
            float minY = Main.screenPosition.Y - padding;
            float maxY = Main.screenPosition.Y + Main.screenHeight + padding;

            for (int i = 0; i < Carts.Count; i++)
            {
                Cart motor = Carts[i];

                if (motor.Chain != null && motor.Chain.Motor == motor)
                {
                    Cart front = motor;

                    for (int c = 0; c < motor.Chain.Carriages.Count; c++)
                    {
                        DrawChainLink(front, motor.Chain.Carriages[c]);
                        front = motor.Chain.Carriages[c];
                    }
                }
            }

            for (int i = 0; i < Carts.Count; i++)
            {
                Cart cart = Carts[i];

                if (cart.Position.X < minX || cart.Position.X > maxX || cart.Position.Y < minY || cart.Position.Y > maxY)
                {
                    continue;
                }

                Color light = Lighting.GetColor((int)(cart.Position.X / 16f), (int)((cart.Position.Y - 8f) / 16f));
                Vector2 screen = cart.Position - Main.screenPosition;

                DrawCart(cart, screen, light);

                if (debug)
                {
                    DrawDebug(cart, screen);
                }
            }

            if (hologram)
            {
                Color tint = holoValid ? Color.White * 0.55f : new Color(255, 70, 70) * 0.6f;
                SpriteEffects effects = Main.LocalPlayer.direction < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

                DrawCartSprite(holoSkin, CartSkinTable.GetAnimation(holoSkin).StandFrame, holoPoint - Main.screenPosition, tint, 0f, effects);
            }

            Main.spriteBatch.End();
        }

        /// <summary>
        /// Placement preview while a cart item is held: faded when the cart can go there, red when not. The preview sits
        /// on the rail when there is track under the cursor (even if range/overlap reject it), else on the cursor tile.
        /// </summary>
        private static bool TryGetHologram(out Vector2 point, out bool valid, out int skinType)
        {
            point = Vector2.Zero;
            valid = false;
            skinType = ItemID.Minecart;

            if (Main.dedServ || Main.gameMenu)
            {
                return false;
            }

            Player player = Main.LocalPlayer;
            Item held = CartSkinTable.GetHeldItem(player);
            if (player.dead || player.mouseInterface || !CartSkinTable.IsPlaceable(held))
            {
                return false;
            }

            skinType = CartSkinTable.SkinTypeFor(held);

            Vector2 railPoint;
            bool onTrack;
            valid = CheckPlacement(player, Player.tileTargetX, Player.tileTargetY, out railPoint, out onTrack);
            point = onTrack ? railPoint : new Vector2(Player.tileTargetX * 16f + 8f, Player.tileTargetY * 16f + 16f);
            return true;
        }

        /// <summary>Draws one cart at its current animation frame, rotated about its feet and flipped by its facing.</summary>
        private static void DrawCart(Cart cart, Vector2 screen, Color light)
        {
            SpriteEffects effects = cart.Facing < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            // The module goes first so the cart's front wall is drawn over it and it looks like it sits inside.
            DrawModule(cart, screen, light, effects);
            DrawCartSprite(cart.SkinType, cart.AnimFrame, screen, light, cart.Rotation, effects);
        }

        private const float ModuleScale = 0.75f;        // module icon size relative to its item sprite
        private const float ModuleLiftFraction = 0.4f;  // height of the module's base above the rail, as a fraction of the cart frame

        /// <summary>Draws the installed chest / terrarium (its item icon) inside the cart, rotated and flipped with it.</summary>
        private static void DrawModule(Cart cart, Vector2 screen, Color light, SpriteEffects effects)
        {
            if (cart.Module == CartModule.Empty)
            {
                return;
            }

            int itemType = cart.ModuleItem > 0 ? cart.ModuleItem : Cart.ModuleItemType(cart.Module);
            if (itemType <= 0)
            {
                return;
            }

            Main.instance.LoadItem(itemType);
            Texture2D icon = TextureAssets.Item[itemType].Value;

            CartSkinTable.PlacedSprite body = CartSkinTable.GetPlacedSprite(cart.SkinType, cart.AnimFrame);
            Vector2 position = screen + CartGeometry.Up(cart) * (body.Source.Height * ModuleLiftFraction * 1.2f);

            if (cart.Module == CartModule.Terrarium)
            {
                DrawTerrariumLiquid(cart, position, light, effects);
            }

            Main.spriteBatch.Draw(icon, position, null, light, cart.Rotation, new Vector2(icon.Width / 2f, icon.Height), ModuleScale * 1.2f, effects, 0f);
        }

        // The liquid art for the terrarium: drawn behind the terrarium icon with the same anchor and scale, so make it the same size as the
        // terrarium item sprite. Painted for water; LiquidTextureCache recolours it for whatever liquid is in the tank.
        // Put the file at Common/Carts/TerrariumLiquid.png. While it does not exist nothing is drawn.
        private const string TerrariumLiquidPath = "Factorraria/Common/Carts/TerrariumLiquid";
        private static Asset<Texture2D> terrariumLiquidAsset;

        /// <summary>Draws the (recoloured) liquid texture inside the terrarium. Only the bottom part matching the tank's fill is shown.</summary>
        private static void DrawTerrariumLiquid(Cart cart, Vector2 position, Color light, SpriteEffects effects)
        {
            LiquidStack tank = cart.Tank;
            if (tank == null || tank.IsEmpty || tank.Capacity <= 0f || tank.LiquidType >= LiquidTypeRegistry.definitions.Count)
            {
                return;
            }

            if (terrariumLiquidAsset == null)
            {
                if (!ModContent.HasAsset(TerrariumLiquidPath))
                {
                    return;
                }

                terrariumLiquidAsset = ModContent.Request<Texture2D>(TerrariumLiquidPath, AssetRequestMode.ImmediateLoad);
            }

            LiquidTypeDefinition liquid = LiquidTypeRegistry.Get(tank.LiquidType);
            Texture2D texture = LiquidTextureCache.GetOrCreate(TerrariumLiquidPath, terrariumLiquidAsset.Value, liquid);

            float fill = Math.Clamp(tank.Amount / tank.Capacity, 0f, 1f);
            int shownRows = Math.Max(1, (int)Math.Ceiling(texture.Height * fill));
            Rectangle source = new Rectangle(0, texture.Height - shownRows, texture.Width, shownRows);

            // origin = bottom centre of the shown part, which is the bottom centre of the full texture too
            Main.spriteBatch.Draw(texture, position, source, light, cart.Rotation, new Vector2(texture.Width / 2f, shownRows), ModuleScale * 1.2f, effects, 0f);
        }

        /// <summary>Draws a skin's mount frame (plus its optional extra layer) with the rail point at screen.</summary>
        private static void DrawCartSprite(int skinType, int frame, Vector2 screen, Color color, float rotation, SpriteEffects effects)
        {
            CartSkinTable.PlacedSprite sprite = CartSkinTable.GetPlacedSprite(skinType, frame);

            Main.spriteBatch.Draw(sprite.Texture, screen, sprite.Source, color, rotation, sprite.Origin, sprite.Scale, effects, 0f);

            if (sprite.Extra != null)
            {
                Main.spriteBatch.Draw(sprite.Extra, screen, sprite.Source, color, rotation, sprite.Origin, sprite.Scale, effects, 0f);
            }
        }

        // Each skin picks its own link art (CartSkinTable.GetChainName: a file in vanilla Images/). A missing file falls back to "Chain".
        private const float ChainHitchHeight = 15f; // px above the rail where the chain is hooked to a cart
        private static readonly Dictionary<string, Asset<Texture2D>> chainAssets = new Dictionary<string, Asset<Texture2D>>();
        private static readonly HashSet<string> chainTexturesFailed = new HashSet<string>();

        private static Texture2D GetChainTexture(string name)
        {
            Asset<Texture2D> asset;

            if (!chainAssets.TryGetValue(name, out asset) && !chainTexturesFailed.Contains(name))
            {
                try
                {
                    asset = Main.Assets.Request<Texture2D>("Images/" + name, AssetRequestMode.ImmediateLoad);
                    chainAssets[name] = asset;
                }
                catch (Exception)
                {
                    chainTexturesFailed.Add(name); // this art does not exist: use the default chain instead
                }
            }

            if (asset != null)
            {
                return asset.Value;
            }

            return name != "Chain" ? GetChainTexture("Chain") : null; // null = plain line fallback
        }

        /// <summary>Draws the chain between two neighbouring carts, from the edge of one to the edge of the other (nothing if they overlap).</summary>
        private static void DrawChainLink(Cart front, Cart back)
        {
            Vector2 a = front.Position + CartGeometry.Up(front) * ChainHitchHeight;
            Vector2 b = back.Position + CartGeometry.Up(back) * ChainHitchHeight;
            Vector2 between = b - a;
            float length = between.Length();
            float halfWidth = CartPhysics.HitboxWidth * 0.5f;

            if (length <= halfWidth * 2f + 2f)
            {
                return; // the carts overlap or touch (for example after a bump): no room for a chain
            }

            Vector2 middle = (a + b) * 0.5f;
            const float cullPadding = 128f;
            if (middle.X < Main.screenPosition.X - cullPadding || middle.X > Main.screenPosition.X + Main.screenWidth + cullPadding
                || middle.Y < Main.screenPosition.Y - cullPadding || middle.Y > Main.screenPosition.Y + Main.screenHeight + cullPadding)
            {
                return;
            }

            Vector2 direction = between / length;
            //Vector2 from = a + direction * halfWidth;
            //Vector2 to = b - direction * halfWidth;
            Vector2 from = a;
            Vector2 to = b;
            float distance = Vector2.Distance(from, to);
            Color light = Lighting.GetColor((int)(middle.X / 16f), (int)(middle.Y / 16f));

            Texture2D texture = GetChainTexture(CartSkinTable.GetChainName(front.SkinType));
            if (texture == null)
            {
                DrawLine(TextureAssets.MagicPixel.Value, from - Main.screenPosition, to - Main.screenPosition, light);
                return;
            }

            // Pieces are stretched a little so they fit the gap exactly (no overshoot at either end).
            int pieces = Math.Max(1, (int)Math.Ceiling(distance / texture.Height));
            float pieceLength = distance / pieces;
            float rotation = (float)Math.Atan2(direction.Y, direction.X) - MathHelper.PiOver2;
            Vector2 origin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);
            Vector2 scale = new Vector2(1f, pieceLength / texture.Height);

            for (int i = 0; i < pieces; i++)
            {
                Vector2 position = from + direction * (pieceLength * (i + 0.5f)) - Main.screenPosition;
                Main.spriteBatch.Draw(texture, position, null, light, rotation, origin, scale, SpriteEffects.None, 0f);
            }
        }

        private static void DrawDebug(Cart cart, Vector2 screen)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 axis = new Vector2((float)Math.Cos(cart.Rotation), (float)Math.Sin(cart.Rotation)) * CartPhysics.WheelHalfBase * 0.6f;

            DrawDot(pixel, screen - axis, Color.Cyan);   // left wheel probe
            DrawDot(pixel, screen + axis, Color.Orange); // right wheel probe
            DrawDot(pixel, screen, Color.Lime);          // cart centre on the rail
            DrawHitbox(cart, pixel);                      // rotated hitbox (white)
            DrawCargoWindow(cart, pixel);                 // 3x2 spill / pour tiles (yellow)
        }

        private static void DrawCargoWindow(Cart cart, Texture2D pixel)
        {
            if (cart.Module != CartModule.Chest && cart.Module != CartModule.Terrarium)
            {
                return;
            }

            int left;
            int top;
            CartCargo.GetWindow(cart, out left, out top);

            for (int column = 0; column < CartCargo.WindowColumns; column++)
            {
                for (int row = 0; row < CartCargo.WindowRows; row++)
                {
                    Vector2 a = new Vector2((left + column) * 16f, (top + row) * 16f) - Main.screenPosition;
                    Vector2 b = a + new Vector2(16f, 0f);
                    Vector2 c = a + new Vector2(16f, 16f);
                    Vector2 d = a + new Vector2(0f, 16f);

                    DrawLine(pixel, a, b, Color.Yellow);
                    DrawLine(pixel, b, c, Color.Yellow);
                    DrawLine(pixel, c, d, Color.Yellow);
                    DrawLine(pixel, d, a, Color.Yellow);
                }
            }
        }

        private static void DrawDot(Texture2D pixel, Vector2 position, Color color)
        {
            Main.spriteBatch.Draw(pixel, new Rectangle((int)position.X - 2, (int)position.Y - 2, 4, 4), color);
        }

        private static readonly Vector2[] hitboxCorners = new Vector2[4];

        private static void DrawHitbox(Cart cart, Texture2D pixel)
        {
            CartGeometry.GetCorners(cart, hitboxCorners);

            for (int i = 0; i < 4; i++)
            {
                Vector2 a = hitboxCorners[i] - Main.screenPosition;
                Vector2 b = hitboxCorners[(i + 1) % 4] - Main.screenPosition;
                DrawLine(pixel, a, b, Color.White);
            }
        }

        private static void DrawLine(Texture2D pixel, Vector2 from, Vector2 to, Color color)
        {
            Vector2 edge = to - from;
            float rotation = (float)Math.Atan2(edge.Y, edge.X);
            // MagicPixel is bigger than 1x1, so draw a 1x1 slice of it; otherwise the scale below is multiplied by its size.
            Main.spriteBatch.Draw(pixel, from, new Rectangle(0, 0, 1, 1), color, rotation, Vector2.Zero, new Vector2(edge.Length(), 1.5f), SpriteEffects.None, 0f);
        }

        #endregion
    }
}