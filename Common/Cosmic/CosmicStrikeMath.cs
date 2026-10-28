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

        // A8 (changed in C6b): surge power = powerPerDecade x log10(thunder energy). The energy is a double and can be astronomically large (A1),
        // but its logarithm never is (about 308 at most for a finite double), so the number that reaches the grid stays small and readable.
        // The physics is never capped, only this number. Energy at or below 1 (zero decades), NaN or a negative gives 0. Infinity, or anything
        // above maxPower, gives maxPower, never infinity.
        public static float SurgePower(double energy, double powerPerDecade, float maxPower, out bool clamped)
        {
            clamped = false;

            if (maxPower <= 0f || double.IsNaN(energy) || energy <= 1.0)
                return 0f;

            double power = Math.Log10(energy) * powerPerDecade;   // log10(+infinity) is +infinity, and infinity x 0 is NaN (gives 0 below)

            if (double.IsNaN(power) || power <= 0.0)
                return 0f;

            if (power > maxPower)
            {
                clamped = true;
                return maxPower;
            }

            return (float)power;
        }

        // Lightning hitting an enemy: damage = the thunder's energy, rounded down, at least 1 (NaN, negative and below 1 give 1) and never above
        // int.MaxValue (NPC damage is an int; the energy itself is a double and stays uncapped everywhere else).
        public static int EnergyDamage(double energy)
        {
            if (double.IsNaN(energy) || energy < 1.0)
                return 1;

            if (energy >= int.MaxValue)
                return int.MaxValue;

            return (int)energy;
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