using Factorraria.Common.Statues;
using Microsoft.Xna.Framework;
using Terraria.ID;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.MagicMaterializer
{
    public static class AwakenedStatueEntries
    {
        public static void Register(AwakenedStatueRegistry registry)
        {
            // registry.Add(TileID.Statues, style: 0, npcType: NPCID.Zombie,
            //     spritePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/Zombie",
            //     outlinePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/Zombie_Outline");

            // registry.Add(TileID.Statues, style: 1, npcTypes: new[] { NPCID.Zombie, NPCID.DemonEye, NPCID.BlueSlime },
            //     spritePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/Mixed",
            //     outlinePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/Mixed_Outline")
            //     .AddLoot(ItemID.GoldCoin, 0.1f, 1, 2)
            //     .AddLoot(ItemID.Gel, 0.5f, 3, 8);

            int[] SlimeStatueNPCIDs = new int[] {
                NPCID.GreenSlime,
                NPCID.BlueSlime,
                NPCID.RedSlime,
                NPCID.PurpleSlime,
                NPCID.YellowSlime,
                NPCID.BlackSlime,
                NPCID.IceSlime,
                NPCID.SandSlime,
                NPCID.JungleSlime,
                NPCID.SpikedJungleSlime,
                NPCID.SpikedIceSlime,
                NPCID.MotherSlime,
                NPCID.BabySlime,
                NPCID.LavaSlime,
                NPCID.Pinky,
                NPCID.UmbrellaSlime
            };

            // Left-facing style only; the right-facing one (+165) is registered and drawn mirrored automatically.
            registry.Add(TileID.Statues, style: 4, SlimeStatueNPCIDs,
                spritePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedSlimeStatueLeft",
                outlinePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedSlimeStatueLeft_Outline");
        }
    }
}