using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace Factorraria.Common.ArmSpike
{
    // THROWAWAY SPIKE (Robotic Arm, Phase 0b). Sees every item spawned while a spike strike is running.
    // Answers: which IEntitySource do tile, wall and tree breaks report, and can OnSpawn take the drop out of the world cleanly?
    public class ArmSpikeDropLogger : GlobalItem
    {
        public override void OnSpawn(Item item, IEntitySource source)
        {
            ArmSpikeSystem system = ModContent.GetInstance<ArmSpikeSystem>();
            ArmSpikeSession s = system?.Session;
            if (s == null || !s.InStep)
                return;

            string text = $"{item.Name} x{item.stack} via {source?.GetType().Name}{Coords(source)}{(string.IsNullOrEmpty(source?.Context) ? "" : " (" + source.Context + ")")}";
            s.Drops.Add(text);

            if (s.Capture)
            {
                s.Swallowed.Add($"{item.Name} x{item.stack}");
                item.active = false;
                item.TurnToAir();
            }
        }

        // Tile-style sources carry the tile coordinates; read them by reflection so this file cannot break on a renamed member.
        static string Coords(IEntitySource source)
        {
            if (source == null)
                return "";

            try
            {
                Type type = source.GetType();
                object value = type.GetField("TileCoords")?.GetValue(source) ?? type.GetProperty("TileCoords")?.GetValue(source);
                return value == null ? "" : " @" + value;
            }
            catch (Exception)
            {
                return "";
            }
        }
    }
}