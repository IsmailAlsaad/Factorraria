using Terraria;
using Terraria.ID;

namespace Factorraria.Content.Projectiles.Weapons.IceBoomerang
{
    /// <summary>
    /// Every number of the Ice Boomerang in one place. They are expression-bodied properties on purpose: hot reload can
    /// change a method body but not a const or static field, so these can be tweaked while the game runs.
    /// </summary>
    public static class IceBoomerangTuning
    {
        // --- Throw ---
        public static float ThrowSpeed => 16f;        // pixels per tick
        public static float MinThrowRange => 20f * 16f;     // a cursor right on the player still throws a little
        public static float MaxThrowRange => MinThrowRange;   // far beyond the screen; the cursor is the real limit
        public static int SettleTicks => 18;          // spin-down before it turns into a crystal

        // --- Gradual stop at the cursor ---
        public static float BrakeDistance => 160f;    // it starts slowing down this far before the cursor
        public static float BrakeMinSpeed => 2f;      // crawl speed right before it settles

        // --- Chain ---
        public static float ChainSpeedGainPerLevel => 0.25f; // +25% of the throw speed per crystal bounced off
        public static float ChainMaxSpeed => 44f;
        public static float DamageGainPerLevel => 0.35f;     // +35% of the boomerang's damage per crystal bounced off
        public static float BlastRadius => 90f;              // pixels
        public static float BlastDamageMultiplier => 0.8f;   // of the boomerang's current damage
        public static int BlastLifeTicks => 6;
        public static int BlastFlashTicks => 12;             // crystal flash when it explodes
        public static int FlightMaxTicks => 1800;            // safety net for a chain/return (30 s)

        // --- Crystals ---
        public static int MaxCrystals => 5;           // the 6th evicts the oldest
        public static int CrystalLifeTicks => 1800;   // 30 seconds
        public static int CrystalPopTicks => 14;      // grow-in animation
        public static int ExpiryWarnTicks => 180;     // flicker during the last 3 seconds
        public static int CrystalSheetFrames => 3;    // frames stacked vertically in the vanilla snowflake sheet (one is picked per crystal)
        public static int CrystalBounceLifeLossTicks => 900; // every bounce off a crystal costs it 15 seconds of life

        // --- Crystal halo: copies of the crystal's silhouette that expand and fade out, a new one every HaloIntervalTicks ---
        public static int HaloIntervalTicks => 36;
        public static int HaloLifeTicks => 60;
        public static float HaloMaxScale => 2f;       // size at the end of its life, relative to the crystal
        public static float HaloAlpha => 0.4f;        // opacity when it is born

        // --- Chill (crystal touch; later also crystal explosions) ---
        public static int ChillTicks => 240;
        public static int ChillRefreshBelowTicks => 90;
        public static int ChillCheckInterval => 6;
    }

    public static class IceBoomerangChill
    {
        // Runs on the crystal owner's client. Vanilla NPC.AddBuff already tells the server when called on a client.
        public static void Apply(NPC npc)
        {
            int index = npc.FindBuffIndex(BuffID.Chilled);
            if (index >= 0 && npc.buffTime[index] > IceBoomerangTuning.ChillRefreshBelowTicks)
                return;
            npc.AddBuff(BuffID.Chilled, IceBoomerangTuning.ChillTicks);
        }
    }
}