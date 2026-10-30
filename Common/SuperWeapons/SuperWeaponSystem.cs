using Factorraria.Common.Net;
using Factorraria.Content.Tiles.SuperWeapons.MissileDrill;
using Factorraria.Content.Tiles.SuperWeapons.MissileTank;
using Factorraria.Content.Tiles.SuperWeapons.MissileThruster;
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

        /// <summary>Missile acceleration in pixels per tick squared (S4.2).</summary>
        public float MissileAccel = 0.35f;

        /// <summary>Missile top speed in pixels per tick (S4.2). Must stay well above the Wall of Flesh's speed.</summary>
        public float MissileMaxSpeedPx = 22f;

        /// <summary>The missile fizzles after flying this far from the launch point, in tiles (S4.2).</summary>
        public int MissileMaxRangeTiles = 700;

        /// <summary>The missile hits the wall when its x is closer than this (S4.3: explosion plus BreakShield).</summary>
        public float HitDistancePx = 32f;

        /// <summary>True only while Launch removes the weapon's tiles, so SuperWeaponDropGuard keeps the parts from dropping items.</summary>
        public bool SuppressPartDrops;

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

        /// <summary>The live Wall of Flesh (the mouth), shield up or not, or null (S4.3: the missile tells "gone" from "shield already down").</summary>
        public static NPC FindWall()
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && npc.type == NPCID.WallofFlesh)
                    return npc;
            }

            return null;
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

            bool busy = igniting != null || SuperWeaponMissile.AnyInFlight();   // S4.2: one weapon per wall, including the one in flight
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
            Launch(tank);
        }

        /// <summary>
        /// S4.2, server / single-player only, ONE server step: spawns the missile at the weapon's centre, plays the launch effect, then removes
        /// every part tile (drill, tank with its entity and fuel, the three thrusters) without drops. The projectile is spawned first so a
        /// problem while removing tiles can never leave a weapon that vanished without a missile.
        /// </summary>
        void Launch(MissileTankTileEntity tank)
        {
            int facing = tank.WeaponFacing == 0 ? 1 : tank.WeaponFacing;
            int left = tank.Position.X;
            int top = tank.Position.Y;
            Vector2 center = TankCenter(tank);

            int drillX = facing > 0 ? left + SuperWeaponAssembly.TankWidth : left - SuperWeaponAssembly.DrillWidth;
            int thrusterX = facing > 0 ? left - 1 : left + SuperWeaponAssembly.TankWidth;

            Projectile.NewProjectile(new EntitySource_Misc("Factorraria.SuperWeaponLaunch"), center, Vector2.Zero, ModContent.ProjectileType<SuperWeaponMissile>(), 0, 0f, Main.myPlayer, 0f, facing, center.X);
            SuperWeaponNet.SendEffect(SuperWeaponEffectKind.Launch, center);

            int removed = 0;
            SuppressPartDrops = true;
            try
            {
                removed += KillPartTiles(left, top, SuperWeaponAssembly.TankWidth, SuperWeaponAssembly.TankHeight);
                removed += KillPartTiles(drillX, top, SuperWeaponAssembly.DrillWidth, SuperWeaponAssembly.DrillHeight);
                removed += KillPartTiles(thrusterX, top, 1, SuperWeaponAssembly.TankHeight);
            }
            finally
            {
                SuppressPartDrops = false;
            }

            Say("[Missile] LAUNCH: missile spawned at tile (" + (int)(center.X / 16f) + ", " + (int)(center.Y / 16f) + "), facing " + (facing > 0 ? "right" : "left") + ", removed " + removed + " part tile(s)");
        }

        // Kills the part tiles of a rectangle (never anything else) through the normal tile kill path: the machine kill hook removes the tank's
        // entity, and on a server every client gets the same "kill tile without item" action so its own hooks run too.
        int KillPartTiles(int x0, int y0, int width, int height)
        {
            int killed = 0;
            for (int x = x0; x < x0 + width; x++)
            {
                for (int y = y0; y < y0 + height; y++)
                {
                    if (!WorldGen.InWorld(x, y, 1))
                        continue;

                    Tile tile = Main.tile[x, y];
                    if (!tile.HasTile || !IsPartTile(tile.TileType))
                        continue;

                    WorldGen.KillTile(x, y, false, false, true);
                    if (Main.netMode == NetmodeID.Server)
                        NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 4, x, y);

                    killed++;
                }
            }

            return killed;
        }

        /// <summary>True for the three super weapon part tiles.</summary>
        public static bool IsPartTile(int tileType)
        {
            return tileType == ModContent.TileType<MissileTankTile>() || tileType == ModContent.TileType<MissileDrillTile>() || tileType == ModContent.TileType<MissileThrusterTile>();
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
            int cleared = SuperWeaponMissile.KillAll();   // S4.2: missiles in flight count too
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
        public static void Say(string message)
        {
            FactorrariaNet.Log("[SuperWeapons] " + message);
            if (Main.netMode == NetmodeID.SinglePlayer)
                Main.NewText(message, Color.Orange);
        }
    }
}