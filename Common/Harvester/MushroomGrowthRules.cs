using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Harvester
{
    // Mushroom growth on the Mushroom boxes (the plain planter box and the Harvester Box). Probe 5 (docs/planter_boxes/ROADMAP.md, section 5c) showed vanilla's
    // update only grows mushrooms on mushroom grass (tile 70) and a box is not tile 70, so this class does what vanilla does for it:
    //  - a glowing mushroom (tile 71) on the tile above, like UpdateWorld_GrassGrowth case 70 (a chance, only when that tile is empty);
    //  - a giant mushroom tree: a vanilla tile 5 tree (tree type Mushroom) grown by WorldGen.GrowTree, with the box standing in for mushroom grass through the
    //    Harvester Box disguise (HarvesterBoxTrees). No sapling and no acorn; vanilla's GrowShroom (tile 72) is never called by vanilla and is not used.
    // The clock is vanilla's own random update (TileLoader.RandomUpdate runs for every tile the random update picks), so a box is picked exactly as often as a
    // natural mushroom grass block, and the chances below are PER random update. Vanilla only picks a given tile every few minutes.
    //
    // Multiplayer: the random update only runs on the server and in single-player. A new mushroom is sent with SendTileSquare, GrowTree sends its own tiles
    // on a server. The PlantCheck hook reads tile data only, so it behaves the same on every side (a client frames the mushroom it receives too).
    public static class MushroomGrowthRules
    {
        // ---- Tunables (expression-bodied so hot reload can change them) ----
        public static float MushroomChance => 0.5f;     // a glowing mushroom on the empty tile above, per random update (vanilla mushroom grass: 0.1)
        public static float TreeChance => 0.025f;       // a tree attempt, per random update (vanilla mushroom grass: about 0.0033)
        public static int ClearHalfWidth => 2;          // columns each side of the box whose glowing mushrooms are pushed aside when a tree finds no room

        // True for a plain Mushroom box and for a Mushroom Harvester Box (the tree soil of both is mushroom grass).
        public static bool IsMushroomBox(int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 6))
                return false;

            Tile tile = Main.tile[x, y];
            return tile.HasTile && HarvesterBoxRegistry.BoxTreeSoil.TryGetValue(tile.TileType, out int soil) && soil == TileID.MushroomGrass;
        }

        // One random update of the mushroom box at (x, y). Server and single-player only.
        public static void Step(int x, int y)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient || !IsMushroomBox(x, y) || Main.tile[x, y].IsActuated)
                return;

            if (WorldGen.genRand.NextFloat() < TreeChance && TryTree(x, y))
                return;

            if (!Main.tile[x, y - 1].HasTile && WorldGen.genRand.NextFloat() < MushroomChance)
                PlaceMushroom(x, y);
        }

        static void PlaceMushroom(int x, int y)
        {
            // PlaceTile asks for mushroom grass under a glowing mushroom, so the box stands in for it for this one call.
            HarvesterBoxTrees.RunAsTreeSoil(x, y, () => WorldGen.PlaceTile(x, y - 1, TileID.MushroomPlants, mute: true));

            if (Main.netMode == NetmodeID.Server && Main.tile[x, y - 1].HasTile)
                NetMessage.SendTileSquare(-1, x, y - 1);
        }

        // Vanilla's GrowTree needs the three tiles above the box (and a wide, tall column) empty, and a glowing mushroom is a tile there. So when the first attempt
        // fails and glowing mushrooms stand next to the trunk's place, they are pushed aside (no drops) and the attempt is made once more.
        static bool TryTree(int x, int y)
        {
            bool grew = HarvesterBoxTrees.TryGrowMushroomTree(x, y);
            bool cleared = false;
            if (!grew && ClearMushrooms(x, y))
            {
                cleared = true;
                grew = HarvesterBoxTrees.TryGrowMushroomTree(x, y);
            }

            global::Factorraria.Content.Configs.FurnaceOffsetConfig cfg = ModContent.GetInstance<global::Factorraria.Content.Configs.FurnaceOffsetConfig>();
            if (cfg != null && cfg.EnableDebugs)
                ModContent.GetInstance<MushroomGrowthHooks>().Mod.Logger.Info($"[MushroomGrowth] tree attempt on the box at ({x},{y}): grew={grew}, mushroomsCleared={cleared}");

            return grew;
        }

        static bool ClearMushrooms(int x, int y)
        {
            bool cleared = false;
            for (int xx = x - ClearHalfWidth; xx <= x + ClearHalfWidth; xx++)
            {
                if (!WorldGen.InWorld(xx, y - 1, 1))
                    continue;

                Tile above = Main.tile[xx, y - 1];
                if (!above.HasTile || above.TileType != TileID.MushroomPlants)
                    continue;

                WorldGen.KillTile(xx, y - 1, false, false, true);
                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendTileSquare(-1, xx, y - 1, 1);
                cleared = true;
            }

            return cleared;
        }
    }

    // The clock: vanilla's random update picks tiles and asks every GlobalTile about them (TileLoader.RandomUpdate).
    public class MushroomBoxGlobalTile : GlobalTile
    {
        public override void RandomUpdate(int i, int j, int type)
        {
            if (HarvesterBoxRegistry.BoxTreeSoil.TryGetValue(type, out int soil) && soil == TileID.MushroomGrass)
                MushroomGrowthRules.Step(i, j);
        }
    }

    // Vanilla's PlantCheck (called from TileFrame for a glowing mushroom) breaks tile 71 unless the soil under it is mushroom grass. For the length of that one call
    // a Mushroom box under a glowing mushroom stands in for mushroom grass, like the herb and tree hooks do. Tile data only, so it is the same on every side.
    public class MushroomGrowthHooks : ModSystem
    {
        public override void Load()
        {
            On_WorldGen.PlantCheck += PlantCheckHook;
        }

        public override void Unload()
        {
            On_WorldGen.PlantCheck -= PlantCheckHook;
        }

        static void PlantCheckHook(On_WorldGen.orig_PlantCheck orig, int x, int y)
        {
            if (HarvesterBoxRegistry.BoxTreeSoil.Count > 0 && WorldGen.InWorld(x, y, 2) && Main.tile[x, y].HasTile && Main.tile[x, y].TileType == TileID.MushroomPlants
                && MushroomGrowthRules.IsMushroomBox(x, y + 1))
            {
                HarvesterBoxTrees.Disguise d = HarvesterBoxTrees.Begin(x, y + 1);
                try
                {
                    orig(x, y);
                }
                finally
                {
                    HarvesterBoxTrees.End(d, false);
                }

                return;
            }

            orig(x, y);
        }
    }
}