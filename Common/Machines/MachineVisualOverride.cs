using Factorraria.Common.Liquids;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace Factorraria.Common.Machines
{
    public class LiquidOverlayLayer
    {
        public Asset<Texture2D> SourceTexture;
        public Func<BaseMachine, LiquidStack> GetLiquidStack;
        public string CacheKey;
    }

    public class MachineVisualDefinition
    {
        public Func<int, int, BaseMachine> GetEntity;
        public Asset<Texture2D> OnTexture;
        public Asset<Texture2D> OffTexture;
        public List<LiquidOverlayLayer> LiquidOverlays = new();
    }

    public static class MachineVisualRegistry
    {
        public static Dictionary<int, MachineVisualDefinition> Definitions = new();

        public static void Register<T>(int tileType, string onPath, string offPath) where T : BaseMachine, new()
        {
            if (tileType <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tileType),
                    $"Failed to register {typeof(T).Name}: Tile ID was {tileType}. " +
                    $"Ensure registration runs in ModSystem.PostSetupContent()."
                );
            }

            Definitions[tileType] = new MachineVisualDefinition
            {
                GetEntity = (i, j) => TileEntityHelper.GetOrCreateEntity<T>(i, j),
                OnTexture = ModContent.Request<Texture2D>(onPath),
                OffTexture = ModContent.Request<Texture2D>(offPath)
            };
        }
        public static void RegisterLiquidOverlay(int tileType, string texturePath, Func<BaseMachine, LiquidStack> getLiquidStack)
        {
            if (!Definitions.TryGetValue(tileType, out var def))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tileType),
                    $"RegisterLiquidOverlay failed for tile type {tileType}: call MachineVisualRegistry.Register (base On/Off textures) first."
                );
            }

            def.LiquidOverlays.Add(new LiquidOverlayLayer
            {
                SourceTexture = ModContent.Request<Texture2D>(texturePath),
                GetLiquidStack = getLiquidStack,
                CacheKey = texturePath // already unique per overlay, no need to invent a separate key
            });
        }
    }

    public class MachineVisualOverride : GlobalTile
    {
        public override bool PreDraw(int i, int j, int type, SpriteBatch spriteBatch)
        {
            if (!MachineVisualRegistry.Definitions.TryGetValue(type, out var def))
                return true;

            BaseMachine entity = def.GetEntity(i, j);
            var texture = entity.isOn ? def.OnTexture : def.OffTexture;

            float rotation = entity is MotorTileEntityBase motor ? GetMotorRotation(motor.Facing) : 0f;

            int frame = TileEntityHelper.AnimateTileEntity(spriteBatch, texture.Value, i, j, rotation);

            if (entity.isOn)
            {
                foreach (var layer in def.LiquidOverlays)
                {
                    LiquidStack stack = layer.GetLiquidStack(entity);
                    if (stack == null || stack.IsEmpty)
                        continue;

                    LiquidTypeDefinition liquidDef = LiquidTypeRegistry.Get(stack.LiquidType);
                    Texture2D recolored = LiquidTextureCache.GetOrCreate(layer.CacheKey, layer.SourceTexture.Value, liquidDef);

                    TileEntityHelper.AnimateTileEntity(spriteBatch, recolored, i, j, rotation);
                }
            }

            entity.NotifyAnimationFrame(frame);
            return false;
        }

        // Assumes the motor art is drawn facing Right at 0 rotation — Facing's default value.
        static float GetMotorRotation(Direction facing) => facing switch
        {
            Direction.Right => 0f,
            Direction.Down => MathHelper.PiOver2,
            Direction.Left => MathHelper.Pi,
            Direction.Up => -MathHelper.PiOver2,
            _ => 0f
        };
        public override void RightClick(int i, int j, int type)
        {
            if (!MachineVisualRegistry.Definitions.TryGetValue(type, out var visualDef))
                return; // not a registered machine, ignore

            TileEntityHelper.TryGetEntityFromTile(i, j, out TileEntity entity, out Point16 topLeft);
            BaseMachine machine = visualDef.GetEntity(topLeft.X, topLeft.Y);

            machine.OnRightClick(topLeft.X, topLeft.Y); // machine decides what happens
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
