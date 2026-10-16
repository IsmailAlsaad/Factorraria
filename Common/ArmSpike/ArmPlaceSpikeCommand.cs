using Factorraria.Content.Tiles.Machines.ElectricalConsumers.RoboticArm;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace Factorraria.Common.ArmSpike
{
    /// <summary>
    /// THROWAWAY spike for Robotic Arm item 4 ("place any placeable item"). Delete this whole folder when the results are in.
    /// Everything works on the tile under your cursor, with a CLONE of the item (stack 5), so your inventory is never touched.
    ///   /armplace info                          print the real signatures of the placement APIs (and every Player.PlaceThing* method)
    ///   /armplace probe [item|all] [dir=..]     read-only: route, TileObjectData, CanPlace for both facings, support, entity overlap
    ///   /armplace place [item] [filters=on|off] [dir=left|right|auto]    really place it through the route the arm would use
    ///   /armplace undo                          remove what the last place created
    /// item = held (default, your selected item) | table | torch | platform | door | wall | sapling | conveyor | an item id
    /// filters=off skips the copied support rules for walls and plain tiles, to show whether vanilla enforces them itself.
    /// Results go to the chat and to the log (client.log, lines starting with [armplace]).
    /// </summary>
    public class ArmPlaceSpikeCommand : ModCommand
    {
        public override string Command => "armplace";
        public override CommandType Type => CommandType.Chat;
        public override string Usage => "/armplace info | probe [item|all] | place [item] [filters=on|off] [dir=left|right|auto] | undo";
        public override string Description => "Robotic Arm item 4 placement spike (throwaway)";

        enum Route { None, Wall, Object, Plain }

        CommandCaller who;
        readonly List<Point> lastTiles = new List<Point>();
        readonly List<Point> lastWalls = new List<Point>();

        void Say(string text, Color? color = null)
        {
            who.Reply(text, color ?? Color.White);
            Mod.Logger.Info("[armplace] " + text);
        }

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            who = caller;
            string sub = args.Length > 0 ? args[0].ToLowerInvariant() : "";

            bool filters = true;
            int dirArg = 0; // 0 = auto
            string spec = "held";
            for (int a = 1; a < args.Length; a++)
            {
                string arg = args[a].ToLowerInvariant();
                if (arg == "filters=off") filters = false;
                else if (arg == "filters=on") filters = true;
                else if (arg == "dir=left") dirArg = -1;
                else if (arg == "dir=right") dirArg = 1;
                else if (arg == "dir=auto") dirArg = 0;
                else spec = arg;
            }

            Point tile = Main.MouseWorld.ToTileCoordinates();
            int dir = dirArg != 0 ? dirArg : (caller.Player.Center.X <= tile.X * 16 + 8 ? 1 : -1);

            try
            {
                switch (sub)
                {
                    case "info":
                        Info();
                        break;

                    case "probe":
                        if (spec == "all")
                        {
                            foreach (string name in new[] { "table", "torch", "platform", "door", "wall", "sapling", "conveyor" })
                            {
                                if (TryResolve(name, out Item each))
                                    Probe(each, tile, dir);
                            }
                        }
                        else if (TryResolve(spec, out Item one))
                            Probe(one, tile, dir);
                        break;

                    case "place":
                        if (TryResolve(spec, out Item item))
                            Place(item, tile, dir, filters);
                        break;

                    case "undo":
                        Undo();
                        break;

                    default:
                        Say(Usage, Color.Orange);
                        break;
                }
            }
            catch (Exception e)
            {
                Say("EXCEPTION " + e, Color.OrangeRed);
            }
        }

        // ------------------------------------------------------------------ item + route helpers

        bool TryResolve(string spec, out Item item)
        {
            item = null;

            if (spec == "held")
            {
                Item held = who.Player.HeldItem;
                if (held == null || held.IsAir)
                {
                    Say("Hold an item, or name one: table, torch, platform, door, wall, sapling, conveyor, or an item id.", Color.OrangeRed);
                    return false;
                }
                item = held.Clone();
            }
            else if (int.TryParse(spec, out int id))
            {
                item = new Item();
                item.SetDefaults(id);
            }
            else
            {
                int type = FindShortcut(spec);
                if (type <= 0)
                {
                    Say($"Unknown item '{spec}'.", Color.OrangeRed);
                    return false;
                }
                item = new Item();
                item.SetDefaults(type);
            }

            item.stack = 5;
            return true;
        }

        // Finds a vanilla item by what it places, not by its (localized) name.
        int FindShortcut(string spec)
        {
            for (int type = 1; type < ItemLoader.ItemCount; type++)
            {
                if (!ContentSamples.ItemsByType.TryGetValue(type, out Item s))
                    continue;

                bool match = spec switch
                {
                    "table" => s.createTile == TileID.Tables && s.placeStyle == 0,
                    "torch" => s.createTile == TileID.Torches && s.placeStyle == 0,
                    "platform" => s.createTile == TileID.Platforms && s.placeStyle == 0,
                    "door" => s.createTile == TileID.ClosedDoor && s.placeStyle == 0,
                    "wall" => s.createWall == WallID.Wood,
                    "sapling" => s.createTile == TileID.Saplings,
                    "conveyor" => s.createTile == TileID.ConveyorBeltLeft,
                    _ => false
                };

                if (match)
                    return type;
            }
            return -1;
        }

        Route Classify(Item item, out TileObjectData data)
        {
            data = null;
            if (item.createWall >= 0)
                return Route.Wall;
            if (item.createTile < 0)
                return Route.None;

            data = TileObjectData.GetTileData(item.createTile, item.placeStyle);
            return data != null ? Route.Object : Route.Plain;
        }

        string Describe(Item item)
        {
            return $"'{item.Name}' type={item.type} createTile={item.createTile} style={item.placeStyle} createWall={item.createWall} consumable={item.consumable}";
        }

        string Anchor(AnchorData a)
        {
            return a.tileCount == 0 && (int)a.type == 0 ? "-" : $"{a.type}x{a.tileCount}";
        }

        string DescribeData(TileObjectData d)
        {
            return $"{d.Width}x{d.Height} origin=({d.Origin.X},{d.Origin.Y}) direction={d.Direction} styleHorizontal={d.StyleHorizontal} "
                + $"anchors bottom={Anchor(d.AnchorBottom)} top={Anchor(d.AnchorTop)} left={Anchor(d.AnchorLeft)} right={Anchor(d.AnchorRight)} wall={d.AnchorWall}";
        }

        // Which players / NPCs overlap a tile rectangle (separately, so player vs NPC refusals can be told apart).
        string EntitiesOver(Rectangle tiles)
        {
            Rectangle world = new Rectangle(tiles.X * 16, tiles.Y * 16, tiles.Width * 16, tiles.Height * 16);
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (p.active && !p.dead && p.Hitbox.Intersects(world))
                    sb.Append("player '" + p.name + "' ");
            }
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC n = Main.npc[i];
                if (n.active && n.Hitbox.Intersects(world))
                    sb.Append("npc '" + n.FullName + "'" + (n.townNPC ? " (town) " : " "));
            }

            return sb.Length == 0 ? "none" : sb.ToString().TrimEnd();
        }

        int EmptyTileRefusals(Rectangle tiles)
        {
            int refused = 0;
            for (int x = tiles.X; x < tiles.X + tiles.Width; x++)
            {
                for (int y = tiles.Y; y < tiles.Y + tiles.Height; y++)
                {
                    if (!Collision.EmptyTile(x, y, true))
                        refused++;
                }
            }
            return refused;
        }

        // Copy of the worker's rule for plain blocks (vanilla BlockPlacementForAssortedThings, final else).
        bool HasSupportCopy(int x, int y)
        {
            if (Framing.GetTileSafely(x, y).WallType > WallID.None)
                return true;

            for (int i = 0; i < 4; i++)
            {
                int nx = x + (i == 0 ? -1 : i == 1 ? 1 : 0);
                int ny = y + (i == 2 ? -1 : i == 3 ? 1 : 0);
                Tile n = Framing.GetTileSafely(nx, ny);

                if (n.WallType > WallID.None)
                    return true;

                if (n.HasTile && (Main.tileSolid[n.TileType] || TileID.Sets.IsBeam[n.TileType] || Main.tileRope[n.TileType] || n.TileType == TileID.MinecartTrack))
                    return true;
            }
            return false;
        }

        // GUESS for walls (the real Player.PlaceThing_Walls rule has not been extracted): a neighbour has a wall or any active tile.
        // Only used with filters=on. With filters=off the spike calls WorldGen.PlaceWall regardless and logs what happened.
        bool WallSupportGuess(int x, int y)
        {
            for (int i = 0; i < 4; i++)
            {
                int nx = x + (i == 0 ? -1 : i == 1 ? 1 : 0);
                int ny = y + (i == 2 ? -1 : i == 3 ? 1 : 0);
                Tile n = Framing.GetTileSafely(nx, ny);
                if (n.WallType > WallID.None || n.HasTile)
                    return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ info

        void Info()
        {
            Say("--- placement API signatures (real, from this tModLoader build) ---", Color.Cyan);

            PrintMethods(typeof(TileObject), "CanPlace", "Place");
            PrintMethods(typeof(TileObjectData), "GetTileData", "CallPostPlacementPlayerHook");
            PrintMethods(typeof(WorldGen), "PlaceTile", "PlaceWall", "PlaceObject", "KillTile", "KillWall");
            PrintMethods(typeof(Collision), "EmptyTile");
            PrintMethods(typeof(TileLoader), "CanPlace", "PlaceInWorld");
            PrintMethods(typeof(ItemLoader), "ConsumeItem");

            Say("TileObject fields: " + string.Join(", ", Array.ConvertAll(typeof(TileObject).GetFields(BindingFlags.Public | BindingFlags.Instance), f => f.FieldType.Name + " " + f.Name)), Color.Cyan);

            Say("--- every Player method starting with PlaceThing (extract these from the decompile next) ---", Color.Cyan);
            foreach (MethodInfo m in typeof(Player).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (m.Name.StartsWith("PlaceThing"))
                    Say("  " + MethodText(m), Color.LightGray);
            }
        }

        void PrintMethods(Type type, params string[] names)
        {
            foreach (string name in names)
            {
                bool any = false;
                foreach (MethodInfo m in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (m.Name != name)
                        continue;
                    any = true;
                    Say("  " + type.Name + "." + MethodText(m), Color.LightGray);
                }
                if (!any)
                    Say("  " + type.Name + "." + name + ": (no such method)", Color.Orange);
            }
        }

        string MethodText(MethodInfo m)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(m.IsPublic ? "public " : "NON-PUBLIC ");
            if (m.IsStatic) sb.Append("static ");
            sb.Append(m.ReturnType.Name).Append(' ').Append(m.Name).Append('(');
            ParameterInfo[] ps = m.GetParameters();
            for (int i = 0; i < ps.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(ps[i].IsOut ? "out " : "").Append(ps[i].ParameterType.Name.TrimEnd('&')).Append(' ').Append(ps[i].Name);
            }
            return sb.Append(')').ToString();
        }

        // ------------------------------------------------------------------ probe (read only)

        void Probe(Item item, Point t, int dir)
        {
            Route route = Classify(item, out TileObjectData data);
            Tile tile = Framing.GetTileSafely(t.X, t.Y);
            Player ghost = ModContent.GetInstance<RoboticArmGhostSystem>().Ghost;

            Say($"=== PROBE {Describe(item)} at ({t.X},{t.Y}) route={route} facing={dir} ===", Color.Cyan);
            Say($"target: hasTile={tile.HasTile} tileType={(tile.HasTile ? tile.TileType : -1)} wall={tile.WallType} liquid={tile.LiquidAmount}");

            Rectangle footprint = new Rectangle(t.X, t.Y, 1, 1);

            switch (route)
            {
                case Route.Object:
                    Say("TileObjectData: " + DescribeData(data));
                    footprint = new Rectangle(t.X - data.Origin.X, t.Y - data.Origin.Y, data.Width, data.Height);
                    Say($"TileLoader.CanPlace={TileLoader.CanPlace(t.X, t.Y, item.createTile)}");
                    foreach (int d in new[] { -1, 1 })
                    {
                        bool ok = TileObject.CanPlace(t.X, t.Y, item.createTile, item.placeStyle, d, out TileObject od, true);
                        Say($"TileObject.CanPlace(dir {d}) = {ok}" + (ok ? $" -> corner ({od.xCoord},{od.yCoord}) alternate {od.alternate} style {od.style}" : ""), ok ? Color.LightGreen : Color.Orange);
                    }
                    break;

                case Route.Plain:
                    Say($"plain tile: solid={Main.tileSolid[item.createTile]} frameImportant={Main.tileFrameImportant[item.createTile]} cut={Main.tileCut[item.createTile]}");
                    Say($"TileLoader.CanPlace={TileLoader.CanPlace(t.X, t.Y, item.createTile)} copiedSupportRule={HasSupportCopy(t.X, t.Y)}");
                    break;

                case Route.Wall:
                    Say($"wall: currentWall={tile.WallType} wallSupportGuess={WallSupportGuess(t.X, t.Y)}");
                    break;

                default:
                    Say("not placeable (no createTile / createWall)", Color.Orange);
                    break;
            }

            int refused = EmptyTileRefusals(footprint);
            Say($"footprint {footprint.Width}x{footprint.Height}: Collision.EmptyTile(ignoreTiles) refuses {refused} tile(s); entities over it: {EntitiesOver(footprint)}");
            Say($"ItemLoader.ConsumeItem(ghost) = {ItemLoader.ConsumeItem(item, ghost)} (consumable={item.consumable})");
        }

        // ------------------------------------------------------------------ place

        void Place(Item item, Point t, int dir, bool filters)
        {
            Route route = Classify(item, out TileObjectData data);
            Player ghost = ModContent.GetInstance<RoboticArmGhostSystem>().Ghost;
            int stackBefore = item.stack;
            bool placed = false;

            lastTiles.Clear();
            lastWalls.Clear();

            Say($"=== PLACE {Describe(item)} at ({t.X},{t.Y}) route={route} facing={dir} filters={(filters ? "on" : "off")} ===", Color.Cyan);

            switch (route)
            {
                case Route.Wall:
                    placed = PlaceWallRoute(item, t, filters);
                    break;
                case Route.Object:
                    placed = PlaceObjectRoute(item, t, dir, data);
                    break;
                case Route.Plain:
                    placed = PlacePlainRoute(item, t, filters);
                    break;
                default:
                    Say("Nothing to place.", Color.Orange);
                    return;
            }

            if (!placed)
            {
                Say($"RESULT placed=False stack {stackBefore} -> {item.stack}", Color.Orange);
                return;
            }

            if (route != Route.Wall)
                TileLoader.PlaceInWorld(t.X, t.Y, item);

            bool hookSaysConsume = ItemLoader.ConsumeItem(item, ghost);
            item.stack--; // the arm rule: always one item, whatever the hook says
            Say($"RESULT placed=True ConsumeItem hook={hookSaysConsume} (arm consumes anyway) stack {stackBefore} -> {item.stack}", Color.LightGreen);
        }

        bool PlaceWallRoute(Item item, Point t, bool filters)
        {
            Tile tile = Framing.GetTileSafely(t.X, t.Y);
            int before = tile.WallType;
            Say($"before: wall={before} hasTile={tile.HasTile} wallSupportGuess={WallSupportGuess(t.X, t.Y)}");

            if (before == item.createWall)
            {
                Say("Target already has that wall.", Color.Orange);
                return false;
            }
            if (filters && !WallSupportGuess(t.X, t.Y))
            {
                Say("Refused by the guessed wall support rule (try filters=off to see whether PlaceWall enforces anything itself).", Color.Orange);
                return false;
            }

            Say($"entities over tile: {EntitiesOver(new Rectangle(t.X, t.Y, 1, 1))}");
            WorldGen.PlaceWall(t.X, t.Y, item.createWall, false);

            int after = Framing.GetTileSafely(t.X, t.Y).WallType;
            Say($"after: wall={after}");
            bool ok = after == item.createWall;
            if (ok)
                lastWalls.Add(t);
            return ok;
        }

        bool PlaceObjectRoute(Item item, Point t, int dir, TileObjectData data)
        {
            Say("TileObjectData: " + DescribeData(data));

            // Vanilla order (PlaceThing_Tiles_PlaceIt): TileLoader.CanPlace, TileObject.CanPlace, then TileObject.Place.
            if (!TileLoader.CanPlace(t.X, t.Y, item.createTile))
            {
                Say("Refused by TileLoader.CanPlace.", Color.Orange);
                return false;
            }
            if (!TileObject.CanPlace(t.X, t.Y, item.createTile, item.placeStyle, dir, out TileObject od, false))
            {
                Say("Refused by TileObject.CanPlace (this is the support / anchor / overlap check for object tiles).", Color.Orange);
                return false;
            }

            TileObjectData placed = TileObjectData.GetTileData(od.type, od.style, od.alternate) ?? data;
            Rectangle footprint = new Rectangle(od.xCoord, od.yCoord, placed.Width, placed.Height);
            Say($"CanPlace ok: corner ({od.xCoord},{od.yCoord}) {footprint.Width}x{footprint.Height} alternate {od.alternate}; EmptyTile refuses {EmptyTileRefusals(footprint)}; entities over it: {EntitiesOver(footprint)}");

            bool ok = TileObject.Place(od);

            int count = 0;
            for (int x = footprint.X; x < footprint.X + footprint.Width; x++)
            {
                for (int y = footprint.Y; y < footprint.Y + footprint.Height; y++)
                {
                    Tile tile = Framing.GetTileSafely(x, y);
                    if (tile.HasTile && tile.TileType == item.createTile)
                        count++;
                }
            }
            Say($"TileObject.Place returned {ok}; {count} of {footprint.Width * footprint.Height} footprint tiles now hold tile {item.createTile}");

            if (count > 0)
                lastTiles.Add(new Point(od.xCoord, od.yCoord));
            return ok && count > 0;
        }

        bool PlacePlainRoute(Item item, Point t, bool filters)
        {
            Tile tile = Framing.GetTileSafely(t.X, t.Y);
            Say($"before: hasTile={tile.HasTile} copiedSupportRule={HasSupportCopy(t.X, t.Y)} TileLoader.CanPlace={TileLoader.CanPlace(t.X, t.Y, item.createTile)}");

            if (filters)
            {
                if (tile.HasTile)
                {
                    Say("Refused: target already has a tile.", Color.Orange);
                    return false;
                }
                if (!TileLoader.CanPlace(t.X, t.Y, item.createTile) || !HasSupportCopy(t.X, t.Y))
                {
                    Say("Refused by TileLoader.CanPlace / the copied support rule.", Color.Orange);
                    return false;
                }
            }

            Say($"EmptyTile refuses {EmptyTileRefusals(new Rectangle(t.X, t.Y, 1, 1))}; entities over tile: {EntitiesOver(new Rectangle(t.X, t.Y, 1, 1))}");

            bool ok = WorldGen.PlaceTile(t.X, t.Y, item.createTile, false, false, -1, item.placeStyle);

            Tile after = Framing.GetTileSafely(t.X, t.Y);
            Say($"WorldGen.PlaceTile returned {ok}; after: hasTile={after.HasTile} type={(after.HasTile ? after.TileType : -1)} frame=({after.TileFrameX},{after.TileFrameY})");

            bool landed = after.HasTile && after.TileType == item.createTile;
            if (landed)
                lastTiles.Add(t);
            return ok && landed;
        }

        // ------------------------------------------------------------------ undo

        void Undo()
        {
            foreach (Point p in lastTiles)
                WorldGen.KillTile(p.X, p.Y, false, false, true);
            foreach (Point p in lastWalls)
                WorldGen.KillWall(p.X, p.Y, false);

            Say($"Undid {lastTiles.Count} tile(s) and {lastWalls.Count} wall(s) from the last place (a wall may drop its item).", Color.LightGreen);
            lastTiles.Clear();
            lastWalls.Clear();
        }
    }
}