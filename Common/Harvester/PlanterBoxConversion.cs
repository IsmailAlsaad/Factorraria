using Factorraria.Common.Machines;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Harvester
{
    // Turning one planter box into another kind (docs/planter_boxes/ROADMAP.md, Phase 5c). Used by seeds (a seed used on a Blinkroot box) and by spreading (the
    // six seed kinds spread to neighbouring Blinkroot boxes). A box keeps its family: a vanilla planter box stays a vanilla planter box (only its style changes,
    // or it becomes our plain Mushroom / Hallowed box), a Harvester Box stays a Harvester Box (tile swap plus the row bookkeeping). Phase 6 reuses ConvertBox
    // and widens CanConvert (the evil and Hallowed boxes also convert the other kinds).
    //
    // Multiplayer: server and single-player only. ConvertBox writes the tile and sends a tile square; a Harvester Box also queues the row reconcile
    // (HarvesterBoxRowSystem), which makes the new entities and kills the stale one; a client drops a stale replica itself (ClientTick) or has it replaced by the
    // new entity, exactly as after a placed or mined box. The spread clock is vanilla's random update (PlanterBoxSpreadGlobalTile), which only runs on the server
    // and in single-player.
    public static class PlanterBoxConversion
    {
        // ---- Tunables (expression-bodied so hot reload can change them) ----
        public static float SpreadChance => 1f;     // a source box spreads at all, per random update (vanilla picks a given tile about every 9 minutes)
        public static int SpreadRange => 2;         // tiles each way (a square) in which a source box looks for boxes to convert
        public static float ChainChance => 0.5f;    // after one box converted, the chance to convert one more in the same update (vanilla infection: 1 in 2)

        // The kind each seed makes. Grass Seeds are Daybloom's, Jungle Grass Seeds Moonglow's, the Corrupt and Crimson Seeds the two Deathweed boxes', Hallowed Seeds
        // the Hallowed box's and Mushroom Grass Seeds the Mushroom box's (the same seeds the recipes use).
        public static HarvesterBoxKind? SeedKind(int itemType) => itemType switch
        {
            ItemID.GrassSeeds => HarvesterBoxKind.Daybloom,
            ItemID.JungleGrassSeeds => HarvesterBoxKind.Moonglow,
            ItemID.CorruptSeeds => HarvesterBoxKind.Corrupt,
            ItemID.CrimsonSeeds => HarvesterBoxKind.Crimson,
            ItemID.HallowedSeeds => HarvesterBoxKind.Hallowed,
            ItemID.MushroomGrassSeeds => HarvesterBoxKind.Mushroom,
            _ => null
        };

        // The boxes that spread: the six seed kinds.
        public static bool IsSpreadSource(HarvesterBoxKind kind) => kind == HarvesterBoxKind.Daybloom || kind == HarvesterBoxKind.Moonglow || kind == HarvesterBoxKind.Corrupt
            || kind == HarvesterBoxKind.Crimson || kind == HarvesterBoxKind.Hallowed || kind == HarvesterBoxKind.Mushroom;

        // The three infections follow vanilla's infection rules (the Journey mode switch, Plantera, sunflowers).
        public static bool IsInfection(HarvesterBoxKind kind) => kind == HarvesterBoxKind.Corrupt || kind == HarvesterBoxKind.Crimson || kind == HarvesterBoxKind.Hallowed;

        // The one table that says what turns into what. For now a seed kind converts Blinkroot boxes and nothing else; Phase 6 adds the rest of the spec
        // (the evil boxes also convert Waterleaf, Moonglow, Shiverthorn, Mushroom and Stone; the Hallowed box Daybloom, Waterleaf, Shiverthorn and Stone).
        public static bool CanConvert(HarvesterBoxKind source, HarvesterBoxKind target) => IsSpreadSource(source) && target == HarvesterBoxKind.Blinkroot;

        // A seed works only on a Blinkroot box (planter or Harvester).
        public static bool CanConvertBySeed(int x, int y, HarvesterBoxKind seed) => TryGetKind(x, y, out HarvesterBoxKind kind, out _) && CanConvert(seed, kind);

        // The kind of the planter box or Harvester Box at x, y. false when the tile is neither (or a box of no kind: the Empty box).
        public static bool TryGetKind(int x, int y, out HarvesterBoxKind kind, out bool harvester)
        {
            kind = default;
            harvester = false;
            if (!WorldGen.InWorld(x, y, 2))
                return false;

            Tile tile = Main.tile[x, y];
            if (!tile.HasTile)
                return false;

            int type = tile.TileType;
            if (type == TileID.PlanterBox)
                return HarvesterBoxRegistry.PlanterStyleKinds.TryGetValue(tile.TileFrameY / 18, out kind);

            if (HarvesterBoxRegistry.TileKinds.TryGetValue(type, out kind))
            {
                harvester = true;
                return true;
            }

            return HarvesterBoxRegistry.PlainKinds.TryGetValue(type, out kind);
        }

        // Turns the box at x, y into the given kind. Server and single-player only (false on a client, or when there is nothing to do).
        public static bool ConvertBox(int x, int y, HarvesterBoxKind to)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient || !WorldGen.InWorld(x, y, 4))
                return false;

            if (!TryGetKind(x, y, out HarvesterBoxKind from, out bool harvester) || from == to)
                return false;

            Tile tile = Main.tile[x, y];
            if (harvester)
            {
                if (!HarvesterBoxRegistry.KindTiles.TryGetValue(to, out int newTile))
                    return false;

                // Like mining the box: the row's wire switch state is carried to whatever row comes out of this, and an entity sitting exactly on this tile goes.
                bool wireOff = false;
                if (HarvesterBoxRows.TryFindRowEntity(x, y, out TileEntity rowEntity, out _) && rowEntity is BaseMachine rowMachine)
                {
                    wireOff = rowMachine.WireToggle;
                    if (rowMachine.Position.X == x && rowMachine.Position.Y == y)
                        rowMachine.Kill(x, y);
                }

                tile.TileType = (ushort)newTile;
                HarvesterBoxRowSystem.MarkDirty(x, y, wireOff);
            }
            else if (HarvesterBoxRegistry.KindPlanterStyles.TryGetValue(to, out int style))
            {
                // A vanilla planter box: the style lives in frameY / 18 (the same rule PlanterBoxGlobalTile and the herb rules read).
                tile.TileType = (ushort)TileID.PlanterBox;
                tile.TileFrameY = (short)(style * 18);
            }
            else if (HarvesterBoxRegistry.PlainKindTiles.TryGetValue(to, out int plainTile))
            {
                tile.TileType = (ushort)plainTile;
                tile.TileFrameX = 0;
                tile.TileFrameY = 0;
            }
            else
            {
                return false;
            }

            // Re-frames the box and its neighbours (merge frames), and the herb or tree above: a herb that is not the new box's herb breaks through the CheckAlch hook,
            // like a herb that lost its soil.
            WorldGen.SquareTileFrame(x, y);
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendTileSquare(-1, x, y, 3);

            return true;
        }

        // One spread step for the box at x, y, from vanilla's random update. Looks like vanilla's infection spread: it picks a target near the box and, after a
        // success, tries one more with a 1 in 2 chance. Only the target choice differs: it picks among the boxes it can convert, so a step is never wasted on empty air.
        public static void SpreadStep(int x, int y)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient || !TryGetKind(x, y, out HarvesterBoxKind source, out _) || !IsSpreadSource(source))
                return;

            if (WorldGen.genRand.NextFloat() >= SpreadChance)
                return;

            bool infection = IsInfection(source);
            if (infection)
            {
                if (!WorldGen.AllowedToSpreadInfections)
                    return;   // Journey mode's "stop biome spread" switch

                if (NPC.downedPlantBoss && WorldGen.genRand.NextBool(2))
                    return;   // after Plantera vanilla's infection spreads half as often
            }

            int range = System.Math.Max(1, SpreadRange);
            bool again = true;
            while (again)
            {
                again = false;

                List<(int X, int Y)> targets = new();
                for (int dx = -range; dx <= range; dx++)
                {
                    for (int dy = -range; dy <= range; dy++)
                    {
                        if (dx == 0 && dy == 0)
                            continue;

                        if (TryGetKind(x + dx, y + dy, out HarvesterBoxKind other, out _) && CanConvert(source, other))
                            targets.Add((x + dx, y + dy));
                    }
                }

                if (targets.Count == 0)
                    return;

                (int X, int Y) pick = targets[WorldGen.genRand.Next(targets.Count)];
                if (infection && SunflowerNear(pick.X, pick.Y))
                    return;   // vanilla's sunflower protection against infections

                if (ConvertBox(pick.X, pick.Y, source))
                    again = WorldGen.genRand.NextFloat() < ChainChance;
            }
        }

        static bool SunflowerNear(int x, int y)
        {
            for (int i = x - 2; i <= x + 2; i++)
            {
                for (int j = y - 2; j <= y + 2; j++)
                {
                    if (WorldGen.InWorld(i, j, 1) && Main.tile[i, j].HasTile && Main.tile[i, j].TileType == TileID.Sunflower)
                        return true;
                }
            }

            return false;
        }
    }

    // The clock: vanilla's random update picks tiles and asks every GlobalTile about them (TileLoader.RandomUpdate runs for every tile type, the vanilla planter box
    // included). Only server and single-player ever get here.
    public class PlanterBoxSpreadGlobalTile : GlobalTile
    {
        public override void RandomUpdate(int i, int j, int type)
        {
            if (type == TileID.PlanterBox || HarvesterBoxRegistry.BoxTypes.Contains(type) || HarvesterBoxRegistry.PlainKinds.ContainsKey(type))
                PlanterBoxConversion.SpreadStep(i, j);
        }
    }
}