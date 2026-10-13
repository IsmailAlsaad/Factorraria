using Factorraria.Common.UI;
using Factorraria.Content.Configs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.MagicMaterializer
{
    public class MagicMaterializerUIState : MachineUIStateBase
    {
        public override Vector2 BasePanelOffset => new Vector2(-30, -60);

        protected override List<MachineUIElementEntry> BuildElements()
        {
            var list = new List<MachineUIElementEntry>();

            list.Add(FuelTankEntry(0, new Vector2(0, 40)));

            return list;
        }
    }
}