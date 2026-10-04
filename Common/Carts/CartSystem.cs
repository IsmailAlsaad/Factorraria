using Factorraria.Content.Configs;
using Factorraria.Content.Items.Carts;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
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
            railPoint = Vector2.Zero;

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

            Vector2 point = new Vector2(x, sample.SurfaceY);

            if (Vector2.Distance(player.Center, point) > PlaceRange)
            {
                return false;
            }

            for (int i = 0; i < Carts.Count; i++)
            {
                Vector2 other = Carts[i].Position;
                if (Math.Abs(other.X - point.X) < CartPhysics.HitboxWidth && Math.Abs(other.Y - point.Y) < CartPhysics.HitboxHeight)
                {
                    return false; // already a cart here
                }
            }

            railPoint = point;
            return true;
        }

        public static Cart SpawnCart(Vector2 railPoint, CartKind kind = CartKind.Empty)
        {
            Cart cart = new Cart();
            cart.Kind = kind;
            cart.Position = railPoint;
            cart.OnTrack = true;
            Carts.Add(cart);
            return cart;
        }

        #endregion

        #region Interaction (right click on a cart)

        public override void PostUpdatePlayers()
        {
            if (Main.dedServ || Main.gameMenu || Main.netMode != NetmodeID.SinglePlayer || Carts.Count == 0)
            {
                return;
            }

            Player player = Main.LocalPlayer;

            if (!Main.mouseRight || !Main.mouseRightRelease || player.dead || player.mouseInterface || player.lastMouseInterface || Main.playerInventory)
            {
                return;
            }

            Vector2 mouse = GetZoomCorrectedMouseWorld();
            Cart target = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < Carts.Count; i++)
            {
                Cart cart = Carts[i];
                Rectangle box = cart.Hitbox;
                box = new Rectangle(box.X - 4, box.Y - 4, box.Width + 8, box.Height + 8);

                if (!box.Contains(mouse.ToPoint()))
                {
                    continue;
                }

                float distance = Vector2.Distance(player.Center, cart.Position);
                if (distance <= InteractRange && distance < bestDistance)
                {
                    bestDistance = distance;
                    target = cart;
                }
            }

            if (target == null)
            {
                return;
            }

            Main.mouseRightRelease = false; // we used this click

            if (player.controlDown)
            {
                // Crouch + right click: pick the cart back up
                target.Active = false;
                player.QuickSpawnItem(player.GetSource_Misc("FactorrariaCartPickup"), ModContent.ItemType<CartItem>());
            }
            else
            {
                // Right click: shove it away from the player. Temporary test hook until motor carts exist.
                float away = target.Position.X >= player.Center.X ? 1f : -1f;
                target.Shove(away * CartPhysics.ShoveSpeed);
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
            if (Carts.Count == 0)
            {
                return;
            }

            // Placeholder art: the vanilla Minecart item icon, rotated to follow the rail.
            Main.instance.LoadItem(ItemID.Minecart);
            Texture2D texture = TextureAssets.Item[ItemID.Minecart].Value;
            Vector2 origin = new Vector2(texture.Width / 2f, texture.Height - 2f);

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

                Color light = Lighting.GetColor((int)(cart.Position.X / 16f), (int)((cart.Position.Y - 8f) / 16f));
                Vector2 screen = cart.Position - Main.screenPosition;

                Main.spriteBatch.Draw(texture, screen, null, light, cart.Rotation, origin, 3f, SpriteEffects.None, 0f);

                if (debug)
                {
                    DrawDebug(cart, screen);
                }
            }

            Main.spriteBatch.End();
        }

        private static void DrawDebug(Cart cart, Vector2 screen)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 axis = new Vector2((float)Math.Cos(cart.Rotation), (float)Math.Sin(cart.Rotation)) * CartPhysics.WheelHalfBase;

            DrawDot(pixel, screen - axis, Color.Cyan);   // left wheel probe
            DrawDot(pixel, screen + axis, Color.Orange); // right wheel probe
            DrawDot(pixel, screen, Color.Lime);          // cart centre on the rail
        }

        private static void DrawDot(Texture2D pixel, Vector2 position, Color color)
        {
            Main.spriteBatch.Draw(pixel, new Rectangle((int)position.X - 2, (int)position.Y - 2, 4, 4), color);
        }

        #endregion
    }
}