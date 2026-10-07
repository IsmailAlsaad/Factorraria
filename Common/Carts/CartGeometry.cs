using Microsoft.Xna.Framework;
using System;

namespace Factorraria.Common.Carts
{
    /// <summary>
    /// The cart's real hitbox is the HitboxWidth x HitboxHeight box standing on the rail point (Position) and tilted
    /// with the cart. It rotates about the feet, exactly like the sprite (whose origin is bottom-centre), so drawing,
    /// click hit-tests and later pickup scans all agree. Cart.Hitbox stays the axis-aligned box used for rough checks.
    /// </summary>
    public static class CartGeometry
    {
        /// <summary>Unit vector pointing along the cart's floor (to its right).</summary>
        public static Vector2 Right(Cart cart)
        {
            return new Vector2((float)Math.Cos(cart.Rotation), (float)Math.Sin(cart.Rotation));
        }

        /// <summary>Unit vector pointing up out of the cart's floor.</summary>
        public static Vector2 Up(Cart cart)
        {
            return new Vector2((float)Math.Sin(cart.Rotation), -(float)Math.Cos(cart.Rotation));
        }

        public static Vector2 BoxCenter(Cart cart)
        {
            return cart.Position + Up(cart) * (CartPhysics.HitboxHeight * 0.5f);
        }

        /// <summary>Fills 4 world-space corners: top-left, top-right, bottom-right, bottom-left (in the cart's own frame).</summary>
        public static void GetCorners(Cart cart, Vector2[] corners)
        {
            Vector2 center = BoxCenter(cart);
            Vector2 right = Right(cart) * (CartPhysics.HitboxWidth * 0.5f);
            Vector2 up = Up(cart) * (CartPhysics.HitboxHeight * 0.5f);

            corners[0] = center - right + up;
            corners[1] = center + right + up;
            corners[2] = center + right - up;
            corners[3] = center - right - up;
        }

        /// <summary>Point-in-rotated-rectangle test, with the box grown by padding pixels on every side.</summary>
        public static bool Contains(Cart cart, Vector2 worldPoint, float padding)
        {
            Vector2 offset = worldPoint - BoxCenter(cart);
            float along = Vector2.Dot(offset, Right(cart));
            float height = Vector2.Dot(offset, Up(cart));

            return Math.Abs(along) <= CartPhysics.HitboxWidth * 0.5f + padding
                && Math.Abs(height) <= CartPhysics.HitboxHeight * 0.5f + padding;
        }
    }
}