using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Harvester
{
    // What the herb rules need to know about planter boxes: which herb each Harvester Box tile and each vanilla planter box style allows.
    // Filled when the tiles load (HarvesterBoxTileBase.SetStaticDefaults) and in PostSetupContent (ResolvePlanterStyles), emptied on unload.
    public static class HarvesterBoxRegistry
    {
        // Harvester Box tile type -> the herb that grows on it.
        public static Dictionary<int, HerbStyle> BoxHerbs = new();

        // Vanilla planter box (tile 380) style -> the herb that grows on it. Built from the planter box ITEMS (found by name), so no style number is hard-coded.
        public static Dictionary<int, HerbStyle> PlanterHerbs = new();

        // Harvester Box tile type -> the vanilla soil tile it stands in for in tree rules (only boxes that grow trees, see HarvesterBoxKinds.TreeSoil).
        public static Dictionary<int, int> BoxTreeSoil = new();

        // The kinds that registered themselves, for ResolvePlanterStyles.
        public static List<HarvesterBoxKind> Kinds = new();

        // Plain custom planter box tiles that grow no herb at all (the Empty Planter Box). See HarvesterBoxRules.HerbAllowedOnSoil.
        public static HashSet<int> NoHerbBoxes = new();

        // Vanilla planter box (tile 380) style -> the tile whose break dust it uses. Built next to PlanterHerbs, from the same planter box items.
        public static Dictionary<int, int> PlanterDustTiles = new();

        public static void Register(int tileType, HarvesterBoxKind kind)
        {
            BoxHerbs[tileType] = HarvesterBoxKinds.Herb(kind);

            int treeSoil = HarvesterBoxKinds.TreeSoil(kind);
            if (treeSoil != 0)
                BoxTreeSoil[tileType] = treeSoil;
            if (!Kinds.Contains(kind))
                Kinds.Add(kind);
        }

        public static void Clear()
        {
            BoxHerbs.Clear();
            PlanterHerbs.Clear();
            BoxTreeSoil.Clear();
            Kinds.Clear();
            NoHerbBoxes.Clear();
            PlanterDustTiles.Clear();
        }

        // Vanilla stores a planter box's style in frameY / 18 (WorldGen.PlaceTile sets frameY = 18 * style, KillTile drops item 3215 + frameY / 18),
        // and an item's placeStyle is that style. So the style of each vanilla planter box is the placeStyle of its item.
        public static void ResolvePlanterStyles(Mod mod)
        {
            PlanterHerbs.Clear();
            PlanterDustTiles.Clear();
            foreach (HarvesterBoxKind kind in Kinds)
            {
                string itemName = HarvesterBoxKinds.PlanterItemName(kind);
                if (!ItemID.Search.TryGetId(itemName, out int itemType))
                {
                    mod.Logger.Warn($"[Harvester] vanilla item '{itemName}' not found: the {kind} planter box keeps the vanilla rules (any herb).");
                    continue;
                }

                Item sample = new Item();
                sample.SetDefaults(itemType);
                if (sample.createTile != TileID.PlanterBox)
                {
                    mod.Logger.Warn($"[Harvester] '{itemName}' does not place the planter box tile (createTile {sample.createTile}): the {kind} planter box keeps the vanilla rules (any herb).");
                    continue;
                }

                PlanterHerbs[sample.placeStyle] = HarvesterBoxKinds.Herb(kind);
                PlanterDustTiles[sample.placeStyle] = HarvesterBoxKinds.PlanterDustTile(kind);
            }

            mod.Logger.Info($"[Harvester] {PlanterHerbs.Count} vanilla planter box style(s) tied to a herb");
        }
    }
}