using System;
using System.Collections.Generic;

namespace Factorraria.Common.SuperWeapons
{
    // Super weapons patch S4.1: the pure part of the launch trigger. NO Terraria types, so it is tested outside the game (tools/sw_tests).
    // SuperWeaponSystem feeds it the primed weapons it found and the shielded wall's x.

    public struct TriggerCandidate
    {
        public int Id;       // any stable id (the tank's TileEntity id); breaks exact ties
        public float X;      // the weapon's centre x, in pixels
        public int Facing;   // +1 right, -1 left

        public TriggerCandidate(int id, float x, int facing)
        {
            Id = id;
            X = x;
            Facing = facing;
        }
    }

    public static class SuperWeaponTrigger
    {
        /// <summary>
        /// Which weapon fires at a wall whose x is wallX (roadmap D2): among the candidates whose facing points toward the wall
        /// (sign(wallX - X) == Facing) the one nearest in x. Returns its index in the list, or -1 for none. busy = another weapon is
        /// already igniting or launched for this wall (only one fires per wall), so nothing is picked. A wall exactly at the weapon's x
        /// points no way, so it matches nobody. An exact tie goes to the lower Id.
        /// </summary>
        public static int Pick(float wallX, IList<TriggerCandidate> candidates, bool busy)
        {
            if (busy || candidates == null)
                return -1;

            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                TriggerCandidate c = candidates[i];
                float dx = wallX - c.X;
                if (c.Facing == 0 || dx == 0f)
                    continue;

                if ((dx > 0f ? 1 : -1) != c.Facing)
                    continue;

                float distance = Math.Abs(dx);
                if (distance < bestDistance || (distance == bestDistance && c.Id < candidates[best].Id))
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            return best;
        }
    }
}