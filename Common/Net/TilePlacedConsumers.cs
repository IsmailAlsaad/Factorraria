using Factorraria.Common.Globals;
using Factorraria.Common.Liquids;
using Factorraria.Common.Statues;
using Factorraria.Common.Systems;
using Factorraria.Content.Tiles.Conveyors;
using Factorraria.Content.VirtualItems;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Liquids
{
    /// <summary>Marker for the motor tiles (MotorTileBase is generic, so a consumer cannot test "is a MotorTileBase").</summary>
    public interface IMotorTile
    {
    }
}

namespace Factorraria.Common.Net
{
    // Roadmap patch 3.3 (finding G0): the placement logic that used to sit in client-only PlaceInWorld hooks, now as tile-placed
    // consumers. TilePlaced runs them where the simulation runs (single-player, the server) when the placing client's request is
    // accepted or when server code (the Robotic Arm) places a tile. Each one is idempotent.
    //   chest conveyors     -> probe the chest (ChestConveyorSystem queues it for the next tick)
    //   liquid topology     -> any tile next to a pipe flags a network rebuild
    //   awakened statues    -> queue the statue check
    //   pipes               -> register the pipe with the liquid network (also fixes the pipe list that is saved with the world, P1)
    //   motors              -> liquid + power rebuild flags
    //   priority conveyors  -> build their conveyor network
    // Machine placement is NOT here; it moves in Phase 4.
    public class TilePlacedConsumersSystem : ModSystem
    {
        /// <summary>TEMPORARY test aid: logs one line on the server when a consumer with a visible effect ran. Remove in Phase 10.</summary>
        public static bool LogConsumers = true;

        public override void Load()
        {
            TilePlaced.Register(new ChestConveyorPlacedConsumer());
            TilePlaced.Register(new LiquidTopologyPlacedConsumer());
            TilePlaced.Register(new AwakenedStatuePlacedConsumer());
            TilePlaced.Register(new PipePlacedConsumer());
            TilePlaced.Register(new MotorPlacedConsumer());
            TilePlaced.Register(new PriorityConveyorPlacedConsumer());
        }

        internal static void Log(string what, int x, int y, int placer)
        {
            if (!LogConsumers || Main.netMode != NetmodeID.Server)
                return;

            string who = placer >= 0 && placer < Main.maxPlayers ? Main.player[placer].name + " (" + placer + ")" : "server";
            FactorrariaNet.Log("[3.3] " + what + " at (" + x + ", " + y + ") placed by " + who);
        }
    }

    sealed class ChestConveyorPlacedConsumer : ITilePlacedConsumer
    {
        public bool AppliesTo(int tileType) => true;

        public void OnTilePlaced(int x, int y, int tileType, int placer)
        {
            // A chest joins the registry if a conveyor is under it; a conveyor looks at the tile above; any other tile re-checks a
            // registered chest standing on it (it may have lost its conveyor). Nothing is decided here: the system probes next tick.
            if (TileID.Sets.BasicChest[tileType])
            {
                ChestConveyorSystem.QueueProbe(x, y, true);
                TilePlacedConsumersSystem.Log("chest conveyor probe queued (chest)", x, y, placer);
                return;
            }

            if (VirtualItemSystem.IsConveyorTile(x, y, out _, out _))
            {
                ChestConveyorSystem.QueueProbe(x, y - 1, true);
                TilePlacedConsumersSystem.Log("chest conveyor probe queued (conveyor under a possible chest)", x, y, placer);
                return;
            }

            if (ChestConveyorSystem.HasListedChestAbove(x, y))
                ChestConveyorSystem.QueueProbe(x, y - 1, false);
        }
    }

    sealed class LiquidTopologyPlacedConsumer : ITilePlacedConsumer
    {
        public bool AppliesTo(int tileType) => true;

        public void OnTilePlaced(int x, int y, int tileType, int placer)
        {
            LiquidTopologyGlobalTile.MarkIfNextToPipe(x, y);
        }
    }

    sealed class AwakenedStatuePlacedConsumer : ITilePlacedConsumer
    {
        public bool AppliesTo(int tileType) => ModContent.GetInstance<AwakenedStatueRegistry>().IsStatueTile(tileType);

        public void OnTilePlaced(int x, int y, int tileType, int placer)
        {
            ModContent.GetInstance<AwakenedStatueSystem>().QueueStatueCheck(x, y);
            TilePlacedConsumersSystem.Log("awakened statue check queued", x, y, placer);
        }
    }

    sealed class PipePlacedConsumer : ITilePlacedConsumer
    {
        public bool AppliesTo(int tileType) => PipeTierRegistry.IsPipeTile(tileType);

        public void OnTilePlaced(int x, int y, int tileType, int placer)
        {
            LiquidNetworkSystem.RegisterPipeTile(new Point(x, y));
            TilePlacedConsumersSystem.Log("pipe registered (" + LiquidNetworkSystem.AllPipeTiles.Count + " pipe tiles)", x, y, placer);
        }
    }

    sealed class MotorPlacedConsumer : ITilePlacedConsumer
    {
        public bool AppliesTo(int tileType) => TileLoader.GetTile(tileType) is IMotorTile;

        public void OnTilePlaced(int x, int y, int tileType, int placer)
        {
            // A motor is a bridge point in the liquid network and a power consumer, so it invalidates both.
            LiquidNetworkSystem.networkNeedsRebuilding = true;
            PowerGridSystem.gridNeedsRebuilding = true;
            TilePlacedConsumersSystem.Log("motor rebuild flags set", x, y, placer);
        }
    }

    sealed class PriorityConveyorPlacedConsumer : ITilePlacedConsumer
    {
        public bool AppliesTo(int tileType) =>
            tileType == ModContent.TileType<ClockwisePriorityConveyorTile>() || tileType == ModContent.TileType<CounterClockwisePriorityConveyorTile>();

        public void OnTilePlaced(int x, int y, int tileType, int placer)
        {
            VirtualItemSystem.OnPriorityConveyorPlaced(x, y);
            TilePlacedConsumersSystem.Log("priority conveyor network built", x, y, placer);
        }
    }
}