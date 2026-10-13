using Factorraria.Common.Statues;
using Microsoft.Xna.Framework;
using Terraria.ID;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.MagicMaterializer
{
    // YOUR LIST of awakenable statues. One registry.Add line per statue.
    //
    // How to find the numbers: in-game, hold LEFT ALT and hover the statue; it prints "tile N style M".
    //   tileType   : the statue's TileID (for example TileID.Statues)
    //   style      : the style number from the readout
    //   npcType    : the enemy it spawns (NPCID.X)
    //   spritePath : your saturated statue PNG, same pixel size as the statue (extension left off)
    //   outlinePath: your outline PNG, same canvas size as the sprite
    //   drawOffset : optional pixel nudge, defaults to none
    //
    // A missing PNG only logs a warning: that statue still spawns, it just keeps its vanilla look.
    public static class AwakenedStatueEntries
    {
        public static void Register(AwakenedStatueRegistry registry)
        {
            // registry.Add(TileID.Statues, style: 0, npcType: NPCID.Zombie,
            //     spritePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/Zombie",
            //     outlinePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/Zombie_Outline");
        }
    }
}