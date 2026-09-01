using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Factorraria.Common.Machines
{
    public abstract class BaseMachine : ModTileEntity
    {
        public abstract int ValidTileType { get; }
        public bool isOn;
        public bool isWorking { get; protected set; }
        protected int WorkProgress;
        protected virtual int WorkDuration => 120; // 60 ticks = 1 second, override per machine

        protected virtual int InputSlotCount => 0;
        protected virtual int OutputSlotCount => 0;

        Item[] inputSlots;
        Item[] outputSlots;
        public Item[] InputSlots => inputSlots ??= CreateItemArray(InputSlotCount);
        public Item[] OutputSlots => outputSlots ??= CreateItemArray(OutputSlotCount);

        static Item[] CreateItemArray(int count)
        {
            var arr = new Item[count];
            for (int i = 0; i < count; i++) arr[i] = new Item();
            return arr;
        }

        public override bool IsTileValidForEntity(int x, int y)
        {
            Tile tile = Framing.GetTileSafely(x, y);
            return tile.HasTile && tile.TileType == ValidTileType;
        }

        public override int Hook_AfterPlacement(int i, int j, int type, int style, int direction, int alternate)
        {
            // Multiplayer stuff I don't know
            //if (Main.netMode == NetmodeID.MultiplayerClient)
            //{
            //    // Synchronize the 3x2 tile area across the network // should somehow make it per tileArea
            //    NetMessage.SendTileSquare(Main.myPlayer, i, j, 3, 2);
            //    NetMessage.SendData(MessageID.TileEntityPlacement, number: -1, number2: i, number3: j, number4: Type);
            //    return -1;
            //}

            if (type != ValidTileType) return -1;
            return Place(i, j);
        }

        public override void SaveData(TagCompound tag)
        {
            for (int i = 0; i < InputSlots.Length; i++) tag[$"Input{i}"] = InputSlots[i];
            for (int i = 0; i < OutputSlots.Length; i++) tag[$"Output{i}"] = OutputSlots[i];
        }

        public override void LoadData(TagCompound tag)
        {
            for (int i = 0; i < InputSlots.Length; i++) InputSlots[i] = tag.Get<Item>($"Input{i}");
            for (int i = 0; i < OutputSlots.Length; i++) OutputSlots[i] = tag.Get<Item>($"Output{i}");
        }
    }
}
