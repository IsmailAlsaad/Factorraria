using Factorraria.Content.Configs;
using Factorraria.Content.Items.Carts;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
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

        #region Lifecycle / saving

        public override void Load()
        {
            Cart.Bumped += OnCartBumped;
        }

        public override void Unload()
        {
            Cart.Bumped -= OnCartBumped;
        }

        /// <summary>Placeholder bumper "boing". Swap the sound here once there is a real one.</summary>
        private static void OnCartBumped(Cart cart)
        {
            SoundEngine.PlaySound(SoundID.Item56, cart.Position);
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

            for (int i = Carts.Count - 1; i >= 0; i--)
            {
                Cart cart = Carts[i];
                cart.Update();

                if (!cart.Active)
                {
                    Carts.RemoveAt(i);
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
        /// Right click on a cart with an empty hand = pick it up. (Fuel and chest-open come with their phases.)
        /// A cart under the cursor beats other uses of the click unless a cart item is held.
        /// </summary>
        public override void PostUpdatePlayers()
        {
            if (Main.dedServ || Main.gameMenu || Main.netMode != NetmodeID.SinglePlayer)
            {
                return;
            }

            Player player = Main.LocalPlayer;

            if (player.dead || player.mouseInterface || player.lastMouseInterface || Main.playerInventory)
            {
                return;
            }

            bool left = Main.mouseLeft && Main.mouseLeftRelease;
            bool right = Main.mouseRight && Main.mouseRightRelease;

            if (!left && !right)
            {
                return;
            }

            Item held = player.HeldItem;

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

            if (right && (held == null || held.IsAir))
            {
                Cart target = FindCartAtCursor(player);
                if (target == null)
                {
                    return;
                }

                Main.mouseRightRelease = false;
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

        /// <summary>Installs the held item as a module if it is one. Only the Chest exists so far (Terrarium and Motor come later).</summary>
        private static bool TryInstallModule(Item held, Cart cart)
        {
            if (held == null || held.IsAir || cart.Module != CartModule.Empty)
            {
                return false;
            }

            if (held.type != Cart.ModuleItemType(CartModule.Chest))
            {
                return false;
            }

            cart.SetModule(CartModule.Chest);

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
                Cart cart = Carts[i];

                if (cart.Position.X < minX || cart.Position.X > maxX || cart.Position.Y < minY || cart.Position.Y > maxY)
                {
                    continue;
                }

                Vector2 origin;
                float scale;
                Texture2D texture = CartSkinTable.GetPlacedTexture(cart.SkinType, out origin, out scale);

                Color light = Lighting.GetColor((int)(cart.Position.X / 16f), (int)((cart.Position.Y - 8f) / 16f));
                Vector2 screen = cart.Position - Main.screenPosition;

                SpriteEffects effects = cart.Facing < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                Main.spriteBatch.Draw(texture, screen, null, light, cart.Rotation, origin, scale, effects, 0f);

                if (debug)
                {
                    DrawDebug(cart, screen);
                }
            }

            if (hologram)
            {
                Vector2 origin;
                float scale;
                Texture2D texture = CartSkinTable.GetPlacedTexture(holoSkin, out origin, out scale);
                Color tint = holoValid ? Color.White * 0.55f : new Color(255, 70, 70) * 0.6f;
                SpriteEffects effects = Main.LocalPlayer.direction < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

                Main.spriteBatch.Draw(texture, holoPoint - Main.screenPosition, null, tint, 0f, origin, scale, effects, 0f);
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
            Item held = player.HeldItem;

            if (player.dead || Main.playerInventory || player.mouseInterface || !CartSkinTable.IsPlaceable(held))
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

        private static void DrawDebug(Cart cart, Vector2 screen)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 axis = new Vector2((float)Math.Cos(cart.Rotation), (float)Math.Sin(cart.Rotation)) * CartPhysics.WheelHalfBase;

            DrawDot(pixel, screen - axis, Color.Cyan);   // left wheel probe
            DrawDot(pixel, screen + axis, Color.Orange); // right wheel probe
            DrawDot(pixel, screen, Color.Lime);          // cart centre on the rail
            DrawHitbox(cart, pixel);                      // rotated hitbox (white)
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
            Main.spriteBatch.Draw(pixel, from, null, color, rotation, Vector2.Zero, new Vector2(edge.Length(), 1.5f), SpriteEffects.None, 0f);
        }

        #endregion
    }
}