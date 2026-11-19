using Factorraria.Content.Items.Statues;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace Factorraria.Content.Tiles.Statues
{
    // A 2x3 statue that spawns a Meteor Head every time it gets a wire signal, like the vanilla enemy statues.
    // Left-facing is tile style 0 and right-facing is style 1 (StyleMultiplier 1, alternate placement addAlternate(1)), so the
    // awakened statue registry can tell the two apart (AwakenedStatueEntries passes rightFacingOffset: 1). The item is
    // registered as the drop for every style, so a right-facing statue still drops it.
    // Meteor Heads that come from this statue die without any loot, see MeteorHeadStatueGlobalNPC.
    public class MeteorHeadStatueTile : ModTile
    {
        // Until MeteorHeadStatueTile.png exists the tile borrows the vanilla statue sheet, so the mod loads without the art.
        // Sheet: 72x54, left-facing frame at x 0 and right-facing frame at x 36 (each 2x3 tiles, 16px + 2px gap, last row 18px tall).
        string artPath = "Factorraria/Content/Tiles/Statues/MeteorHeadStatueTile";
        public override string Texture => ModContent.HasAsset(artPath) ? artPath : "Terraria/Images/Tile_" + TileID.Statues;

        public override void SetStaticDefaults()
        {
            Main.tileFrameImportant[Type] = true;
            Main.tileObsidianKill[Type] = true;
            Main.tileSpelunker[Type] = true;
            TileID.Sets.DisableSmartCursor[Type] = true;
            TileID.Sets.IsAMechanism[Type] = true;   // keeps this tile and its pressure plate when worldgen removes broken traps

            // Vanilla loot gate for statue-spawned enemies: with a drop rarity of 0 a Meteor Head that has SpawnedFromStatue set never
            // drops anything. Natural Meteor Heads and awakened ones (which never get the flag) keep their normal loot, and OnKill hooks
            // (the awakened statue loot list) are untouched, unlike with a PreKill that returns false.
            NPCID.Sets.StatueSpawnedDropRarity[NPCID.MeteorHead] = 0f;

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2xX);
            TileObjectData.newTile.DrawYOffset = 2;
            TileObjectData.newTile.StyleHorizontal = true;
            TileObjectData.newTile.StyleMultiplier = 1;
            TileObjectData.newTile.Direction = TileObjectDirection.PlaceLeft;
            TileObjectData.newAlternate.CopyFrom(TileObjectData.newTile);
            TileObjectData.newAlternate.Direction = TileObjectDirection.PlaceRight;
            TileObjectData.addAlternate(1);
            TileObjectData.addTile(Type);

            DustType = DustID.Stone;
            AddMapEntry(new Color(144, 148, 144), CreateMapEntryName());
            RegisterItemDrop(ModContent.ItemType<MeteorHeadStatueItem>());   // no style list = fallback for style 0 and 1
        }

        // Runs on the server / in singleplayer (wires are simulated there). Same rules as the vanilla statues: a cooldown per statue
        // (CheckMech) and a cap on nearby spawns (NPC.MechSpawn).
        public override void HitWire(int i, int j)
        {
            TileObjectData data = TileObjectData.GetTileData(Main.tile[i, j]);
            if (data == null) return;

            Point16 origin = TileObjectData.TopLeft(i, j);
            int x = origin.X;
            int y = origin.Y;

            // Skip every tile of the statue so one signal can't run this several times.
            for (int yy = y; yy < y + data.Height; yy++)
            {
                for (int xx = x; xx < x + data.Width; xx++)
                    Wiring.SkipWire(xx, yy);
            }

            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            // The statue looks shorter than its 3 tiles, hence 0.65 for the height.
            float spawnX = (x + data.Width * 0.5f) * 16;
            float spawnY = (y + data.Height * 0.65f) * 16;

            if (!Wiring.CheckMech(x, y, 30) || !NPC.MechSpawn(spawnX, spawnY, NPCID.MeteorHead)) return;

            int index = NPC.NewNPC(new EntitySource_TileUpdate(x, y, "MeteorHeadStatue"), (int)spawnX, (int)spawnY - 12, NPCID.MeteorHead);
            if (index < 0 || index >= Main.maxNPCs) return;

            NPC npc = Main.npc[index];
            npc.value = 0f;                        // no coins
            npc.npcSlots = 0f;                     // doesn't suppress natural spawns
            npc.SpawnedFromStatue = true;          // same flag the vanilla statues set
            npc.CanBeReplacedByOtherNPCs = true;   // can't fill the NPC array and block real spawns
        }
    }
}