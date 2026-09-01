using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria.ID;

namespace Factorraria.Common.Networks
{
    public class PriorityConveyorNetwork
    {
        public HashSet<Point> Tiles { get; private set; } = new HashSet<Point>();
        public int FilteredItemId { get; set; } = ItemID.None;

        public PriorityConveyorNetwork() { }

        public PriorityConveyorNetwork(int filterItemId)
        {
            FilteredItemId = filterItemId;
        }
    }
}