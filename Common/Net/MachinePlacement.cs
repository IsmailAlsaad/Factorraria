using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Factorraria.Common.Systems;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace Factorraria.Common.Net
{
    // Server-authoritative machine placement (roadmap patch 4.2, finding M1). Before this patch a machine's TileEntity was created
    // by the PLACING CLIENT (from PlaceInWorld, Hook_AfterPlacement or lazily from draw code), so the server had no machine and
    // every other client had a different local one. Now the entity is created where the simulation runs, and shared with clients
    // through vanilla's tile-entity sharing message (86), which also carries the entity id so both sides agree on it.
    //
    // Three routes, one helper (ServerPlace), all idempotent (a second call for the same machine changes nothing):
    //  - ModTile machines (motors, Mechanical Assembler, Robotic Arm): BaseMachine.Hook_AfterPlacement. On a multiplayer client it
    //    sends the tile square and the tile-entity placement request (87) and returns; the server's NetPlaceEntityAttempt places the
    //    entity, OnNetPlace finishes it and vanilla sends message 86. In single-player and in server code (the Robotic Arm calls the
    //    hook on the server) it calls ServerPlace directly.
    //  - Vanilla-tile machines (furnace, hellforge, ...): MachinePlacedConsumer, a tile-placed consumer (patch 1.3 / 3.3) that runs
    //    on the server and in single-player, also for placements made by server code such as the Robotic Arm (TileLoader.PlaceInWorld).
    //  - Nothing on a multiplayer client ever creates a machine entity any more (draw, light, right-click and kill code only look up).
    public static class MachinePlacement
    {
        // TEMPORARY test aid (patch 7b.6, remove in Phase 10). Set by /farmsend placereject in the server console: the next machine entity
        // request (message 87) is refused by BaseMachine.NetPlaceEntityAttempt, as if the server had said no.
        public static bool RejectNextEntityRequest;

        /// <summary>
        /// Server or single-player only (returns null on a multiplayer client or for a tile that is not a registered machine).
        /// x, y is any tile of the machine. Creates the entity, registers it with the power grid and liquid network, runs OnPlaced and
        /// shares it with the clients. If the machine already has an entity it is returned untouched.
        /// </summary>
        public static BaseMachine ServerPlace(int tileType, int x, int y)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return null;

            if (!MachineVisualRegistry.Definitions.TryGetValue(tileType, out MachineVisualDefinition def))
                return null;

            TileEntityHelper.TryGetEntityFromTile(x, y, out TileEntity existing, out Point16 corner);
            if (existing is BaseMachine already)
                return already;

            BaseMachine machine = def.GetEntity(corner.X, corner.Y);   // Place + power grid registration + liquid rebuild flag
            if (machine == null)
                return null;

            machine.OnPlaced(corner.X, corner.Y);
            Share(machine);

            if (Main.netMode == NetmodeID.Server)
                FactorrariaNet.Log("[Machines] placed " + machine.GetType().Name + " at (" + corner.X + ", " + corner.Y + "), id " + machine.ID);

            return machine;
        }

        /// <summary>
        /// Patch 4.5. The entity of the machine tile at x, y (any tile of the machine). Server and single-player create it when the tile
        /// has none (power grid and liquid registration through GetEntity, geometry, and a share with the clients); a multiplayer client
        /// only looks up. This is not a placement: OnPlaced does not run, so a bootstrapped machine keeps biome Unknown. Used by the
        /// bootstrap scan (MachineBootstrapSystem) and by right-click; Phase 6.1's open-machine request should call it too.
        /// </summary>
        public static BaseMachine EnsureEntity(int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 1))
                return null;

            Tile tile = Main.tile[x, y];
            if (!tile.HasTile || !MachineVisualRegistry.Definitions.TryGetValue(tile.TileType, out MachineVisualDefinition def))
                return null;

            if (TileEntityHelper.TryGetEntityFromTile(x, y, out TileEntity existing, out Point16 corner))
                return existing as BaseMachine;

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return null;

            // The corner comes from the tile frame; only trust it when the tile has object data and the corner tile is the same machine tile.
            if (TileObjectData.GetTileData(tile.TileType, 0) == null || !WorldGen.InWorld(corner.X, corner.Y, 1))
                return null;

            Tile cornerTile = Main.tile[corner.X, corner.Y];
            if (!cornerTile.HasTile || cornerTile.TileType != tile.TileType)
                return null;

            BaseMachine machine = def.GetEntity(corner.X, corner.Y);   // Place + power grid registration + liquid rebuild flag
            if (machine == null)
                return null;

            machine.InitializeGeometry();
            Share(machine);

            if (Main.netMode == NetmodeID.Server)
                FactorrariaNet.Log("[Machines] bootstrapped " + machine.GetType().Name + " at (" + corner.X + ", " + corner.Y + "), id " + machine.ID);

            return machine;
        }

        /// <summary>Server only: sends the entity (id and position) to every client with vanilla's tile-entity sharing message.</summary>
        public static void Share(BaseMachine machine)
        {
            if (Main.netMode != NetmodeID.Server || machine == null)
                return;

            NetMessage.SendData(MessageID.TileEntitySharing, -1, -1, null, machine.ID, machine.Position.X, machine.Position.Y);
        }

        /// <summary>Power grid and liquid network registration, shared by the routes that do not go through TileEntityHelper.GetOrCreateEntity.</summary>
        public static void Register(BaseMachine machine)
        {
            PowerGridSystem.RegisterMachineToMasterList(machine);
            LiquidNetworkSystem.networkNeedsRebuilding = true;
        }
    }

    // Vanilla-tile machines only. ModTile machines are created by Hook_AfterPlacement (see above), so they are skipped here and
    // cannot be created twice.
    public sealed class MachinePlacedConsumer : ITilePlacedConsumer
    {
        public bool AppliesTo(int tileType) => tileType < TileID.Count && MachineVisualRegistry.Definitions.ContainsKey(tileType);

        public void OnTilePlaced(int x, int y, int tileType, int placer)
        {
            MachinePlacement.ServerPlace(tileType, x, y);
        }
    }
}