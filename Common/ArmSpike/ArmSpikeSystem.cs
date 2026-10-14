using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.ArmSpike
{
    // THROWAWAY SPIKE for the Robotic Arm (Phase 0b). Delete the whole Common/ArmSpike folder once the checklist is done.
    // Question it answers: do the direct vanilla calls (Player.PickTile, Player.PickWall, the replicated axe branch,
    // WorldGen.PlaceTile) work with one hidden Player that is only a hitTile holder, and can OnSpawn capture the drops?
    // The hidden player is never ticked, never put into Main.player, and ItemCheck() is never called on it
    // (the one exception is via=itemcheck, which uses a separate throwaway player to prove the early return).
    public class ArmSpikeSession
    {
        public string Mode;                // pick, axe, hammer, place
        public bool ViaItemCheck;          // diagnostic: run ItemCheck() on a temp player instead of the direct calls
        public bool Capture;               // swallow drops inside OnSpawn
        public bool Filters = true;        // false: skip the arm's own pre-filters so vanilla's own refusal can be seen
        public Item Tool;                  // copy of the held item (place mode consumes from this copy)
        public Point Target;
        public int TicksTotal;
        public int Tick;
        public int Interval;               // ticks between strikes (the item's useTime)
        public int Strikes;
        public int ErrorCount;
        public Exception FirstError;
        public bool InStep;                // true only while a strike runs, so the drop logger ignores everything else
        public string Before;
        public string AreaBefore;
        public int TreeBefore;
        public int DoneTick = -1;
        public string DoneReason = "ran out of ticks";
        public Player Diag;                // via=itemcheck only
        public int MaxAnimation;
        public List<string> Drops = new List<string>();
        public List<string> Swallowed = new List<string>();
    }

    public class ArmSpikeSystem : ModSystem
    {
        public ArmSpikeSession Session;
        public Player Ghost;
        public string GhostStatus = "not created yet";

        public override void OnWorldUnload()
        {
            Stop();
        }

        public override void Unload()
        {
            Session = null;
            Ghost = null;
        }

        public void Stop()
        {
            Session = null;
        }

        // The one shared hidden player. Creating it outside a Main.player slot is one of the things this spike checks.
        public Player EnsureGhost()
        {
            if (Ghost != null)
                return Ghost;

            try
            {
                Ghost = new Player();
                Ghost.name = "ArmSpikeGhost";
                GhostStatus = $"created; whoAmI {Ghost.whoAmI}, hitTile {(Ghost.hitTile != null ? "ok" : "NULL")}, hitReplace {(Ghost.hitReplace != null ? "ok" : "NULL")}";
            }
            catch (Exception e)
            {
                Ghost = null;
                GhostStatus = "FAILED: " + e.GetType().Name + ": " + e.Message;
                Mod.Logger.Warn("[ArmSpike] new Player() failed: " + e);
            }

            return Ghost;
        }

        // Returns null when the spike started, otherwise the reason it did not.
        public string TryStart(Player real, string mode, bool viaItemCheck, bool capture, bool filters, int ticks)
        {
            if (Main.netMode != NetmodeID.SinglePlayer)
                return "Singleplayer only.";
            if (Session != null)
                return "A spike is already running (use /armspike stop).";

            Item held = real.HeldItem;
            if (held == null || held.IsAir)
                return "Hold the item you want to test.";

            bool isTool = held.pick > 0 || held.axe > 0 || held.hammer > 0;
            if (mode == "pick" && held.pick <= 0)
                return "Hold a pickaxe for pick mode.";
            if (mode == "axe" && held.axe <= 0)
                return "Hold an axe for axe mode.";
            if (mode == "hammer" && held.hammer <= 0)
                return "Hold a hammer for hammer mode.";
            if (mode == "place" && (isTool || held.createTile < 0))
                return "Hold a block for place mode.";

            Player ghost = null;
            Player diag = null;
            if (viaItemCheck)
            {
                try
                {
                    diag = new Player();
                    diag.name = "ArmSpikeDiag";
                    diag.active = true;
                    diag.whoAmI = 1;   // anything but Main.myPlayer: this is the "not the local player" case
                    diag.inventory[0] = held.Clone();
                    diag.selectedItem = 0;
                    diag.direction = 1;
                }
                catch (Exception e)
                {
                    return "new Player() failed: " + e.GetType().Name + ": " + e.Message;
                }
            }
            else
            {
                ghost = EnsureGhost();
                if (ghost == null)
                    return "new Player() failed: " + GhostStatus;
            }

            string totalUseTime;
            try
            {
                totalUseTime = CombinedHooks.TotalUseTime(held.useTime, ghost ?? diag, held).ToString();
            }
            catch (Exception e)
            {
                totalUseTime = "threw " + e.GetType().Name + ": " + e.Message;
            }

            Point target = Main.MouseWorld.ToTileCoordinates();
            ArmSpikeSession s = new ArmSpikeSession
            {
                Mode = mode,
                ViaItemCheck = viaItemCheck,
                Capture = capture,
                Filters = filters,
                Tool = held.Clone(),
                Target = target,
                TicksTotal = ticks,
                Interval = Math.Max(1, held.useTime),
                Diag = diag,
                Before = Describe(target),
                AreaBefore = DescribeArea(target),
                TreeBefore = TreeColumn(target)
            };

            Session = s;
            Mod.Logger.Info($"[ArmSpike] started: {mode} with {held.Name}, target {target.X},{target.Y}, via {(viaItemCheck ? "itemcheck" : "direct")}, filters {(filters ? "on" : "off")}, capture {(capture ? "onspawn" : "none")}, ticks {ticks}; useTime {held.useTime}, TotalUseTime(ghost) {totalUseTime}; ghost: {GhostStatus}; before: {s.Before}");
            return null;
        }

        public override void PostUpdateEverything()
        {
            ArmSpikeSession s = Session;
            if (s == null)
                return;

            s.Tick++;

            if (s.ViaItemCheck)
                StepItemCheck(s);
            else if (s.DoneTick < 0 && (s.Tick - 1) % s.Interval == 0)
                Strike(s);

            if (s.DoneTick >= 0 && s.Tick >= s.DoneTick + 20)   // a few extra ticks so late drops still get logged
                Finish(s);
            else if (s.Tick >= s.TicksTotal)
                Finish(s);
        }

        // ---- via=itemcheck: proves that ItemCheck() on a non-local player never reaches the mining/placing code ----
        void StepItemCheck(ArmSpikeSession s)
        {
            Player d = s.Diag;
            d.Center = new Vector2(s.Target.X * 16 + 8, s.Target.Y * 16 + 8 - 48);
            d.velocity = Vector2.Zero;
            d.controlUseItem = true;
            d.releaseUseItem = true;

            int savedX = Player.tileTargetX;
            int savedY = Player.tileTargetY;
            s.InStep = true;
            try
            {
                Player.tileTargetX = s.Target.X;
                Player.tileTargetY = s.Target.Y;
                d.ItemCheck();
            }
            catch (Exception e)
            {
                s.ErrorCount++;
                s.FirstError ??= e;
            }
            finally
            {
                s.InStep = false;
                Player.tileTargetX = savedX;
                Player.tileTargetY = savedY;
            }

            s.MaxAnimation = Math.Max(s.MaxAnimation, d.itemAnimation);
        }

        // ---- direct calls ----
        void Strike(ArmSpikeSession s)
        {
            s.Strikes++;
            string result;

            s.InStep = true;
            try
            {
                switch (s.Mode)
                {
                    case "pick": result = StrikePick(s); break;
                    case "axe": result = StrikeAxe(s); break;
                    case "hammer": result = StrikeHammer(s); break;
                    default: result = StrikePlace(s); break;
                }
            }
            catch (Exception e)
            {
                s.ErrorCount++;
                s.FirstError ??= e;
                result = "EXCEPTION " + e.GetType().Name + ": " + e.Message;
                Mod.Logger.Warn("[ArmSpike] exception in strike: " + e);
            }
            finally
            {
                s.InStep = false;
            }

            Tile t = Framing.GetTileSafely(s.Target.X, s.Target.Y);
            Mod.Logger.Info($"[ArmSpike] t={s.Tick} strike {s.Strikes}: {result} | {Describe(s.Target)}");

            if (result.StartsWith("REFUSED") || result.StartsWith("EXCEPTION"))
            {
                Done(s, result);
                return;
            }

            if ((s.Mode == "pick" || s.Mode == "axe") && !t.HasTile)
                Done(s, "tile is gone");
            else if (s.Mode == "hammer" && t.WallType == 0)
                Done(s, "wall is gone");
            else if (s.Mode == "place")
                Done(s, result);
            else if (s.Strikes >= 40)
                Done(s, "gave up after 40 strikes");
        }

        void Done(ArmSpikeSession s, string reason)
        {
            if (s.DoneTick >= 0)
                return;

            s.DoneTick = s.Tick;
            s.DoneReason = reason;
        }

        string StrikePick(ArmSpikeSession s)
        {
            int x = s.Target.X, y = s.Target.Y;
            Tile t = Framing.GetTileSafely(x, y);
            if (!t.HasTile)
                return "REFUSED: no tile here";

            int breakability = WorldGen.CheckTileBreakability(x, y);
            bool axeTile = Main.tileAxe[t.TileType];
            bool hammerTile = Main.tileHammer[t.TileType];
            string info = $"tileAxe={axeTile} tileHammer={hammerTile} CheckTileBreakability={breakability}";

            if (s.Filters)
            {
                if (axeTile || hammerTile)
                    return "REFUSED: routing rule, a pickaxe skips tileAxe/tileHammer tiles (" + info + ")";
                if (breakability == 2)
                    return "REFUSED: CheckTileBreakability is 2, KillTile would silently do nothing (" + info + ")";
            }

            EnsureGhost().PickTile(x, y, s.Tool.pick);
            return $"PickTile power {s.Tool.pick} ({info})";
        }

        // Replica of the inline tileAxe branch of vanilla's ItemCheck_UseMiningTools_ActuallyUseMiningTool (see plan section 2).
        string StrikeAxe(ArmSpikeSession s)
        {
            int x = s.Target.X, y = s.Target.Y;
            Tile t = Framing.GetTileSafely(x, y);
            if (!t.HasTile)
                return "REFUSED: no tile here";
            if (!Main.tileAxe[t.TileType])
                return "REFUSED: routing rule, an axe only acts on tileAxe tiles";

            Player ghost = EnsureGhost();
            int damage;
            if (TileLoader.GetTile(t.TileType) is ModTile modTile)
                damage = (int)(s.Tool.axe / modTile.MineResist);
            else if (t.TileType == 80)
                damage = (int)(s.Tool.axe * 3 * 1.2f);
            else
                damage = (int)(s.Tool.axe * 1.2f);

            if (Main.getGoodWorld)
                damage = (int)(damage * 1.3);
            if (!WorldGen.CanKillTile(x, y))
                damage = 0;

            int id = ghost.hitTile.HitObject(x, y, 1);
            int total = ghost.hitTile.AddDamage(id, damage);
            if (total >= 100)
            {
                ghost.hitTile.Clear(id);
                WorldGen.KillTile(x, y);
            }
            else
            {
                WorldGen.KillTile(x, y, fail: true);
            }

            return $"axe damage {damage}, hitTile total {total}";
        }

        string StrikeHammer(ArmSpikeSession s)
        {
            int x = s.Target.X, y = s.Target.Y;
            Tile t = Framing.GetTileSafely(x, y);
            if (t.WallType == 0)
                return "REFUSED: no wall here";

            if (s.Filters)
            {
                if (t.HasTile)
                    return "REFUSED: hammer only targets tiles with no tile in them";
                if (Main.wallDungeon[t.WallType] && !NPC.downedBoss3)
                    return "REFUSED: dungeon wall before Skeletron";
                if (t.WallType == 87 && !NPC.downedGolemBoss)
                    return "REFUSED: lihzahrd wall before Golem";
                if (!Player.CanPlayerSmashWall(x, y))
                    return "REFUSED: CanPlayerSmashWall says no";
            }

            int damage = (int)(s.Tool.hammer * 1.5f);
            EnsureGhost().PickWall(x, y, damage);
            return $"PickWall damage {damage}";
        }

        string StrikePlace(ArmSpikeSession s)
        {
            int x = s.Target.X, y = s.Target.Y;
            Item item = s.Tool;
            int type = item.createTile;
            Tile t = Framing.GetTileSafely(x, y);

            if (t.HasTile)
                return "REFUSED: target tile is occupied";

            if (s.Filters)
            {
                // v1 filter from the plan: plain solid blocks only.
                int[] banned = { 2, 23, 109, 199, 477, 492, 633, 661, 662, 60, 70 };
                if (!(Main.tileSolid[type] && !Main.tileFrameImportant[type]))
                    return "REFUSED: not a plain solid block (multi-tile or special tile)";
                if (Array.IndexOf(banned, type) >= 0 || Main.tileMoss[type])
                    return "REFUSED: grass, moss or other special block";
                if (!TileLoader.CanPlace(x, y, type))
                    return "REFUSED: TileLoader.CanPlace says no";
                if (!HasSupport(x, y))
                    return "REFUSED: no wall and no solid neighbour (copied vanilla support rule)";
            }

            int stackBefore = item.stack;
            bool placed = WorldGen.PlaceTile(x, y, type, mute: false, forced: false, plr: -1, style: item.placeStyle);
            if (placed)
            {
                TileLoader.PlaceInWorld(x, y, item);
                if (item.consumable && ItemLoader.ConsumeItem(item, EnsureGhost()))
                    item.stack--;
            }

            return $"PlaceTile returned {placed}; spike item stack {stackBefore} -> {item.stack}";
        }

        // Copy of vanilla's rule for plain blocks (PlaceThing_Tiles_BlockPlacementForAssortedThings, final else).
        // Beams are not counted yet: that condition has to be copied from the real method before Phase 4.
        bool HasSupport(int x, int y)
        {
            if (Framing.GetTileSafely(x, y).WallType > 0)
                return true;

            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };
            for (int k = 0; k < 4; k++)
            {
                Tile n = Framing.GetTileSafely(x + dx[k], y + dy[k]);
                if (n.HasTile && (Main.tileSolid[n.TileType] || Main.tileRope[n.TileType] || n.TileType == 314))
                    return true;
                if (n.WallType > 0)
                    return true;
            }

            return false;
        }

        // ---- reporting ----
        void Finish(ArmSpikeSession s)
        {
            List<string> lines = new List<string>();
            lines.Add($"RESULT {s.Mode} with {s.Tool.Name} (pick {s.Tool.pick}, axe {s.Tool.axe}, hammer {s.Tool.hammer}, createTile {s.Tool.createTile}); via {(s.ViaItemCheck ? "ItemCheck on a temp player" : "direct calls")}; filters {(s.Filters ? "on" : "off")}; capture {(s.Capture ? "onspawn" : "none")}; {s.Tick} ticks, {s.Strikes} strike(s)");
            lines.Add($"Ended because: {s.DoneReason}");
            lines.Add($"Target {s.Target.X},{s.Target.Y}: [{s.Before}] -> [{Describe(s.Target)}]");
            lines.Add($"3x3 around it before: {s.AreaBefore}");
            lines.Add($"3x3 around it after:  {DescribeArea(s.Target)}");
            if (s.Mode == "axe")
                lines.Add($"Tree-trunk tiles in the target's column, from the target upwards: {s.TreeBefore} -> {TreeColumn(s.Target)}");
            if (s.ViaItemCheck)
                lines.Add($"Max itemAnimation on the temp player: {s.MaxAnimation} (the animation may start, but the tile must not change)");
            lines.Add($"Exceptions: {s.ErrorCount}");
            if (s.FirstError != null)
            {
                lines.Add($"First exception: {s.FirstError.GetType().Name}: {s.FirstError.Message}");
                Mod.Logger.Warn("[ArmSpike] first exception in full: " + s.FirstError);
            }

            lines.Add($"OnSpawn saw {s.Drops.Count} drop(s): {(s.Drops.Count == 0 ? "none" : string.Join("; ", s.Drops))}");
            if (s.Capture)
                lines.Add($"Swallowed inside OnSpawn: {(s.Swallowed.Count == 0 ? "nothing" : string.Join("; ", s.Swallowed))}");
            lines.Add($"Items lying in the world within 6 tiles of the target: {DescribeWorldItems(s.Target)}");

            Stop();

            foreach (string line in lines)
            {
                Mod.Logger.Info("[ArmSpike] " + line);
                Main.NewText(line, 150, 220, 255);
            }
        }

        string Describe(Point p)
        {
            Tile t = Framing.GetTileSafely(p.X, p.Y);
            string tile = t.HasTile ? $"tile {t.TileType} {t.BlockType}" : "no tile";
            return $"{tile}, wall {t.WallType}";
        }

        // 3x3 summary, rows top to bottom: tile type (h = half brick, sN = slope N) or "-" for no tile, then wN for a wall.
        string DescribeArea(Point p)
        {
            List<string> rows = new List<string>();
            for (int dy = -1; dy <= 1; dy++)
            {
                List<string> cells = new List<string>();
                for (int dx = -1; dx <= 1; dx++)
                {
                    Tile t = Framing.GetTileSafely(p.X + dx, p.Y + dy);
                    string cell = t.HasTile
                        ? t.TileType + (t.IsHalfBlock ? "h" : "") + (t.Slope != SlopeType.Solid ? "s" + (int)t.Slope : "")
                        : "-";
                    if (t.WallType > 0)
                        cell += "w" + t.WallType;
                    cells.Add(cell);
                }
                rows.Add(string.Join(" ", cells));
            }
            return string.Join(" / ", rows);
        }

        // Contiguous tileAxe tiles in the target's column, starting at the target and going up.
        int TreeColumn(Point p)
        {
            int count = 0;
            for (int y = p.Y; y > p.Y - 80 && y > 0; y--)
            {
                Tile t = Framing.GetTileSafely(p.X, y);
                if (!t.HasTile || !Main.tileAxe[t.TileType])
                    break;
                count++;
            }
            return count;
        }

        string DescribeWorldItems(Point target)
        {
            Vector2 center = new Vector2(target.X * 16 + 8, target.Y * 16 + 8);
            List<string> found = new List<string>();
            for (int i = 0; i < Main.maxItems; i++)
            {
                Item item = Main.item[i];
                if (item.active && !item.IsAir && Vector2.Distance(item.Center, center) < 96f)
                    found.Add($"{item.Name} x{item.stack}");
            }
            return found.Count == 0 ? "none" : string.Join("; ", found);
        }

        // ---- /armspike info: prints the real signatures so nothing has to be guessed ----
        public List<string> DescribeApi()
        {
            EnsureGhost();
            List<string> list = new List<string>();
            list.Add($"Hidden player: {GhostStatus}");
            list.Add($"netMode {Main.netMode}, myPlayer {Main.myPlayer}, maxPlayers {Main.maxPlayers}");
            list.Add("Player.PickTile: " + Sigs(typeof(Player), "PickTile"));
            list.Add("Player.PickWall: " + Sigs(typeof(Player), "PickWall"));
            list.Add("Player.CanPlayerSmashWall: " + Sigs(typeof(Player), "CanPlayerSmashWall"));
            list.Add("Player.ClearMiningCacheAt: " + Sigs(typeof(Player), "ClearMiningCacheAt"));
            list.Add("Player.ItemCheck: " + Sigs(typeof(Player), "ItemCheck"));
            list.Add("HitTile.HitObject: " + Sigs(typeof(HitTile), "HitObject"));
            list.Add("HitTile.AddDamage: " + Sigs(typeof(HitTile), "AddDamage"));
            list.Add("HitTile.Clear: " + Sigs(typeof(HitTile), "Clear"));
            list.Add("WorldGen.CheckTileBreakability: " + Sigs(typeof(WorldGen), "CheckTileBreakability"));
            list.Add("WorldGen.CanKillTile: " + Sigs(typeof(WorldGen), "CanKillTile"));
            list.Add("WorldGen.PlaceTile: " + Sigs(typeof(WorldGen), "PlaceTile"));
            list.Add("Collision.EmptyTile: " + Sigs(typeof(Collision), "EmptyTile"));
            list.Add("TileLoader.CanPlace: " + Sigs(typeof(TileLoader), "CanPlace"));
            list.Add("TileLoader.PlaceInWorld: " + Sigs(typeof(TileLoader), "PlaceInWorld"));
            list.Add("ItemLoader.ConsumeItem: " + Sigs(typeof(ItemLoader), "ConsumeItem"));
            list.Add("CombinedHooks.TotalUseTime: " + Sigs(typeof(CombinedHooks), "TotalUseTime"));
            return list;
        }

        string Sigs(Type type, string name)
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            List<string> found = type.GetMethods(flags).Where(m => m.Name == name).Select(Signature).ToList();
            return found.Count == 0 ? "NOT FOUND" : string.Join(" | ", found);
        }

        string Signature(MethodInfo method)
        {
            string args = string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name));
            return $"{(method.IsPublic ? "public" : "NON-PUBLIC")}{(method.IsStatic ? " static" : "")} {method.ReturnType.Name} ({args})";
        }
    }
}