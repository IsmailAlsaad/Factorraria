using Factorraria.Content.Configs;
using Factorraria.Content.Tiles.SuperWeapons.MissileTank;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.SuperWeapons
{
    // /fweapon: debug command for the missile launch (super weapons patch S4.1).
    //   /fweapon [status]   every missile tank on this side with its state, facing and first problem, plus the Wall of Flesh and its shield
    //   /fweapon fire       (test, needs EnableDebugs) starts the ignition of the ready tank (assembled, full) nearest to you, WITHOUT needing a wall
    //   /fweapon reset      (test, needs EnableDebugs) every Igniting / Launched tank goes back to a normal state
    // status runs on every side (a client shows its own replica, which proves the sync). fire and reset only work where the simulation
    // runs: single-player and the server console. Removed in patch S6 (roadmap section 11).
    public class SuperWeaponCommand : ModCommand
    {
        public override string Command => "fweapon";
        public override CommandType Type => CommandType.Chat | CommandType.Console;
        public override string Usage => "/fweapon [status|fire|reset]";
        public override string Description => "Missile launch: status, fire (test), reset (test).";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            string sub = args.Length > 0 ? args[0].ToLowerInvariant() : "status";

            if (sub == "status")
                Status(caller);
            else if (sub == "fire" || sub == "reset")
                Change(caller, sub == "fire");
            else
                caller.Reply("Usage: " + Usage, Color.OrangeRed);
        }

        static string Side()
        {
            return Main.netMode == NetmodeID.Server ? "server" : Main.netMode == NetmodeID.MultiplayerClient ? "client" : "single-player";
        }

        static void Status(CommandCaller caller)
        {
            caller.Reply("fweapon [" + Side() + "]:", Color.LightGreen);

            int tanks = 0;
            foreach (TileEntity entity in TileEntity.ByID.Values)
            {
                if (!(entity is MissileTankTileEntity tank))
                    continue;

                tanks++;
                caller.Reply("  tank (" + tank.Position.X + ", " + tank.Position.Y + "): " + tank.WeaponState + ", " + tank.AssemblyText + (Main.netMode != NetmodeID.MultiplayerClient ? ", fuel " + (int)tank.InputLiquids[0].Amount + "/" + (int)tank.FuelCapacity : "")
                    + (tank.WeaponState == SuperWeaponState.Igniting && Main.netMode != NetmodeID.MultiplayerClient ? " [" + tank.IgnitionTicksLeft + " ticks left]" : ""), Color.White);
            }

            if (tanks == 0)
                caller.Reply("  no missile tanks", Color.Gray);

            NPC wall = null;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                if (Main.npc[i].active && Main.npc[i].type == NPCID.WallofFlesh)
                {
                    wall = Main.npc[i];
                    break;
                }
            }

            int missileType = ModContent.ProjectileType<SuperWeaponMissile>();
            int missiles = 0;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile projectile = Main.projectile[i];
                if (!projectile.active || projectile.type != missileType)
                    continue;

                missiles++;
                caller.Reply("  missile in flight: tile (" + (int)(projectile.Center.X / 16f) + ", " + (int)(projectile.Center.Y / 16f) + "), speed " + projectile.velocity.X.ToString("0.0") + " px/tick, facing " + (projectile.ai[1] < 0f ? "left" : "right"), Color.White);
            }

            if (missiles == 0)
                caller.Reply("  no missile in flight", Color.Gray);

            caller.Reply("  " + SuperWeaponMissile.ArtStatus(), Color.White);

            if (wall == null)
                caller.Reply("  no Wall of Flesh", Color.Gray);
            else
                caller.Reply("  Wall of Flesh at x " + (int)(wall.Center.X / 16f) + " (tiles), shield " + (WallShieldGlobalNPC.IsShielded(wall) ? "UP" : "down"), Color.White);
        }

        static void Change(CommandCaller caller, bool fire)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                caller.Reply("This only works in single-player or on the server console.", Color.OrangeRed);
                return;
            }

            FurnaceOffsetConfig config = ModContent.GetInstance<FurnaceOffsetConfig>();
            if (config == null || !config.EnableDebugs)
            {
                caller.Reply("Turn on EnableDebugs in the Factorraria mod config first.", Color.OrangeRed);
                return;
            }

            SuperWeaponSystem system = ModContent.GetInstance<SuperWeaponSystem>();
            if (!fire)
            {
                caller.Reply("Cleared " + system.ClearAll() + " tank(s).", Color.LightGreen);
                return;
            }

            MissileTankTileEntity best = null;
            float bestDistance = float.MaxValue;
            Vector2 from = caller.Player != null ? caller.Player.Center : Vector2.Zero;
            foreach (TileEntity entity in TileEntity.ByID.Values)
            {
                if (!(entity is MissileTankTileEntity tank) || !tank.IsReadyToFire)
                    continue;

                float distance = Vector2.Distance(from, SuperWeaponSystem.TankCenter(tank));
                if (distance < bestDistance)
                {
                    best = tank;
                    bestDistance = distance;
                }
            }

            if (best == null)
                caller.Reply("No ready tank (assembled weapon, full tank) found.", Color.OrangeRed);
            else if (!system.StartIgnition(best))
                caller.Reply("Could not start: another weapon is already igniting.", Color.OrangeRed);
            else
                caller.Reply("Ignition started.", Color.LightGreen);
        }
    }
}