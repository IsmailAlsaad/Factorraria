using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace Factorraria.Common.Mounts
{
    // Shared two-bone (elbow) inverse-kinematics solve, used by every mechanical-arm-style
    // visual (the handheld Mechanical Arm hook, and each Mech Suit arm). One implementation
    // so both stay visually identical and any tuning only has to happen in one place.
    public static class MechanicalArmIK
    {
        public static Vector2 SolveElbow(Vector2 shoulder, Vector2 target, float l1, float l2, float bendSign, float fallbackAngle)
        {
            Vector2 delta = target - shoulder;
            float d = delta.Length();

            if (d < 1f)
            {
                // Not enough distance for the triangle to mean anything yet — use the
                // cocked angle locked in at spawn instead.
                return shoulder + fallbackAngle.ToRotationVector2() * l1;
            }

            float clampedD = MathHelper.Clamp(d, MathF.Abs(l1 - l2) + 0.01f, l1 + l2 - 0.01f);

            float baseAngle = delta.ToRotation();
            float cosAngle = (l1 * l1 + clampedD * clampedD - l2 * l2) / (2f * l1 * clampedD);
            cosAngle = MathHelper.Clamp(cosAngle, -1f, 1f);
            float offsetAngle = MathF.Acos(cosAngle) * bendSign;

            float elbowAngle = baseAngle + offsetAngle;
            return shoulder + elbowAngle.ToRotationVector2() * l1;
        }

        public static void DrawSegment(SpriteBatch spriteBatch, Texture2D texture, Vector2 start, Vector2 end, Color color)
        {
            Vector2 diff = end - start;
            float length = diff.Length();
            if (length < 0.01f) return;

            float rotation = diff.ToRotation();
            Vector2 origin = new Vector2(0f, texture.Height / 2f);
            Vector2 scale = new Vector2(length / texture.Width, 0.7f);

            spriteBatch.Draw(texture, start - Main.screenPosition, null, color, rotation, origin, scale, SpriteEffects.None, 0f);
        }
    }
}