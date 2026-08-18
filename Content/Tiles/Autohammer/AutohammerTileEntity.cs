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
    public class AutohammerTileEntity : ModTileEntity, IElectricConsumer
    {
        public Item inputItem = new Item();
        public bool isPowered { get; set; }
        public bool isWorking { get; set; }
        public float PowerDemand => 100f;
        int WorkProgress;
        int WorkDuration = 120; // 60 ticks = 1 second

        public override void Update()
        {
            // isWorking == true when has valid inputs and can output product
            // isWorking could still be true when isPowered == false, i.e. power grid failed but it can still use energy when on
            // so determine isWorking before returning on !isPower
            isWorking = true;
            if (!isPowered)
            {
                // set sprite to off
                return;
            }

            //TEST
            //WorkProgress++;

            //if(WorkProgress >= WorkDuration)
            //{
            //    isPowered = !isPowered;
            //    WorkProgress = 0;
            //}
            //TEST
        }

        public override bool IsTileValidForEntity(int x, int y)
        {
            Tile tile = Framing.GetTileSafely(x, y);
            return tile.HasTile && tile.TileType == TileID.Autohammer;
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
            if (type != TileID.Autohammer)
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
