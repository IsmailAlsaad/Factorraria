using System;

namespace Factorraria.Common.Cosmic
{
    // Pure code (no Terraria types), tested in tools/cosmic_tests. Roadmap section 2 "Strike" and assumptions A7 and A8.
    public static class CosmicStrikeMath
    {
        // Is the point (pointX, pointY) within the strike radius? limitSq is the squared radius, or the squared distance of the best
        // target found so far (so a closer machine wins). distanceSq is the squared distance whether or not the point was close enough.
        public static bool IsCloser(float centerX, float centerY, float pointX, float pointY, float limitSq, out float distanceSq)
        {
            float dx = pointX - centerX;
            float dy = pointY - centerY;
            distanceSq = dx * dx + dy * dy;
            return distanceSq <= limitSq;
        }

        // A8: surge power = thunder energy x power per energy. The energy is a double and can be astronomically large (A1), so the result
        // is clamped to maxPower before it becomes a float: the physics is never capped, only the number that reaches the grid.
        // A negative, NaN or zero result gives 0. Infinity (or anything above maxPower) gives maxPower, never infinity.
        public static float SurgePower(double energy, double powerPerEnergy, float maxPower, out bool clamped)
        {
            clamped = false;
            double power = energy * powerPerEnergy;

            if (maxPower <= 0f || double.IsNaN(power) || power <= 0.0)
                return 0f;

            if (power > maxPower)
            {
                clamped = true;
                return maxPower;
            }

            return (float)power;
        }

        // Two strikes on the same machine inside the same window stack: the powers add (never above maxPower) and the window restarts
        // at the longer of the time left and the new duration. A strike with no power or no duration changes nothing.
        public static void Stack(ref float power, ref int ticksLeft, float addPower, int durationTicks, float maxPower)
        {
            if (addPower <= 0f || durationTicks <= 0)
                return;

            float sum = power + addPower;
            power = float.IsInfinity(sum) || sum > maxPower ? maxPower : sum;
            ticksLeft = Math.Max(ticksLeft, durationTicks);
        }

        // One grid tick of a surge (PowerNetwork.Tick calls it once per machine per tick): returns the power that counts this tick and
        // counts the window down. The power is cleared on the last tick, so a surge counts for exactly its duration.
        public static float Tick(ref float power, ref int ticksLeft)
        {
            if (ticksLeft <= 0)
            {
                power = 0f;
                ticksLeft = 0;
                return 0f;
            }

            float counted = power;
            ticksLeft--;

            if (ticksLeft == 0)
                power = 0f;

            return counted;
        }
    }
}