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

        // Draws one arm segment by TILING the beam sprite along it (like a vanilla grappling-hook chain), never stretching it.
        // The art is a beam drawn at 45 degrees, bottom-left to top-right, whose copies repeat every 12 pixels along that diagonal.
        // Each copy is centred on its point along the arm, so the diagonal of the PNG must pass through the PNG's centre.
        // scale resizes the sprite and the spacing together (the callers used it as the beam thickness before).
        public static void DrawSegment(SpriteBatch spriteBatch, Texture2D texture, Vector2 start, Vector2 end, Color color, float scale = 0.7f)
        {
            Vector2 diff = end - start;
            float length = diff.Length();
            if (length < 0.01f) return;

            float spacing = 12f * scale;                    // distance between two copies, measured along the arm
            float artAngle = -MathHelper.PiOver4;           // the way the beam points inside the PNG: up and to the right
            float rotation = diff.ToRotation() - artAngle;  // turns that diagonal onto the arm
            Vector2 origin = texture.Size() / 2f;
            Vector2 direction = diff / length;
            Vector2 screenStart = start - Main.screenPosition;

            for (float travelled = 0f; travelled < length; travelled += spacing)
                spriteBatch.Draw(texture, screenStart + direction * travelled, null, color, rotation, origin, scale, SpriteEffects.None, 0f);

            // One closing copy exactly on the end point, so the arm never stops short of the joint or the claw.
            spriteBatch.Draw(texture, screenStart + diff, null, color, rotation, origin, scale, SpriteEffects.None, 0f);
        }
    }
}