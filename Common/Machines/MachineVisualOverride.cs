using Factorraria.Content.Tiles.Machines.Autohammer;
using Factorraria.Content.Tiles.Machines.GelBurner;
using Factorraria.Content.Tiles.Machines.Solidifier;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using static Terraria.GameContent.Bestiary.IL_BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions;

namespace Factorraria.Common.Machines
{
    public class MachineVisualDefinition
    {
        public Func<int, int, BaseMachine> GetEntity;
        public Asset<Texture2D> OnTexture;
        public Asset<Texture2D> OffTexture;
    }

    public static class MachineVisualRegistry
    {
        public static Dictionary<int, MachineVisualDefinition> Definitions = new();

        public static void Register<T>(int tileType, string onPath, string offPath) where T : BaseMachine, new()
        {
            Definitions[tileType] = new MachineVisualDefinition
            {
                GetEntity = (i, j) => TileEntityHelper.GetOrCreateEntity<T>(i, j),
                OnTexture = ModContent.Request<Texture2D>(onPath),
                OffTexture = ModContent.Request<Texture2D>(offPath)
            };
        }
    }

    public class MachineVisualOverride : GlobalTile
    {
        public override void Load()
        {
            MachineVisualRegistry.Register<AutohammerTileEntity>(TileID.Autohammer,
                "Factorraria/Content/Tiles/Machines/Electrical Consumers/Autohammer/Autohammer_On", "Factorraria/Content/Tiles/Machines/Electrical Consumers/Autohammer/Autohammer_Off");

            MachineVisualRegistry.Register<SolidifierTileEntity>(TileID.Solidifier,
                "Factorraria/Content/Tiles/Machines/Electrical Consumers/Solidifier/Solidifier_On", "Factorraria/Content/Tiles/Machines/Electrical Consumers/Solidifier/Solidifier_Off");

            MachineVisualRegistry.Register<GelBurnerTileEntity>(TileID.SteampunkBoiler,
                "Factorraria/Content/Tiles/Machines/Electrical Producers/GelBurner/GelBurner_On", "Factorraria/Content/Tiles/Machines/Electrical Producers/GelBurner/GelBurner_Off");
        }

        public override bool PreDraw(int i, int j, int type, SpriteBatch spriteBatch)
        {
            if (!MachineVisualRegistry.Definitions.TryGetValue(type, out var def))
                return true;

            BaseMachine entity = def.GetEntity(i, j);
            var texture = entity.isOn ? def.OnTexture : def.OffTexture;
            int frame = TileEntityHelper.AnimateTileEntity(spriteBatch, texture.Value, i, j);
            entity.NotifyAnimationFrame(frame);
            return false;
        }

        public override void KillTile(int i, int j, int type, ref bool fail, ref bool effectOnly, ref bool noItem)
        {
            if (!MachineVisualRegistry.Definitions.TryGetValue(type, out var def))
                return;

            TileEntityHelper.TryGetEntityFromTile(i, j, out TileEntity entity, out Point16 position);
            BaseMachine machine = def.GetEntity(position.X, position.Y);

            foreach (var item in machine.InputSlots.Concat(machine.OutputSlots))
            {
                if (!item.IsAir)
                    Item.NewItem(new EntitySource_TileEntity(machine), position.X * 16 + 16, position.Y * 16 + 8, 16, 16, item.type, item.stack);
            }

            machine.Kill(position.X, position.Y);
        }
    }
}
