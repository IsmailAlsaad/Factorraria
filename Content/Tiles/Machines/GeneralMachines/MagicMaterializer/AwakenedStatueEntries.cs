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

            // MAKE A WAY TO ADD A CONDITION FOR STATUES TO ONLY AWAKEN IN, LIKE HARDMODE OR OTHER CONDITIONS => then uncomment the rest of the statues
            // + GO CHECK THE HARDMODE VARIENTS OF ALL ENEMIES TO INCLUDE IN THE LIST BASED ON THE CONDITION
            // maybe just add another list that gets inputted into registry.Add(...HardmodeAdditions...)?

            // Slime statue
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
            registry.Add(TileID.Statues, style: 4, SlimeStatueNPCIDs,
                spritePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedSlimeStatue",
                outlinePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedSlimeStatue_Outline",
                new Vector2(0,2));

            // Bat statue
            int[] BatStatueNPCIDs = new int[] {
                NPCID.CaveBat,
                NPCID.SporeBat,
                NPCID.JungleBat,
                NPCID.Hellbat,
                NPCID.IceBat
            };
            registry.Add(TileID.Statues, style: 7, BatStatueNPCIDs,
                spritePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedBatStatue",
                outlinePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedBatStatue_Outline",
                new Vector2(0, 2));

            // BloodZombie statue
            int[] BloodZombieStatueNPCIDs = new int[] {
                NPCID.BloodZombie
            };
            registry.Add(TileID.Statues, style: 75, BloodZombieStatueNPCIDs,
                spritePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedBloodZombieStatue",
                outlinePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedBloodZombieStatue_Outline",
                new Vector2(0, 2));

            // Crab statue
            int[] CrabStatueNPCIDs = new int[] {
                NPCID.Crab
            };
            registry.Add(TileID.Statues, style: 18, CrabStatueNPCIDs,
                spritePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedCrabStatue",
                outlinePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedCrabStatue_Outline",
                new Vector2(0, 2));

            // DemonEye statue
            int[] DemonEyeStatueNPCIDs = new int[] {
                NPCID.DemonEye
            };
            registry.Add(TileID.Statues, style: 35, DemonEyeStatueNPCIDs,
                spritePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedDemonEyeStatue",
                outlinePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedDemonEyeStatue_Outline",
                new Vector2(0, 2));

            // Drippler statue
            int[] DripplerStatueNPCIDs = new int[] {
                NPCID.Drippler
            };
            registry.Add(TileID.Statues, style: 65, DripplerStatueNPCIDs,
                spritePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedDripplerStatue",
                outlinePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedDripplerStatue_Outline",
                new Vector2(0, 2));

            // EaterOfSouls statue
            int[] EaterOfSoulsStatueNPCIDs = new int[] {
                NPCID.EaterofSouls,
                NPCID.EaterofSouls,
                NPCID.EaterofSouls,
                NPCID.EaterofSouls,
                NPCID.EaterofSouls,
                NPCID.EaterofSouls,
                
                NPCID.DevourerHead,

                //NPCID.Corruptor // IN HARDMODE
            };
            registry.Add(TileID.Statues, style: 30, EaterOfSoulsStatueNPCIDs,
                spritePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedEaterOfSoulsStatue",
                outlinePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedEaterOfSoulsStatue_Outline",
                new Vector2(0, 2));

            // Goblin statue
            int[] GoblinStatueNPCIDs = new int[] {
                NPCID.GoblinScout,
                NPCID.GoblinArcher,
                NPCID.GoblinPeon,
                NPCID.GoblinSorcerer,
                //NPCID.GoblinSummoner, IN HARDMODE
                NPCID.GoblinThief,
                NPCID.GoblinWarrior
            };
            registry.Add(TileID.Statues, style: 5, GoblinStatueNPCIDs,
                spritePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedGoblinStatue",
                outlinePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedGoblinStatue_Outline",
                new Vector2(0, 2));

            // GraniteGolem statue
            int[] GraniteGolemStatueNPCIDs = new int[] {
                NPCID.GraniteGolem
            };
            registry.Add(TileID.Statues, style: 73, GraniteGolemStatueNPCIDs,
                spritePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedGraniteGolemStatue",
                outlinePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedGraniteGolemStatue_Outline",
                new Vector2(0, 2));

            // Harpy statue
            int[] HarpyStatueNPCIDs = new int[] {
                NPCID.Harpy
            };
            registry.Add(TileID.Statues, style: 70, HarpyStatueNPCIDs,
                spritePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedHarpyStatue",
                outlinePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedHarpyStatue_Outline",
                new Vector2(0, 2));

            // Hoplite statue
            int[] HopliteStatueNPCIDs = new int[] {
                NPCID.GreekSkeleton
            };
            registry.Add(TileID.Statues, style: 72, HopliteStatueNPCIDs,
                spritePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedHopliteStatue",
                outlinePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedHopliteStatue_Outline",
                new Vector2(0, 2));

            // Hornet statue
            int[] HornetStatueNPCIDs = new int[] {
                NPCID.Hornet,
                NPCID.HornetFatty,
                NPCID.HornetHoney,
                NPCID.HornetLeafy,
                NPCID.HornetSpikey,
                NPCID.HornetStingy
                //NPCID.MossHornet IN HARDMODE
            };
            registry.Add(TileID.Statues, style: 16, HornetStatueNPCIDs,
                spritePath: "Factorraria/ContentTiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedHornetStatue",
                outlinePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedHornetStatue_Outline",
                new Vector2(0, 2));

            // Imp statue
            int[] ImpStatueNPCIDs = new int[] {
                NPCID.FireImp
            };
            registry.Add(TileID.Statues, style: 13, ImpStatueNPCIDs,
                spritePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedImpStatue",
                outlinePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedImpStatue_Outline",
                new Vector2(0, 2));

            // Jellyfish statue
            int[] JellyfishStatueNPCIDs = new int[] {
                NPCID.BlueJellyfish,
                NPCID.GreenJellyfish,
                NPCID.PinkJellyfish,
                NPCID.BloodJelly,
                NPCID.FungoFish
            };
            registry.Add(TileID.Statues, style: 23, JellyfishStatueNPCIDs,
                spritePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedJellyfishStatue",
                outlinePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedJellyfishStatue_Outline",
                new Vector2(0, 2));

            // Medusa statue
            int[] MedusaStatueNPCIDs = new int[] {
                NPCID.Medusa
            };
            registry.Add(TileID.Statues, style: 69, MedusaStatueNPCIDs,
                spritePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedMedusaStatue",
                outlinePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedMedusaStatue_Outline",
                new Vector2(0, 2));

            // Mimic statue
            //int[] MimicStatueNPCIDs = new int[] {
            //    NPCID.Mimic,
            //    NPCID.Mimic,
            //    NPCID.Mimic,
            //    NPCID.IceMimic
            //};
            //registry.Add(TileID.Statues, style: 27, MimicStatueNPCIDs,
            //    spritePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedMimicStatue",
            //    outlinePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedMimicStatue_Outline",
            //    new Vector2(0, 2));

            // Pigron statue
            int[] PigronStatueNPCIDs = new int[] {
                NPCID.PigronCorruption,
                NPCID.PigronCrimson,
                NPCID.PigronHallow
            };
            registry.Add(TileID.Statues, style: 71, PigronStatueNPCIDs,
                spritePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedPigronStatue",
                outlinePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedPigronStatue_Outline",
                new Vector2(0, 2));

            // Piranha statue
            int[] PiranhaStatueNPCIDs = new int[] {
                NPCID.Piranha
            };
            registry.Add(TileID.Statues, style: 42, PiranhaStatueNPCIDs,
                spritePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedPiranhaStatue",
                outlinePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedPiranhaStatue_Outline",
                new Vector2(0, 2));

            // Shark statue
            int[] SharkStatueNPCIDs = new int[] {
                NPCID.Shark
            };
            registry.Add(TileID.Statues, style: 50, SharkStatueNPCIDs,
                spritePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedSharkStatue",
                outlinePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedSharkStatue_Outline",
                new Vector2(0, 2));

            // Skeleton statue
            int[] SkeletonStatueNPCIDs = new int[] {
                NPCID.Skeleton,
                NPCID.HeadacheSkeleton,
                NPCID.MisassembledSkeleton,
                NPCID.PantlessSkeleton,
                NPCID.SporeSkeleton,
            };
            registry.Add(TileID.Statues, style: 10, SkeletonStatueNPCIDs,
                spritePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedSkeletonStatue",
                outlinePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedSkeletonStatue_Outline",
                new Vector2(0, 2));

            // BoneSkeleton statue
            int[] BoneSkeletonStatueNPCIDs = SkeletonStatueNPCIDs;
            registry.Add(TileID.Statues, style: 67, BoneSkeletonStatueNPCIDs,
                spritePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedBoneSkeletonStatue",
                outlinePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedBoneSkeletonStatue_Outline",
                new Vector2(0, 2));


            // UndeadViking statue
            int[] UndeadVikingStatueNPCIDs = new int[] {
                NPCID.UndeadViking,
                NPCID.UndeadMiner
            };
            registry.Add(TileID.Statues, style: 68, UndeadVikingStatueNPCIDs,
                spritePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedUndeadVikingStatue",
                outlinePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedUndeadVikingStatue_Outline",
                new Vector2(0, 2));

            // Unicorn statue
            int[] UnicornStatueNPCIDs = new int[] {
                NPCID.Unicorn
            };
            registry.Add(TileID.Statues, style: 64, UnicornStatueNPCIDs,
                spritePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedUnicornStatue",
                outlinePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedUnicornStatue_Outline",
                new Vector2(0, 2));

            // WallCreeper statue
            int[] WallCreeperStatueNPCIDs = new int[] {
                NPCID.WallCreeper,
                NPCID.JungleCreeper,
                NPCID.BloodCrawler
                //NPCID.BlackRecluse IN HARDMODE
            };
            registry.Add(TileID.Statues, style: 63, WallCreeperStatueNPCIDs,
                spritePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedWallCreeperStatue",
                outlinePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedWallCreeperStatue_Outline",
                new Vector2(0, 2));

            // Wraith statue
            int[] WraithStatueNPCIDs = new int[] {
                NPCID.Wraith
            };
            registry.Add(TileID.Statues, style: 66, WraithStatueNPCIDs,
                spritePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedWraithStatue",
                outlinePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedWraithStatue_Outline",
                new Vector2(0, 2));

            // Zombie statue
            int[] ZombieStatueNPCIDs = new int[] {
                NPCID.Zombie,
                NPCID.ArmedZombie,
                NPCID.BaldZombie,
                NPCID.PincushionZombie,
                NPCID.SlimedZombie,
                NPCID.SwampZombie,
                NPCID.TwiggyZombie,
                NPCID.FemaleZombie,
                NPCID.TorchZombie,
                NPCID.ZombieRaincoat,
                NPCID.ZombieEskimo,
                NPCID.ZombieMushroom,
                NPCID.ZombieMushroomHat
                //NPCID.MaggotZombie
                //NPCID.MossZombie
            };
            registry.Add(TileID.Statues, style: 74, ZombieStatueNPCIDs,
                spritePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedZombieStatue",
                outlinePath: "Factorraria/Content/Tiles/Machines/GeneralMachines/MagicMaterializer/Statues/AwakenedZombieStatue_Outline",
                new Vector2(0, 2));

        }
    }
}