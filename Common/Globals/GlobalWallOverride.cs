using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.ModLoader;

namespace Factorraria.Common.Globals
{
    public class GlobalWallOverride : GlobalWall
    {
        public override bool Drop(int i, int j, int type, ref int dropType)
        {
            //return false;
            return base.Drop(i, j, type, ref dropType);
        }
    }
}
