using Factorraria.Common.Machines;
using Factorraria.Content.Tiles.Machines.GeneralMachines.MagicMaterializer;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // Materializer background toggle (roadmap patch 6.4, findings M4 and U9).
    //
    // Authority: the server (or single-player) owns MagicMaterializerTileEntity.BackgroundActive, because the server's Update reads it
    // to set the monolith frames (the starry background) and the server saves it.
    //
    // A right-click on a multiplayer client does not flip anything locally. MagicMaterializerTileEntity.OnRightClick calls Request, which
    // sends MaterializerToggleRequest with the value the player wants (the opposite of what the replica shows). The server validates it
    // (MachineRequest.Validate: entity exists, player alive and in reach; the machine must be a Materializer with shimmer in its tank,
    // as in single-player), sets the value and broadcasts MaterializerBackgroundChanged to EVERY client (a few bytes, rare), so every
    // replica follows, near or far, and the next server SyncMonolithFrames tick sends the monolith tiles as before. The packet names the
    // player who asked; that client plays the click sound when the change arrives. A request that is refused (no shimmer, out of reach)
    // is simply not answered: there is nothing to roll back because the client never changed anything. A request for the value the
    // server already has (two clicks before the first answer, two players) changes nothing and only answers the sender with the real value.
    //
    // Single-player and the host do not send anything: OnRightClick flips the real machine.
    //
    // MaterializerRightClickGuard (U9) stops vanilla's own monolith toggle (and its HitSwitch message to the server) from running at all.
    public static class MaterializerToggle
    {
        // Set on the network thread, played on the main thread (PostUpdateEverything of the system below).
        static volatile bool playClick;

        // Temporary test counters (remove in Phase 10).
        public static int ServerChanged;
        public static int ServerRefused;
        public static int ClientSent;
        public static int ClientApplied;

        // ---------------------------------------------------------------- client

        /// <summary>Multiplayer client: asks the server to switch the background to the opposite of what this replica shows.</summary>
        public static void Request(MagicMaterializerTileEntity machine)
        {
            if (machine == null)
                return;

            ClientSent++;
            Point16 corner = machine.Position;
            bool wanted = !machine.BackgroundActive;
            FactorrariaNet.SendRequest(FactorrariaPacket.MaterializerToggleRequest, w =>
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
            bool wanted = reader.ReadBoolean();

            if (Main.netMode != NetmodeID.Server)
                return;   // single-player and the host: OnRightClick already changed the real machine

            RequestCheck check = MachineRequest.Validate(sender, x, y, 0f, out BaseMachine machine, out Point16 corner);
            if (check == RequestCheck.Ignored)
                return;

            if (check == RequestCheck.Refused || machine is not MagicMaterializerTileEntity materializer)
            {
                ServerRefused++;
                FactorrariaNet.Log("[Materializer] refused toggle from " + Main.player[sender].name + " at (" + corner.X + ", " + corner.Y + "): " + (machine == null ? "no machine there" : machine is MagicMaterializerTileEntity ? "out of reach" : "not a Materializer"));
                return;
            }

            if (!materializer.HasShimmer)
            {
                ServerRefused++;
                FactorrariaNet.Log("[Materializer] refused toggle from " + Main.player[sender].name + " at (" + corner.X + ", " + corner.Y + "): no shimmer in the tank");
                return;
            }

            if (materializer.BackgroundActive == wanted)
            {
                FactorrariaNet.SendToClient(sender, FactorrariaPacket.MaterializerBackgroundChanged, w => WriteChanged(w, corner, wanted, -1));
                return;   // already in that state: only the sender is told the real value
            }

            materializer.BackgroundActive = wanted;
            ServerChanged++;
            FactorrariaNet.Log("[Materializer] " + Main.player[sender].name + " switched the background " + (wanted ? "on" : "off") + " at (" + corner.X + ", " + corner.Y + ")");
            int who = sender;
            FactorrariaNet.Broadcast(FactorrariaPacket.MaterializerBackgroundChanged, w => WriteChanged(w, corner, wanted, who));
        }

        static void WriteChanged(BinaryWriter w, Point16 corner, bool active, int by)
        {
            w.Write(corner.X);
            w.Write(corner.Y);
            w.Write(active);
            w.Write((sbyte)by);
        }

        // ---------------------------------------------------------------- client: the change

        // May run on the network thread: it only writes one bool into the replica (like the detail and light handlers do) and sets a flag.
        public static void HandleChanged(BinaryReader reader, int sender)
        {
            short x = reader.ReadInt16();
            short y = reader.ReadInt16();
            bool active = reader.ReadBoolean();
            int by = reader.ReadSByte();

            // No replica yet: nothing to do, the machine's snapshot carries the value when it arrives.
            if (TileEntity.ByPosition.TryGetValue(new Point16(x, y), out TileEntity entity) && entity is MagicMaterializerTileEntity materializer)
            {
                materializer.BackgroundActive = active;
                ClientApplied++;
            }

            if (by == Main.myPlayer)
                playClick = true;
        }

        /// <summary>Main thread, every tick on a client.</summary>
        public static void Update()
        {
            if (!playClick)
                return;

            playClick = false;
            SoundEngine.PlaySound(SoundID.MenuTick);
        }

        public static void Reset()
        {
            playClick = false;
        }

        // Test aid (temporary, used by /fslot).
        public static string Describe()
        {
            return Main.netMode == NetmodeID.Server
                ? "materializer toggles: changed " + ServerChanged + ", refused " + ServerRefused
                : "materializer toggles: sent " + ClientSent + ", applied " + ClientApplied;
        }
    }

    public class MaterializerToggleSystem : ModSystem
    {
        public override void Load()
        {
            FactorrariaNet.Register(FactorrariaPacket.MaterializerToggleRequest, PacketDirection.ToServer, MaterializerToggle.HandleRequest);
            FactorrariaNet.Register(FactorrariaPacket.MaterializerBackgroundChanged, PacketDirection.ToClient, MaterializerToggle.HandleChanged);
        }

        public override void PostUpdateEverything()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                MaterializerToggle.Update();
        }

        public override void OnWorldUnload()
        {
            MaterializerToggle.Reset();
        }

        public override void Unload()
        {
            MaterializerToggle.Reset();
        }
    }
}