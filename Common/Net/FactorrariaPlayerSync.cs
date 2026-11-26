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

            // Welcome message (local client only, so it needs no syncing).
            Microsoft.Xna.Framework.Color welcomeColor = new Microsoft.Xna.Framework.Color(255, 140, 0);
            Terraria.Main.NewText("Hello!", welcomeColor);
            Terraria.Main.NewText("Thank you for trying out Factorraria :D", welcomeColor);
            Terraria.Main.NewText("The mod is currently in its beta stage so if you find any bugs or have some recommendations don't be shy to let me know over at discord, my username: ismail643 <3", welcomeColor);
            Terraria.Main.NewText("P.S. I didn't make a wiki for this mod on purpose so you could explore and figure things out yourself ;)", welcomeColor);
        }

        // Server and remote clients. Clears this player's pending join job and runs the registered cleanup hooks.
        public override void PlayerDisconnect()
        {
            JoinSync.OnPlayerDisconnect(Player.whoAmI);
            MachineDetailSystem.OnPlayerLeft(Player.whoAmI);   // patch 5.3 (server only)
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