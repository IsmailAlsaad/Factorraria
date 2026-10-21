using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Factorraria.Common.Systems;
using System.Collections.Concurrent;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // Motor rotation (roadmap patch 8.3, finding P3).
    //
    // Authority: the server (or single-player) owns MotorTileEntityBase.Facing. The liquid networks (PipeConnectionHelper.CanConnect and
    // LiquidNetworkSystem.ResolveFlow) only run on the server and read the SERVER's Facing, so before this patch a right-click on a client
    // rotated the replica only: the sprite turned, the server's network did not, and the next machine snapshot put the old facing back.
    //
    // Client (multiplayer): a right-click does not rotate anything locally. MotorTileEntityBase.OnRightClick calls Request, which sends
    // MotorRotateRequest with the facing the player wants (the replica's facing turned by 90 degrees, a value and not "rotate once", so a
    // repeated or delayed packet changes nothing twice). The server validates it (MachineRequest.Validate: entity exists, player alive
    // and in reach; the machine must be a motor; the facing must be a real direction), sets Facing, flags a network rebuild and broadcasts
    // MotorFacingChanged to EVERY client (four bytes, rare, and the network is not tied to where a player stands). A request for the facing
    // the server already has changes nothing and only answers the sender with the real value. A refused request is not answered: nothing
    // was changed on the client, so there is nothing to roll back.
    //
    // Client receipt: the packet handler only queues the change; the main thread (PostUpdateEverything) writes the facing into the replica
    // and reframes the neighbouring pipes (PipeTileBase.ReframeNeighbors changes tile frames, which is main-thread work). A motor with no
    // replica yet ignores the packet: its snapshot carries the facing when it arrives.
    //
    // Single-player and the host do not send anything: OnRightClick rotates the real motor, flags the rebuild and reframes the pipes.
    public static class MotorRotate
    {
        sealed class Change
        {
            public Point16 Corner;
            public Direction Facing;
        }

        static readonly ConcurrentQueue<Change> changes = new ConcurrentQueue<Change>();

        // Temporary test counters (remove in Phase 10).
        public static int ServerRotated;
        public static int ServerRefused;
        public static int ServerAlreadyThere;
        public static int ClientSent;
        public static int ClientApplied;

        // ---------------------------------------------------------------- client

        /// <summary>Multiplayer client: asks the server to turn this motor 90 degrees from the facing the replica shows.</summary>
        public static void Request(MotorTileEntityBase motor)
        {
            if (motor == null)
                return;

            ClientSent++;
            Point16 corner = motor.Position;
            byte wanted = (byte)motor.Facing.Rotate90();
            FactorrariaNet.SendRequest(FactorrariaPacket.MotorRotateRequest, w =>
            {
                w.Write(corner.X);
                w.Write(corner.Y);
                w.Write(wanted);
            });
        }

        // ---------------------------------------------------------------- server

        public static void HandleRequest(BinaryReader reader, int sender)
        {
            short x = reader.ReadInt16();
            short y = reader.ReadInt16();
            int wanted = reader.ReadByte();

            if (Main.netMode != NetmodeID.Server)
                return;   // single-player and the host: OnRightClick already rotated the real motor

            RequestCheck check = MachineRequest.Validate(sender, x, y, 0f, out BaseMachine machine, out Point16 corner);
            if (check == RequestCheck.Ignored)
                return;

            if (check == RequestCheck.Refused || machine is not MotorTileEntityBase motor || wanted > (int)Direction.Right)
            {
                ServerRefused++;
                FactorrariaNet.Log("[Motor] refused rotation from " + Main.player[sender].name + " at (" + corner.X + ", " + corner.Y + "): "
                    + (machine == null ? "no machine there" : machine is not MotorTileEntityBase ? "not a motor" : wanted > (int)Direction.Right ? "bad direction" : "out of reach"));
                return;
            }

            Direction facing = (Direction)wanted;
            if (motor.Facing == facing)
            {
                ServerAlreadyThere++;
                FactorrariaNet.SendToClient(sender, FactorrariaPacket.MotorFacingChanged, w => WriteChanged(w, corner, facing));
                return;   // already that way: only the sender is told the real value
            }

            motor.Facing = facing;
            LiquidNetworkSystem.networkNeedsRebuilding = true;
            ServerRotated++;
            FactorrariaNet.Log("[Motor] " + Main.player[sender].name + " turned the motor at (" + corner.X + ", " + corner.Y + ") to " + facing);
            FactorrariaNet.Broadcast(FactorrariaPacket.MotorFacingChanged, w => WriteChanged(w, corner, facing));
        }

        static void WriteChanged(BinaryWriter w, Point16 corner, Direction facing)
        {
            w.Write(corner.X);
            w.Write(corner.Y);
            w.Write((byte)facing);
        }

        // ---------------------------------------------------------------- client: the change

        // May run on the network thread: it only queues.
        public static void HandleChanged(BinaryReader reader, int sender)
        {
            short x = reader.ReadInt16();
            short y = reader.ReadInt16();
            int facing = reader.ReadByte();
            if (facing > (int)Direction.Right)
                return;

            changes.Enqueue(new Change { Corner = new Point16(x, y), Facing = (Direction)facing });
        }

        /// <summary>Main thread, every tick on a client.</summary>
        public static void Update()
        {
            while (changes.TryDequeue(out Change change))
            {
                if (!TileEntity.ByPosition.TryGetValue(change.Corner, out TileEntity entity) || entity is not MotorTileEntityBase motor)
                    continue;

                motor.Facing = change.Facing;
                PipeTileBase.ReframeNeighbors(change.Corner.X, change.Corner.Y);
                ClientApplied++;
            }
        }

        public static void Reset()
        {
            while (changes.TryDequeue(out _)) { }
        }

        // Test aid (temporary, remove in Phase 10).
        public static string Describe()
        {
            return Main.netMode == NetmodeID.Server
                ? "motor rotations: rotated " + ServerRotated + ", already there " + ServerAlreadyThere + ", refused " + ServerRefused
                : "motor rotations: sent " + ClientSent + ", applied " + ClientApplied;
        }
    }

    public class MotorRotateSystem : ModSystem
    {
        public override void Load()
        {
            FactorrariaNet.Register(FactorrariaPacket.MotorRotateRequest, PacketDirection.ToServer, MotorRotate.HandleRequest);
            FactorrariaNet.Register(FactorrariaPacket.MotorFacingChanged, PacketDirection.ToClient, MotorRotate.HandleChanged);
        }

        public override void PostUpdateEverything()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                MotorRotate.Update();
        }

        public override void OnWorldUnload()
        {
            MotorRotate.Reset();
        }

        public override void Unload()
        {
            MotorRotate.Reset();
        }
    }
}