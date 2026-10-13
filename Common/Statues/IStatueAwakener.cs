using Terraria.DataStructures;

namespace Factorraria.Common.Statues
{
    // Implemented by any machine that awakens nearby statues (the Magic Materializer today).
    // AwakenedStatueSystem reads this; the machine never needs to know how the statues are tracked.
    public interface IStatueAwakener
    {
        bool IsAwakening { get; }
        Point16 AwakenerOrigin { get; }
        int AwakenRadiusTiles { get; }

        // Written by the statue systems, read by the machine UI.
        int AwakenedStatueCount { get; set; }
        int AwakenedEnemyCount { get; set; }
    }
}