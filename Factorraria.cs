using System;
using System.Collections.Generic;
using System.IO;
using Factorraria.Common.Net;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.ModLoader;

namespace Factorraria
{
	// Please read https://github.com/tModLoader/tModLoader/wiki/Basic-tModLoader-Modding-Guide#mod-skeleton-contents for more information about the various files in a mod.
	public class Factorraria : Mod
	{
		// All Factorraria packets go through the router in Common/Net (roadmap patch 1.1).
		public override void HandlePacket(BinaryReader reader, int whoAmI)
		{
			FactorrariaNet.Handle(reader, whoAmI);
		}

		
	}
}
