using Factorraria.Content.Items.Carts;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Carts
{
    /// <summary>
    /// Maps a minecart item (the skin) to how a placed cart looks. A skin is any item whose mount is a minecart
    /// (MountID.Sets.Cart), so every vanilla cart works and so do carts from other mods.
    /// A placed cart draws the same sprite the player sees when riding it: the mount's own texture (MountData front/back
    /// texture), cut into the mount's animation frames (MountData.totalFrames). A skin whose mount texture is not
    /// available falls back to its item icon.
    /// </summary>
    public static class CartSkinTable
    {
        /// <summary>Scale of the item-icon fallback (only used when a skin has no mount texture).</summary>
        public const float FallbackIconScale = 1.05f;

        /// <summary>Pixels the wheels sink into the rail. Raise it if the cart floats above the track, lower (or go negative) if it sinks.</summary>
        public const float RailSink = 0f;

        /// <summary>The item the player is "holding" for cart interactions: the item on the cursor if there is one, else the selected hotbar item.</summary>
        public static Item GetHeldItem(Player player)
        {
            if (Main.mouseItem != null && !Main.mouseItem.IsAir)
            {
                return Main.mouseItem;
            }

            return player.HeldItem;
        }

        /// <summary>True for vanilla/modded minecart items. Using one places a cart instead of mounting.</summary>
        public static bool IsSkinItem(Item item)
        {
            return item != null && !item.IsAir && item.mountType > MountID.Rudolph && MountID.Sets.Cart[item.mountType];
        }

        /// <summary>True for anything that places a cart: skin items and the legacy Track Cart item.</summary>
        public static bool IsPlaceable(Item item)
        {
            return item != null && !item.IsAir && (IsSkinItem(item) || item.type == ModContent.ItemType<CartItem>());
        }

        /// <summary>Item type whose skin a placeable item would produce.</summary>
        public static int SkinTypeFor(Item item)
        {
            return item.type == ModContent.ItemType<CartItem>() ? ItemID.Minecart : item.type;
        }

        /// <summary>Everything needed to draw one frame of a placed cart.</summary>
        public struct PlacedSprite
        {
            public Texture2D Texture;
            public Rectangle Source;   // the frame inside the sheet
            public Vector2 Origin;     // the rail point (bottom centre of the wheels) inside Source
            public float Scale;
            public Texture2D Extra;    // optional second layer (the mount's "extra" texture), same Source
        }

        /// <summary>Which frames of the mount sheet are used while standing, running and airborne.</summary>
        public struct SkinAnimation
        {
            public int TotalFrames;
            public int StandFrame;
            public int RunStart;
            public int RunCount;
            public int AirFrame;
        }

        private static readonly Dictionary<int, SkinAnimation> animations = new Dictionary<int, SkinAnimation>();
        private static readonly Dictionary<int, Vector2> origins = new Dictionary<int, Vector2>();

        /// <summary>The mount a skin item would summon, or null if the item has none.</summary>
        public static Mount.MountData GetMountData(int skinItemType)
        {
            if (skinItemType <= 0 || skinItemType >= ItemLoader.ItemCount)
            {
                return null;
            }

            int mountType = ContentSamples.ItemsByType[skinItemType].mountType;
            if (mountType < 0 || Mount.mounts == null || mountType >= Mount.mounts.Length)
            {
                return null;
            }

            return Mount.mounts[mountType];
        }

        /// <summary>Frame ranges for a skin, read from the mount's own standing / running / in-air frame settings.</summary>
        public static SkinAnimation GetAnimation(int skinItemType)
        {
            SkinAnimation result;
            if (animations.TryGetValue(skinItemType, out result))
            {
                return result;
            }

            result = new SkinAnimation { TotalFrames = 1, StandFrame = 0, RunStart = 0, RunCount = 1, AirFrame = 0 };

            Mount.MountData data = GetMountData(skinItemType);
            if (data != null && data.totalFrames > 0)
            {
                int total = data.totalFrames;
                result.TotalFrames = total;
                result.StandFrame = ClampFrame(data.standingFrameStart, total);
                result.RunStart = ClampFrame(data.runningFrameStart, total);
                result.RunCount = Math.Max(1, Math.Min(data.runningFrameCount, total - result.RunStart));
                result.AirFrame = ClampFrame(data.inAirFrameStart, total);
            }

            animations[skinItemType] = result;
            return result;
        }

        /// <summary>The sprite for one animation frame of a skin: the mount texture if it is loaded, else the item icon.</summary>
        public static PlacedSprite GetPlacedSprite(int skinItemType, int frame)
        {
            PlacedSprite sprite = new PlacedSprite();
            sprite.Scale = 1f;

            Mount.MountData data = GetMountData(skinItemType);
            if (data != null)
            {
                Texture2D body = Loaded(data.frontTexture) ?? Loaded(data.backTexture);
                if (body != null)
                {
                    int frames = Math.Max(1, data.totalFrames);
                    int frameHeight = body.Height / frames;

                    sprite.Texture = body;
                    sprite.Source = new Rectangle(0, frameHeight * ClampFrame(frame, frames), body.Width, frameHeight);
                    sprite.Origin = GetOrigin(skinItemType, body, frameHeight);

                    Texture2D extra = Loaded(data.frontTextureExtra) ?? Loaded(data.backTextureExtra);
                    if (extra != null && extra.Width == body.Width && extra.Height == body.Height)
                    {
                        sprite.Extra = extra;
                    }

                    return sprite;
                }
            }

            Main.instance.LoadItem(skinItemType);
            Texture2D icon = TextureAssets.Item[skinItemType].Value;
            sprite.Texture = icon;
            sprite.Source = new Rectangle(0, 0, icon.Width, icon.Height);
            sprite.Origin = new Vector2(icon.Width / 2f, icon.Height - 2f);
            sprite.Scale = FallbackIconScale;
            return sprite;
        }

        private static int ClampFrame(int frame, int total)
        {
            return Math.Max(0, Math.Min(frame, total - 1));
        }

        private static Texture2D Loaded(Asset<Texture2D> asset)
        {
            if (asset == null || asset == Asset<Texture2D>.Empty || !asset.IsLoaded)
            {
                return null;
            }

            Texture2D texture = asset.Value;
            if (texture == null || texture.IsDisposed || texture == Asset<Texture2D>.DefaultValue)
            {
                return null;
            }

            return texture;
        }

        /// <summary>
        /// Rail point inside a frame: horizontally centred, vertically the lowest opaque pixel of the first frame (the bottom of
        /// the wheels), so the cart sits on the rail whatever empty margin the mount sheet has. Cached per skin.
        /// </summary>
        private static Vector2 GetOrigin(int skinItemType, Texture2D texture, int frameHeight)
        {
            Vector2 origin;
            if (origins.TryGetValue(skinItemType, out origin))
            {
                return origin;
            }

            origin = new Vector2(texture.Width / 2f, frameHeight);

            try
            {
                Color[] pixels = new Color[texture.Width * frameHeight];
                texture.GetData(0, new Rectangle(0, 0, texture.Width, frameHeight), pixels, 0, pixels.Length);

                for (int y = frameHeight - 1; y >= 0; y--)
                {
                    bool found = false;
                    for (int x = 0; x < texture.Width && !found; x++)
                    {
                        found = pixels[y * texture.Width + x].A > 16;
                    }

                    if (found)
                    {
                        origin.Y = y + 1 - RailSink;
                        break;
                    }
                }
            }
            catch (Exception)
            {
                // Texture not readable: keep the bottom edge of the frame.
            }

            origins[skinItemType] = origin;
            return origin;
        }
    }
}