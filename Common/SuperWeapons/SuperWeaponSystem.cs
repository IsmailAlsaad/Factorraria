using Factorraria.Common.Net;
using Factorraria.Content.Tiles.SuperWeapons.MissileTank;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.SuperWeapons
{
    // The launch trigger and the ignition countdown (super weapons patch S4.1).
    //
    // AUTHORITY: the server (or single-player) runs everything here; a multiplayer client does nothing. A client only sees the result: the
    // tank's WeaponState (Igniting) through the snapshot / light update / detail channel, and one cosmetic packet (SuperWeaponEffect,
    // kind Ignition) for the sound and the camera shake. No request packet: the player's only actions are building and fueling.
    //
    // TRIGGER (roadmap section 2, decisions D2 and D3): every TriggerCheckTicks, while a Wall of Flesh with its shield up exists, take the
    // ready tanks (Assembled + full tank) whose facing points at the wall (sign(wall.x - weapon.x) == Facing) and start the nearest one (SuperWeaponTrigger.Pick).
    // Nothing starts while another weapon is Igniting or Launched, so only one fires per wall. The check is continuous, so a weapon that became ready
    // after the wall spawned also fires. IGNITION: IgniteTicks ticks of Igniting, then Launched. In S4.1 Launched only logs (S4.2 spawns the
    // missile and removes the parts); it is cleared when the wall is gone or shield-less, when a part is removed, or by /fweapon reset.
    // The tunables are instance fields so hot reload can change them.
    public class SuperWeaponSystem : ModSystem
    {
        /// <summary>Ticks between two looks for a launch (the check walks the tile entities once).</summary>
        public int TriggerCheckTicks = 30;

        /// <summary>Ticks between the trigger and the launch (flames, shake, sound).</summary>
        public int IgniteTicks = 120;

        int checkTimer;
        MissileTankTileEntity igniting;   // the tank counting down, null = none
        readonly List<MissileTankTileEntity> primedTanks = new List<MissileTankTileEntity>();
        readonly List<TriggerCandidate> candidates = new List<TriggerCandidate>();

        public override void OnWorldLoad() => Reset();

        public override void OnWorldUnload() => Reset();

        void Reset()
        {
            checkTimer = 0;
            igniting = null;
            primedTanks.Clear();
            candidates.Clear();
        }

        public override void PostUpdateEverything()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            TickIgnition();

            if (--checkTimer > 0)
                return;

            checkTimer = Math.Max(1, TriggerCheckTicks);
            CheckTrigger();
        }

        /// <summary>The Wall of Flesh (the mouth) while its stone shield is up, or null.</summary>
        public static NPC FindShieldedWall()
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && npc.type == NPCID.WallofFlesh && WallShieldGlobalNPC.IsShielded(npc))
                    return npc;
            }

            return null;
        }

        /// <summary>The weapon's centre in world pixels (the tank's centre; the 7 x 3 weapon is centred on it within a tile or so).</summary>
        public static Vector2 TankCenter(MissileTankTileEntity tank)
        {
            return new Vector2(tank.Position.X * 16f + SuperWeaponAssembly.TankWidth * 8f, tank.Position.Y * 16f + SuperWeaponAssembly.TankHeight * 8f);
        }

        void CheckTrigger()
        {
            NPC wall = FindShieldedWall();
            if (wall == null)
            {
                ClearSpentTanks();
                return;
            }

            bool busy = igniting != null;
            primedTanks.Clear();
            candidates.Clear();

            foreach (TileEntity entity in TileEntity.ByID.Values)
            {
                if (!(entity is MissileTankTileEntity tank))
                    continue;

                if (tank.WeaponState >= SuperWeaponState.Igniting)
                    busy = true;   // Igniting, or Launched at this wall
                else if (tank.IsReadyToFire)
                {
                    primedTanks.Add(tank);
                    candidates.Add(new TriggerCandidate(tank.ID, TankCenter(tank).X, tank.WeaponFacing));
                }
            }

            int pick = SuperWeaponTrigger.Pick(wall.Center.X, candidates, busy);
            if (pick >= 0)
                StartIgnition(primedTanks[pick]);
        }

        /// <summary>Starts the countdown on a ready tank (the trigger, and /fweapon fire). Returns false when the tank is not ready or another one is counting down.</summary>
        public bool StartIgnition(MissileTankTileEntity tank)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient || igniting != null || !tank.BeginIgnition(IgniteTicks))
                return false;

            igniting = tank;
            Vector2 center = TankCenter(tank);
            SuperWeaponNet.SendEffect(SuperWeaponEffectKind.Ignition, center);
            Say("[Missile] ignition: tank at (" + tank.Position.X + ", " + tank.Position.Y + "), facing " + (tank.WeaponFacing > 0 ? "right" : "left") + ", launch in " + IgniteTicks + " ticks");
            return true;
        }

        void TickIgnition()
        {
            if (igniting == null)
                return;

            MissileTankTileEntity tank = igniting;
            bool alive = TileEntity.ByID.TryGetValue(tank.ID, out TileEntity current) && ReferenceEquals(current, tank);
            if (!alive || tank.WeaponState != SuperWeaponState.Igniting)
            {
                // the tank was mined, or a part was removed and the tank went back to a normal state: the countdown is aborted
                igniting = null;
                Say("[Missile] ignition aborted");
                return;
            }

            if (--tank.IgnitionTicksLeft > 0)
                return;

            tank.CompleteIgnition();
            igniting = null;
            Say("[Missile] LAUNCH (S4.1 stub: the missile itself comes with S4.2)");
        }

        // No shielded wall: weapons that ignited or "launched" for it go back to normal.
        void ClearSpentTanks()
        {
            igniting = null;
            foreach (TileEntity entity in TileEntity.ByID.Values)
                if (entity is MissileTankTileEntity tank && tank.WeaponState >= SuperWeaponState.Igniting)
                    tank.ClearLaunch();
        }

        /// <summary>Debug (/fweapon reset): every Igniting / Launched tank goes back to a normal state. Returns how many.</summary>
        public int ClearAll()
        {
            int cleared = 0;
            igniting = null;
            foreach (TileEntity entity in TileEntity.ByID.Values)
            {
                if (entity is MissileTankTileEntity tank && tank.WeaponState >= SuperWeaponState.Igniting)
                {
                    tank.ClearLaunch();
                    cleared++;
                }
            }

            return cleared;
        }

        // Temporary test output (removed in S6, roadmap section 11): the server log, plus the chat in single-player so it can be read there.
        static void Say(string message)
        {
            FactorrariaNet.Log("[SuperWeapons] " + message);
            if (Main.netMode == NetmodeID.SinglePlayer)
                Main.NewText(message, Color.Orange);
        }
    }
}