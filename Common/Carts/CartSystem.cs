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

        #region Lifecycle / saving

        public override void Load()
        {
            Cart.Bumped += OnCartBumped;
            Cart.Collided += OnCartCollide;
            Cart.FrameChanged += OnCartFrameChanged;
            CartChain.Coupled += OnCartCoupled;
        }

        public override void Unload()
        {
            Cart.Bumped -= OnCartBumped;
            Cart.Collided -= OnCartCollide;
            Cart.FrameChanged -= OnCartFrameChanged;
            CartChain.Coupled -= OnCartCoupled;
            terrariumLiquidAsset = null;
            chainAsset = null;
        }

        /// <summary>Placeholder bumper "boing". Swap the sound here once there is a real one.</summary>
        private static void OnCartBumped(Cart cart)
        {
            SoundEngine.PlaySound(SoundID.Item56, cart.Position);

            CartCargo.OnBump(cart); // chest: spill items as vItems, terrarium: pour the tank out
        }

        /// <summary>
        /// Customisable hook: a free cart just hooked onto a motor's chain and snapped into its slot.
        /// Currently a placeholder sound. Put sparks, a UI message etc. here later.
        /// </summary>
        private static void OnCartCoupled(Cart motor, Cart carriage)
        {
            SoundEngine.PlaySound(SoundID.Item37, carriage.Position);
        }

        /// <summary>
        /// Customisable cart-vs-cart collision hook. Runs after the elastic exchange; left/right are ordered by X.
        /// Currently just plays a sound. Put sparks, damage, cargo spill etc. here later.
        /// </summary>
        private static void OnCartCollide(Cart left, Cart right)
        {
            Vector2 midPoint = (left.Position + right.Position) * 0.5f;
            SoundEngine.PlaySound(SoundID.Item53, midPoint);
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

        /// <summary>Motor cart recipe: Steampunk Boiler + Minecart at an Anvil gives the vanilla Steampunk Minecart.</summary>
        public override void AddRecipes()
        {
            int motorCart = Cart.SteampunkItemType;
            if (motorCart <= 0)
            {
                return; // the item lookup logged why
            }

            Recipe.Create(motorCart, 1)
                .AddIngredient(ItemID.SteampunkBoiler, 1)
                .AddIngredient(ItemID.Minecart, 1)
                .AddTile(TileID.Anvils)
                .Register();
        }

        public override void OnWorldLoad()
        {
            // Do not clear Carts here: LoadWorldData may already have filled it.
            TrackData.Reset();
            reportedFailure = false;
        }

        public override void OnWorldUnload()
        {
            Carts.Clear();
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
        }

        public override void LoadWorldData(TagCompound tag)
        {
            Carts.Clear();

            foreach (TagCompound cartTag in tag.GetList<TagCompound>("carts"))
            {
                Carts.Add(Cart.Load(cartTag));
            }

            chainsNeedRebuild = true;
        }

        #endregion

        #region Simulation

        public override void PostUpdateWorld()
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

            // Each chain places its carriages on the trail its motor just recorded. A motor that derailed or died drops the chain.
            for (int i = 0; i < Carts.Count; i++)
            {
                Cart motor = Carts[i];

                if (motor.Chain == null || motor.Chain.Motor != motor)
                {
                    continue;
                }

                if (!motor.Active || !motor.OnTrack)
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

            if (Main.netMode != NetmodeID.SinglePlayer || !TrackData.EnsureLoaded())
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
            if (Main.dedServ || Main.gameMenu || Main.netMode != NetmodeID.SinglePlayer)
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

                if ((target.Module == CartModule.Chest || target.Module == CartModule.Terrarium || target.Module == CartModule.Motor) && !Terraria.UI.ItemSlot.ShiftInUse)
                {
                    CartChestUISystem.Toggle(target);
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
            if (held == null || held.IsAir || cart.Module != CartModule.Empty || Cart.IsMotorSkin(cart.SkinType))
            {
                return false;
            }

            CartModule module;
            if (Cart.IsChestItem(held))
            {
                module = CartModule.Chest;
            }
            else if (Cart.IsTerrariumItem(held))
            {
                module = CartModule.Terrarium;
            }
            else
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

        /// <summary>Removes the cart and gives the player its skin item, module item and contents.</summary>
        public static void PickUp(Player player, Cart cart)
        {
            CartChestUISystem.NotifyCartRemoved(cart);

            if (cart.Chain != null)
            {
                cart.Chain.Remove(cart); // a middle carriage: the rest close the gap. The motor: the whole chain breaks up
            }
            CartCargo.PourTank(cart); // a terrarium cart empties back into the world instead of voiding its liquid
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

            if (Main.dedServ || Main.gameMenu || Main.netMode != NetmodeID.SinglePlayer)
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

        // The link art is vanilla chain texture 1 ("Chain" in the wiki's Chain IDs list, the grappling hook chain). Change the path to use another.
        private const string ChainTexturePath = "Images/Chain";
        private const float ChainHitchHeight = 15f; // px above the rail where the chain is hooked to a cart
        private static Asset<Texture2D> chainAsset;
        private static bool chainTextureFailed;

        private static Texture2D GetChainTexture()
        {
            if (chainAsset == null && !chainTextureFailed)
            {
                try
                {
                    chainAsset = Main.Assets.Request<Texture2D>(ChainTexturePath, AssetRequestMode.ImmediateLoad);
                }
                catch (Exception)
                {
                    chainTextureFailed = true; // fall back to a plain line
                }
            }

            return chainAsset != null ? chainAsset.Value : null;
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

            Texture2D texture = GetChainTexture();
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