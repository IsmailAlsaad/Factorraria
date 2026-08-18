using Factorraria.Common;
using Factorraria.Common.Interfaces;
using Factorraria.Common.Systems;
using Factorraria.Content.Tiles.Furnace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Factorraria.Content.Tiles.Autohammer
{
    public class GelBurnerTileEntity : ModTileEntity, IElectricProducer
    {
        public Item inputItem = new Item();
        public bool isGenerating { get; set; }
        public bool isWorking { get; set; }
        public float PowerSupply => 500f;

        // later add burn rate based on smelt count for fuel item
        int WorkProgress;
        int WorkDuration = 120; // 60 ticks = 1 second
        //

        public override void Update()
        {
            // isWorking == true is determined when the generator has valid & enough fuel to burn, so count its power output
            // isGenerating == false when grid is overloaded, so stop consuming fuel and turn off, but you could still be working!
            // i.e. have enough fuel to work once the grid is not overloaded
            isWorking = true;

            if (!isGenerating)
            {
                // set sprite to off
                return;
            }



            //TEST
            //WorkProgress++;

            //if (WorkProgress >= WorkDuration)
            //{
            //    isGenerating = !isGenerating;
            //    WorkProgress = 0;
            //}
            //TEST
        }

        public override bool IsTileValidForEntity(int x, int y)
        {
            Tile tile = Framing.GetTileSafely(x, y);
            return tile.HasTile && tile.TileType == TileID.SteampunkBoiler;
        }

        public override int Hook_AfterPlacement(int i, int j, int type, int style, int direction, int alternate)
        {
            // Multiplayer stuff I don't know
            //if (Main.netMode == NetmodeID.MultiplayerClient)
            //{
            //    // Synchronize the 3x2 tile area across the network
            //    NetMessage.SendTileSquare(Main.myPlayer, i, j, 3, 2);
            //    NetMessage.SendData(MessageID.TileEntityPlacement, number: -1, number2: i, number3: j, number4: Type);
            //    return -1;
            //}
            if (type != TileID.SteampunkBoiler)
            {
                return -1;
            }

            return Place(i, j);
        }

        public override void OnKill()
        {
            PowerGridSystem.AllMachines.Remove(this);
            PowerGridSystem.gridNeedsRebuilding = true;
        }

        public override void SaveData(TagCompound tag)
        {
            tag["InputItem"] = inputItem;
        }

        public override void LoadData(TagCompound tag)
        {
            inputItem = tag.Get<Item>("InputItem");
        }
    }
}
