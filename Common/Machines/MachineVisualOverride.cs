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
using Terraria.ObjectData;

namespace Factorraria.Common.Machines
{
    public class LiquidOverlayLayer
    {
        public Asset<Texture2D> SourceTexture;
        public Func<BaseMachine, LiquidStack> GetLiquidStack;
        public string CacheKey;
    }

    // The canvas shared by BOTH the On and Off textures of an overhang machine (see MachineVisualRegistry.RegisterOverhang).
    // Canvas size is read from the On texture the first time it is needed (width = pixels / 16, height = pixels / 16 / Frames).
    public class MachineCanvas
    {
        public int FootprintWidth;   // tiles
        public int FootprintHeight;  // tiles
        public int Frames = 1;       // number of stacked frames in the On texture (only used to detect the canvas height)
        public int CanvasWidth;      // tiles, one frame (auto-detected)
        public int CanvasHeight;     // tiles, one frame (auto-detected)
        public float OriginX;        // tiles: where the footprint's top-left tile sits inside a frame (+x right, +y down)
        public float OriginY;
        public bool Resolved;
        public bool Valid;
        public string Error;

        public int FrameCount(Texture2D texture) => Math.Max(1, texture.Height / (CanvasHeight * 16));

        // Works out the canvas size from the On texture and checks that the Off texture matches it. Runs once.
        public bool Resolve(Texture2D on, Texture2D off)
        {
            if (Resolved)
                return Valid;

            Resolved = true;
            Frames = Math.Max(1, Frames);

            if (on.Width % 16 != 0 || on.Height % (16 * Frames) != 0)
            {
                Error = $"On texture is {on.Width}x{on.Height}; width must be a multiple of 16 and height a multiple of 16 x {Frames} frame(s).";
                return false;
            }

            CanvasWidth = on.Width / 16;
            CanvasHeight = on.Height / 16 / Frames;

            if (off.Width != on.Width || off.Height % (CanvasHeight * 16) != 0)
            {
                Error = $"Off texture is {off.Width}x{off.Height}; it must be {on.Width} wide and a multiple of {CanvasHeight * 16} tall (same canvas as On).";
                return false;
            }

            if (OriginX + FootprintWidth > CanvasWidth || OriginY + FootprintHeight > CanvasHeight)
            {
                Error = $"the footprint ({FootprintWidth}x{FootprintHeight} at {OriginX},{OriginY}) doesn't fit inside the detected {CanvasWidth}x{CanvasHeight} canvas.";
                return false;
            }

            Valid = true;
            return true;
        }
    }

    public class MachineVisualDefinition
    {
        public Func<int, int, BaseMachine> GetEntity;
        public Asset<Texture2D> OnTexture;
        public Asset<Texture2D> OffTexture;
        public List<LiquidOverlayLayer> LiquidOverlays = new();
        public MachineCanvas Canvas;   // null = On and Off are normal footprint-sized sheets
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

        // Use INSTEAD of Register<T> when the art is bigger than the tile footprint. BOTH the On and Off textures are
        // canvases of the SAME size (frames stacked vertically, each texture may have its own frame count), containing the
        // footprint tiles too. The footprint's top-left tile sits at (originX, originY) tiles inside the canvas
        // (+x right, +y down). Canvas width/height are detected from the On texture (pixels / 16); pass frames only if the
        // On texture has more than one frame stacked. Anything outside the footprint that you don't want drawn in a given
        // state (e.g. Off has no overhang) is just left transparent in that texture.
        public static void RegisterOverhang<T>(int tileType, string onPath, string offPath,
            int footprintWidth, int footprintHeight, float originX, float originY, int frames = 1) where T : BaseMachine, new()
        {
            if (footprintWidth <= 0 || footprintHeight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(footprintWidth), "Footprint size must be at least 1x1 tiles.");
            }

            if (originX < 0f || originY < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(originX), "The footprint origin can't be negative; it is the footprint's top-left position inside the canvas.");
            }

            Register<T>(tileType, onPath, offPath);

            Definitions[tileType].Canvas = new MachineCanvas
            {
                FootprintWidth = footprintWidth,
                FootprintHeight = footprintHeight,
                Frames = frames,
                OriginX = originX,
                OriginY = originY
            };
        }
    }

    public class MachineVisualOverride : GlobalTile
    {
        public override bool PreDraw(int i, int j, int type, SpriteBatch spriteBatch)
        {
            if (!MachineVisualRegistry.Definitions.TryGetValue(type, out var def))
                return true;

            BaseMachine entity = def.GetEntity(i, j);

            InitializeMachine(entity);

            if (def.Canvas != null)
            {
                // The whole machine (footprint tiles + overhang), On or Off, is drawn as one canvas by
                // MachineOverhangSystem.PostDrawTiles, so nothing is drawn per tile here.
                if (def.Canvas.Resolve(def.OnTexture.Value, def.OffTexture.Value))
                {
                    var canvasTexture = entity.isOn && entity.isWorking ? def.OnTexture : def.OffTexture;
                    TileEntityHelper.TryGetEntityFromTile(i, j, out _, out Point16 topLeft);
                    entity.NotifyAnimationFrame(TileEntityHelper.GetAnimationFrame(topLeft, def.Canvas.FrameCount(canvasTexture.Value)));
                }
                return false;
            }

            var texture = entity.isOn && entity.isWorking ? def.OnTexture : def.OffTexture;

            float rotation = entity is MotorTileEntityBase motor ? GetMotorRotation(motor.Facing) : 0f;

            int frame = TileEntityHelper.AnimateTileEntity(spriteBatch, texture.Value, i, j, rotation);

            if (entity.isOn && entity.isWorking)
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

        void InitializeMachine(BaseMachine machine)
        {
            if (machine.MachineInitialized)
            {
                return;
            }

            TileEntityHelper.TryGetEntityFromTile(machine.Position.X, machine.Position.Y, out _, out machine.cornerPosition);
            Tile tile = Framing.GetTileSafely(machine.Position.X, machine.Position.Y);
            TileObjectData data = TileObjectData.GetTileData(tile.TileType, 0);
            machine.MachineHeight = data.Height;
            machine.MachineWidth = data.Width;
            machine.MachineCenter = machine.cornerPosition.ToVector2() * 16 + new Vector2(machine.MachineWidth, machine.MachineHeight) * 8f;

            machine.MachineInitialized = true;
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
        public override void PlaceInWorld(int i, int j, int type, Item item)
        {
            if (!MachineVisualRegistry.Definitions.TryGetValue(type, out var def))
                return;

            // i,j is the cursor tile; resolve the multi-tile's top-left corner.
            TileEntityHelper.TryGetEntityFromTile(i, j, out _, out Point16 corner);

            // Creates + registers the entity (power grid / liquid network) if it doesn't exist yet.
            BaseMachine machine = def.GetEntity(corner.X, corner.Y);
            machine?.OnPlaced(corner.X, corner.Y);
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
                    Item.NewItem(new EntitySource_TileEntity(machine), position.X * 16 + 16, position.Y * 16 + 8, 16, 16, item);
            }

            machine.Kill(position.X, position.Y);
        }

        public override void ModifyLight(int i, int j, int type, ref float r, ref float g, ref float b)
        {
            if (!MachineVisualRegistry.Definitions.TryGetValue(type, out var def))
                return;

            TileEntityHelper.TryGetEntityFromTile(i, j, out TileEntity entity, out Point16 position);
            BaseMachine machine = def.GetEntity(position.X, position.Y);

            if(machine == null)
            { 
                return; 
            }

            if (!machine.isOn || !machine.isWorking)
            {
                r = 0f;
                g = 0f;
                b = 0f;
            }
        }
    }
}
