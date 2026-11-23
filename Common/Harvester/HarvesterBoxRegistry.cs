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
        // Every Harvester Box tile type, whether or not it grows a herb. This (not BoxHerbs) is the "is this a Harvester Box" test.
        public static HashSet<int> BoxTypes = new();

        // Harvester Box tile type -> the herb that grows on it. Only boxes that grow a herb are in here (Mushroom, Hallowed and Stone are not: see NoHerbBoxes).
        public static Dictionary<int, HerbStyle> BoxHerbs = new();

        // Vanilla planter box (tile 380) style -> the herb that grows on it. Built from the planter box ITEMS (found by name), so no style number is hard-coded.
        public static Dictionary<int, HerbStyle> PlanterHerbs = new();

        // Harvester Box tile type -> the vanilla soil tile it stands in for in tree rules (only boxes that grow trees, see HarvesterBoxKinds.TreeSoil).
        public static Dictionary<int, int> BoxTreeSoil = new();

        // The kinds that registered themselves, for ResolvePlanterStyles.
        public static List<HarvesterBoxKind> Kinds = new();

        // Custom box tiles that grow no herb at all: the plain custom planter boxes (Empty, Mushroom, Hallowed, Stone) and the Harvester Boxes of kinds
        // without a herb (Register adds them). See HarvesterBoxRules.HerbAllowedOnSoil.
        public static HashSet<int> NoHerbBoxes = new();

        // Vanilla planter box (tile 380) style -> the tile whose break dust it uses. Built next to PlanterHerbs, from the same planter box items.
        public static Dictionary<int, int> PlanterDustTiles = new();

        // Kind <-> tile, so a box can be turned into another kind (PlanterBoxConversion). The Harvester Boxes fill these in Register; the plain custom boxes
        // (Mushroom, Hallowed, Stone) fill the Plain pair from PlanterBoxTileBase; the vanilla planter box (tile 380) fills the style pair in ResolvePlanterStyles.
        public static Dictionary<HarvesterBoxKind, int> KindTiles = new();
        public static Dictionary<int, HarvesterBoxKind> TileKinds = new();
        public static Dictionary<HarvesterBoxKind, int> PlainKindTiles = new();
        public static Dictionary<int, HarvesterBoxKind> PlainKinds = new();
        public static Dictionary<int, HarvesterBoxKind> PlanterStyleKinds = new();
        public static Dictionary<HarvesterBoxKind, int> KindPlanterStyles = new();

        public static void Register(int tileType, HarvesterBoxKind kind)
        {
            BoxTypes.Add(tileType);
            KindTiles[kind] = tileType;
            TileKinds[tileType] = kind;

            HerbStyle? herb = HarvesterBoxKinds.Herb(kind);
            if (herb.HasValue)
                BoxHerbs[tileType] = herb.Value;
            else
                NoHerbBoxes.Add(tileType);

            int treeSoil = HarvesterBoxKinds.TreeSoil(kind);
            if (treeSoil != 0)
                BoxTreeSoil[tileType] = treeSoil;
            if (!Kinds.Contains(kind))
                Kinds.Add(kind);
        }

        public static void Clear()
        {
            BoxTypes.Clear();
            BoxHerbs.Clear();
            PlanterHerbs.Clear();
            BoxTreeSoil.Clear();
            Kinds.Clear();
            NoHerbBoxes.Clear();
            PlanterDustTiles.Clear();
            KindTiles.Clear();
            TileKinds.Clear();
            PlainKindTiles.Clear();
            PlainKinds.Clear();
            PlanterStyleKinds.Clear();
            KindPlanterStyles.Clear();
        }

        // Vanilla stores a planter box's style in frameY / 18 (WorldGen.PlaceTile sets frameY = 18 * style, KillTile drops item 3215 + frameY / 18),
        // and an item's placeStyle is that style. So the style of each vanilla planter box is the placeStyle of its item.
        public static void ResolvePlanterStyles(Mod mod)
        {
            PlanterHerbs.Clear();
            PlanterDustTiles.Clear();
            PlanterStyleKinds.Clear();
            KindPlanterStyles.Clear();
            foreach (HarvesterBoxKind kind in Kinds)
            {
                string itemName = HarvesterBoxKinds.PlanterItemName(kind);
                HerbStyle? herb = HarvesterBoxKinds.Herb(kind);
                if (itemName == null || herb == null)
                    continue;   // our own boxes (Mushroom, Hallowed, Stone): no vanilla planter box to tie a herb to

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

                PlanterHerbs[sample.placeStyle] = herb.Value;
                PlanterDustTiles[sample.placeStyle] = HarvesterBoxKinds.PlanterDustTile(kind);
                PlanterStyleKinds[sample.placeStyle] = kind;
                KindPlanterStyles[kind] = sample.placeStyle;
            }

            mod.Logger.Info($"[Harvester] {PlanterHerbs.Count} vanilla planter box style(s) tied to a herb");
        }
    }
}