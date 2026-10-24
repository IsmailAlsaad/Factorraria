using System;
using System.Collections.Generic;
using Factorraria.Common.Carts;
using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Factorraria.Content.Items.Materials;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace Factorraria.Common.WorldGeneration
{
    /// <summary>
    /// Biome / depth specific part of the abandoned cart generator: which cart skins, which chest, which loot, what a terrarium holds.
    /// Biome is read from the tiles around the track (Jungle / Desert / Snow, else plain), depth from the world layers
    /// (surface, underground + cavern, lava layer). The biome wins over the depth for cart types, chests and loot,
    /// except that snow only decides chest and loot (its cart types follow the depth).
    /// </summary>
    public partial class CartWorldGen
    {
        public int BiomeScanRadius = 30;            // tiles around the track tile that are counted
        public int BiomeMinTiles = 150;             // biome tiles needed inside that box for the biome to count

        public double DesertAmberChance = 0.30;     // desert carriage is an Amber Minecart instead of a Desert Minecart
        public double HellcartChance = 0.50;        // lava layer motor is a Demonic Hellcart instead of a Steampunk
        public double ShroomCartChance = 0.20;      // underground carriage is a Shroom Minecart
        public double GemCartChance = 0.10;         // underground carriage is a gem minecart (random of the six)
        public double GoldChestShare2 = 0.5;        // wooden-or-gold / ivy-or-gold choices: chance of the gold one

        public double TerrariumEmptyChance = 1.0 / 3.0;   // terrarium cart: empty
        public double TerrariumWaterChance = 1.0 / 3.0;   // water (the rest is lava)
        public double JungleHoneyChance = 0.5;            // in the jungle: honey or empty
        public int TerrariumMaxAmount = 1000;             // a filled tank holds 1..this

        private enum CartBiome { Forest, Jungle, Desert, Snow }
        private enum CartDepth { Surface, Underground, LavaLayer }

        private struct CartSpot
        {
            public CartBiome Biome;
            public CartDepth Depth;
            public bool BelowRockLayer; // the cavern layer (the lava layer counts too); false = the dirt layer or above
        }

        // One line of a loot table: Slots slots, each one rolls Chance and, on a hit, takes a random item of Pool with Min..Max items.
        private class LootEntry
        {
            public int[] Pool;
            public int Min;
            public int Max;
            public double Chance;
            public int Slots = 1;
            public bool Distinct = true; // the slots of this entry never repeat an item
        }

        private CartSpot ClassifySpot(Point tile)
        {
            CartSpot spot = new CartSpot();

            int lavaLine = GenVars.lavaLine > 0 ? GenVars.lavaLine : (int)((Main.rockLayer + Main.maxTilesY) / 2.0);
            if (tile.Y < Main.worldSurface)
                spot.Depth = CartDepth.Surface;
            else if (tile.Y >= lavaLine)
                spot.Depth = CartDepth.LavaLayer;
            else
                spot.Depth = CartDepth.Underground;

            spot.BelowRockLayer = tile.Y >= Main.rockLayer;

            int jungle = 0, snow = 0, desert = 0;
            int r = BiomeScanRadius;
            int x0 = Math.Max(1, tile.X - r), x1 = Math.Min(Main.maxTilesX - 2, tile.X + r);
            int y0 = Math.Max(1, tile.Y - r), y1 = Math.Min(Main.maxTilesY - 2, tile.Y + r);

            for (int x = x0; x <= x1; x++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    Tile t = Main.tile[x, y];
                    if (!t.HasTile)
                        continue;

                    switch (t.TileType)
                    {
                        case TileID.JungleGrass:
                        case TileID.Mud:
                            jungle++;
                            break;
                        case TileID.SnowBlock:
                        case TileID.IceBlock:
                        case TileID.Slush:
                            snow++;
                            break;
                        case TileID.Sand:
                        case TileID.HardenedSand:
                        case TileID.Sandstone:
                        case TileID.FossilOre:
                            desert++;
                            break;
                    }
                }
            }

            spot.Biome = CartBiome.Forest;
            int best = BiomeMinTiles - 1;
            if (jungle > best) { best = jungle; spot.Biome = CartBiome.Jungle; }
            if (snow > best) { best = snow; spot.Biome = CartBiome.Snow; }
            if (desert > best) { best = desert; spot.Biome = CartBiome.Desert; }
            return spot;
        }

        #region Cart skins

        private int PickMotorSkin(CartSpot spot)
        {
            if (spot.Biome == CartBiome.Jungle)
                return ItemID.BeeMinecart;

            if (spot.Biome != CartBiome.Desert && spot.Depth == CartDepth.LavaLayer && WorldGen.genRand.NextDouble() < HellcartChance)
                return ItemID.HellMinecart;

            return ItemID.SteampunkMinecart;
        }

        private int PickCarriageSkin(CartSpot spot)
        {
            if (spot.Biome == CartBiome.Jungle)
                return ItemID.SunflowerMinecart;

            if (spot.Biome == CartBiome.Desert)
                return WorldGen.genRand.NextDouble() < DesertAmberChance ? ItemID.AmberMinecart : ItemID.DesertMinecart;

            if (spot.Depth == CartDepth.Underground)
            {
                double roll = WorldGen.genRand.NextDouble();
                if (roll < ShroomCartChance)
                    return ItemID.ShroomMinecart;

                if (roll < ShroomCartChance + GemCartChance)
                {
                    int[] gemCarts = { ItemID.AmethystMinecart, ItemID.TopazMinecart, ItemID.SapphireMinecart, ItemID.EmeraldMinecart, ItemID.RubyMinecart, ItemID.DiamondMinecart };
                    return gemCarts[WorldGen.genRand.Next(gemCarts.Length)];
                }
            }

            return ItemID.Minecart;
        }

        #endregion

        #region Spawning

        private void SpawnSolo(Point tile, Vector2 railPoint, int facing)
        {
            CartSpot spot = ClassifySpot(tile);
            int skin = PickCarriageSkin(spot);

            Cart cart = CartSystem.SpawnCart(railPoint, skin);
            cart.Facing = facing;
            cart.PlacedByPlayer = false;
            RollCargo(cart, spot);
            statSolo++;

            Mod.Logger.Info("[CartGen] solo at tile " + tile.X + "," + tile.Y + " biome=" + spot.Biome + " depth=" + spot.Depth + " skin=" + ItemID.Search.GetName(skin) + " module=" + cart.Module);
        }

        private void SpawnLinked(Point tile, Vector2 railPoint, int facing)
        {
            CartSpot spot = ClassifySpot(tile);
            int motorSkin = PickMotorSkin(spot);
            int carriageSkin = PickCarriageSkin(spot);

            // The motor is out of fuel / liquid: SpawnCart gives a motor skin its empty fuel slot or empty tank.
            Cart motor = CartSystem.SpawnCart(railPoint, motorSkin);
            motor.Facing = facing;
            motor.PlacedByPlayer = false;

            Cart carriage = CartSystem.SpawnCart(railPoint, carriageSkin);
            carriage.Facing = facing;
            carriage.PlacedByPlayer = false;
            RollCargo(carriage, spot);

            // The new chain backfills its trail from the rail behind the motor and Attach snaps the carriage into slot 1.
            CartChain chain = new CartChain(motor);
            chain.Attach(carriage, false);
            statLinked++;

            Mod.Logger.Info("[CartGen] linked at tile " + tile.X + "," + tile.Y + " biome=" + spot.Biome + " depth=" + spot.Depth + " motor=" + ItemID.Search.GetName(motorSkin)
                + " carriage=" + ItemID.Search.GetName(carriageSkin) + " module=" + carriage.Module);
        }

        private void RollCargo(Cart cart, CartSpot spot)
        {
            double roll = WorldGen.genRand.NextDouble();

            if (roll < ChestChance)
            {
                cart.SetModule(CartModule.Chest, PickChestItem(cart, spot));
                FillChest(cart, spot);
                statChest++;
            }
            else if (roll < ChestChance + TerrariumChance)
            {
                cart.SetModule(CartModule.Terrarium, ItemID.Terrarium);
                FillTerrarium(cart, spot);
                statTerrarium++;
            }
        }

        private int PickChestItem(Cart cart, CartSpot spot)
        {
            if (cart.SkinType == ItemID.ShroomMinecart)
                return ItemID.MushroomChest;

            if (spot.Biome == CartBiome.Snow)
                return ItemID.IceChest;

            if (spot.Biome == CartBiome.Desert)
                return ItemID.DesertChest;

            bool gold = WorldGen.genRand.NextDouble() < GoldChestShare2;

            if (spot.Biome == CartBiome.Jungle)
                return gold ? ItemID.GoldChest : ItemID.IvyChest;

            int randomIndex = Main.rand.Next(0, 3);
            int[] ChestOptions = new int[] { ItemID.ShadowChest, ItemID.GoldChest, ItemID.Chest };
            if (spot.Depth == CartDepth.LavaLayer)
                return ChestOptions[randomIndex];

            return gold ? ItemID.GoldChest : ItemID.Chest;
        }

        /// <summary>
        /// The water a terrarium cart holds here, using the same biome waters as the machines (LiquidTypeRegistry.WaterFor):
        /// snow = Snow Water, desert = Desert Water underground / Oasis Water on the surface, and otherwise by layer:
        /// Cavern Water below the rock layer, Underground Water in the dirt layer, plain Water on the surface (forest has no variant).
        /// </summary>
        private int BiomeWater(CartSpot spot)
        {
            MachineBiome biome;

            if (spot.Biome == CartBiome.Snow)
                biome = MachineBiome.Snow;
            else if (spot.Biome == CartBiome.Desert)
                biome = spot.Depth == CartDepth.Surface ? MachineBiome.Desert : MachineBiome.UndergroundDesert;
            else if (spot.Depth == CartDepth.Surface)
                biome = MachineBiome.Forest;
            else
                biome = spot.BelowRockLayer ? MachineBiome.Cavern : MachineBiome.Underground;

            return LiquidTypeRegistry.WaterFor(biome);
        }

        private void FillTerrarium(Cart cart, CartSpot spot)
        {
            double roll = WorldGen.genRand.NextDouble();
            int liquid = -1;

            if (spot.Biome == CartBiome.Jungle)
            {
                if (roll < JungleHoneyChance)
                    liquid = LiquidTypeRegistry.Honey;
            }
            else if (roll >= TerrariumEmptyChance)
            {
                liquid = roll < TerrariumEmptyChance + TerrariumWaterChance ? BiomeWater(spot) : LiquidTypeRegistry.Lava;
            }

            if (liquid < 0)
                return;

            cart.Tank.LiquidType = liquid;
            cart.Tank.Amount = WorldGen.genRand.Next(1, Math.Max(1, TerrariumMaxAmount) + 1);
            cart.NormalizeTank();
        }

        #endregion

        #region Chest loot

        private void FillChest(Cart cart, CartSpot spot)
        {
            List<LootEntry> table = BuildLootTable(spot);
            List<Item> items = new List<Item>();

            // A chest cart always holds something: reroll until at least one slot hit.
            for (int attempt = 0; attempt < 500 && items.Count == 0; attempt++)
                items = RollTable(table);

            if (items.Count == 0)
                items.Add(MakeItem(ItemID.Coal, WorldGen.genRand.Next(8, 21)));

            for (int i = 0; i < items.Count && i < cart.ChestItems.Length; i++)
                cart.ChestItems[i] = items[i];
        }

        private List<Item> RollTable(List<LootEntry> table)
        {
            List<Item> result = new List<Item>();

            for (int e = 0; e < table.Count; e++)
            {
                LootEntry entry = table[e];
                List<int> available = new List<int>(entry.Pool);

                for (int s = 0; s < entry.Slots && available.Count > 0; s++)
                {
                    if (WorldGen.genRand.NextDouble() >= entry.Chance)
                        continue;

                    int index = WorldGen.genRand.Next(available.Count);
                    int type = available[index];
                    if (entry.Distinct)
                        available.RemoveAt(index);

                    result.Add(MakeItem(type, WorldGen.genRand.Next(entry.Min, entry.Max + 1)));
                }
            }

            return result;
        }

        private static Item MakeItem(int type, int stack)
        {
            Item item = new Item();
            item.SetDefaults(type);
            item.stack = Math.Max(1, Math.Min(stack, item.maxStack));
            return item;
        }

        private static void Add(List<LootEntry> table, int type, int min, int max, double chance)
        {
            table.Add(new LootEntry { Pool = new[] { type }, Min = min, Max = max, Chance = chance });
        }

        private static void AddPool(List<LootEntry> table, int[] pool, int slots, int min, int max, double chance)
        {
            table.Add(new LootEntry { Pool = pool, Slots = slots, Min = min, Max = max, Chance = chance });
        }

        /// <summary>Each line is its own slot (or slots) and stacks with every other line, so a lucky roll can fill the whole chest. Coal is on every table.</summary>
        private List<LootEntry> BuildLootTable(CartSpot spot)
        {
            List<LootEntry> t = new List<LootEntry>();
            int[] gems = { ItemID.Amethyst, ItemID.Topaz, ItemID.Sapphire, ItemID.Emerald, ItemID.Ruby, ItemID.Diamond };
            int[] ores =
                {
                    ItemID.TinOre, ItemID.CopperOre, ItemID.IronOre, ItemID.LeadOre, ItemID.TungstenOre,
                    ItemID.SilverOre, ItemID.GoldOre, ItemID.PlatinumOre, ModContent.ItemType<SiliconOreItem>()
                };

            if (spot.Biome == CartBiome.Snow)
            {
                AddPool(t, new int[] { ItemID.TungstenOre, ItemID.SilverOre, ItemID.PlatinumOre }, 2, OreMin, OreMax, 1.0);
            }
            else if (spot.Biome == CartBiome.Jungle)
            {
                Add(t, ItemID.Vine, 2, 4, 0.5);
                Add(t, ItemID.JungleSpores, 8, 13, 0.7);
                Add(t, ItemID.MudBlock, 30, 60, 0.9);
                Add(t, ItemID.HoneyBlock, 3, 12, 0.1);
            }
            else if (spot.Biome == CartBiome.Desert)
            {
                Add(t, ItemID.FossilOre, 8, 16, 0.5);
                Add(t, ItemID.DesertFossil, 30, 60, 0.9);
                Add(t, ItemID.CopperOre, 8, 20, 0.4);
                Add(t, ItemID.GoldOre, 8, 20, 0.1);
                Add(t, ItemID.Amber, 8, 20, 0.1);
            }
            else if (spot.Depth == CartDepth.LavaLayer)
            {
                Add(t, ItemID.SiltBlock, 30, 60, 0.9);
                AddPool(t, gems, 2, 3, 10, 0.1);
                AddPool(t, ores, OreSlots, OreMin, OreMax, 0.4);
            }
            else
            {
                Add(t, ItemID.SiltBlock, 30, 60, 0.9);
                Add(t, ItemID.StoneBlock, 30, 60, 0.7);

                AddPool(t, ores, OreSlots, OreMin, OreMax, 1.0);
            }

            Add(t, ItemID.Coal, 8, 20, 0.1);
            return t;
        }

        #endregion
    }
}