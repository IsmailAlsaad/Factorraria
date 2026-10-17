using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // Player-level multiplayer plumbing (roadmap patch 1.2): asks the server for the full state when this client enters a world,
    // and runs the disconnect cleanup hooks when a player leaves. SyncPlayer / CopyClientState / SendClientChanges are empty stubs
    // for any per-player state other clients need later (for example the mech suit flag, optionally).
    public class FactorrariaPlayerSync : ModPlayer
    {
        // Local client only (also fires in single-player, where the request runs the handler directly and nothing is sent).
        public override void OnEnterWorld()
        {
            JoinSync.RequestFullState();
        }

        // Server and remote clients. Clears this player's pending join job and runs the registered cleanup hooks.
        public override void PlayerDisconnect()
        {
            JoinSync.OnPlayerDisconnect(Player.whoAmI);
        }

        public override void CopyClientState(ModPlayer targetCopy)
        {
        }

        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
        {
        }

        public override void SendClientChanges(ModPlayer clientPlayer)
        {
        }
    }
}