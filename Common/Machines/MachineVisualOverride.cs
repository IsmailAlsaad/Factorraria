using Factorraria.Common.Liquids;
using Factorraria.Common.Net;
using Factorraria.Content.Configs;
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
    // Which one-shot visual a machine is playing right now. Client-side only: never saved, never sent over the network.
    public enum MachineTransition : byte
    {
        None,
        Startup,    // went from not (isOn && isWorking) to (isOn && isWorking)
        Shutdown    // went from (isOn && isWorking) to not
    }

    public class LiquidOverlayLayer
    {
        public Asset<Texture2D> SourceTexture;
        public Func<BaseMachine, LiquidStack> GetLiquidStack;
        public string CacheKey;
    }

    // The canvas shared by BOTH the On and Off textures of an overhang machine (see MachineVisualRegistry.RegisterOverhang).
    // Textures use the usual Terraria sheet layout: every 16px tile cell is followed by Padding px (so a cell is 18px).
    // Canvas size is read from the On texture the first time it is needed.
    public class MachineCanvas
    {
        public int FootprintWidth;   // tiles
        public int FootprintHeight;  // tiles
        public int OnFrames = 1;     // number of stacked frames in the On texture
        public int OffFrames = 1;    // number of stacked frames in the Off texture
        public int Padding = 0;      // px between tile cells in the texture
        public int CanvasWidth;      // tiles, one frame (auto-detected)
        public int CanvasHeight;     // tiles, one frame (auto-detected)
        public float OriginX;        // tiles: where the footprint's top-left tile sits inside a frame (+x right, +y down)
        public float OriginY;
        public float YOffset;        // px: extra vertical shift of the drawn sprite (+ is down)
        public bool Resolved;
        public bool Valid;
        public string Error;

        public int CellSize => 16 + Padding;
        public int FrameStride => CanvasHeight * CellSize;   // px from one frame to the next
        public int FrameCount(bool on) => on ? OnFrames : OffFrames;

        // Works out the canvas size from the On texture (and its frame count) and checks that the Off texture matches. Runs once.
        // A trailing padding strip at the right/bottom edge of a texture is accepted but not required.
        public bool Resolve(Texture2D on, Texture2D off)
        {
            if (Resolved)
                return Valid;

            Resolved = true;
            OnFrames = Math.Max(1, OnFrames);
            OffFrames = Math.Max(1, OffFrames);
            int cell = CellSize;

            CanvasWidth = (on.Width + Padding) / cell;
            CanvasHeight = (on.Height + Padding) / (cell * OnFrames);

            if (CanvasWidth < 1 || CanvasHeight < 1
                || (on.Width != CanvasWidth * cell && on.Width != CanvasWidth * cell - Padding)
                || (on.Height != CanvasHeight * cell * OnFrames && on.Height != CanvasHeight * cell * OnFrames - Padding))
            {
                Error = $"On texture is {on.Width}x{on.Height}; with {cell}px tile cells and {OnFrames} frame(s) it doesn't divide into a whole canvas.";
                return false;
            }

            int offHeight = CanvasHeight * cell * OffFrames;
            if (off.Width != on.Width || (off.Height != offHeight && off.Height != offHeight - Padding))
            {
                Error = $"Off texture is {off.Width}x{off.Height}; with {OffFrames} frame(s) it must be {on.Width}x{offHeight} (same canvas as On).";
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
        public Func<int, int, BaseMachine> GetEntity;       // finds the entity, or CREATES it when missing (single-player and server only)
        public Func<int, int, BaseMachine> FindEntity;      // lookup only, never creates (patch 4.2)

        // Patch 4.5: what draw and light code call. Lookup only, in every process: entities are created by the placement routes (4.2),
        // by the bootstrap scan (MachineBootstrapSystem) and by MachinePlacement.EnsureEntity, never from draw code.
        public BaseMachine ResolveEntity(int i, int j) => FindEntity?.Invoke(i, j);
        public Asset<Texture2D> OnTexture;
        public Asset<Texture2D> OffTexture;
        public Asset<Texture2D> AltOnTexture;          // optional second On look, see MachineVisualRegistry.RegisterAlternateOn
        public Func<BaseMachine, bool> UseAltOn;
        public List<LiquidOverlayLayer> LiquidOverlays = new();
        public MachineCanvas Canvas;   // null = On and Off are normal footprint-sized sheets

        // ---- Startup / shutdown animations (MachineVisualRegistry.RegisterStartup / RegisterShutdown) ----
        public int TileType;
        public Asset<Texture2D> StartupTexture;
        public Asset<Texture2D> ShutdownTexture;
        public int StartupFrames;              // overhang canvases only: frames stacked in the texture (normal sheets are auto-detected)
        public int ShutdownFrames;
        public int StartupTicksPerFrame;       // 0 = the animation speed from the config
        public int ShutdownTicksPerFrame;
        public List<LiquidOverlayLayer> StartupLiquidOverlays = new();    // drawn on top while the startup plays
        public List<LiquidOverlayLayer> ShutdownLiquidOverlays = new();   // drawn on top while the shutdown plays
        readonly bool[] transitionChecked = new bool[3];   // indexed by (int)MachineTransition
        readonly bool[] transitionValid = new bool[3];

        Asset<Texture2D> TransitionAsset(MachineTransition kind) => kind switch
        {
            MachineTransition.Startup => StartupTexture,
            MachineTransition.Shutdown => ShutdownTexture,
            _ => null
        };

        // Frames in the startup/shutdown texture. 0 = unusable.
        public int GetTransitionFrameCount(MachineTransition kind)
        {
            Asset<Texture2D> asset = TransitionAsset(kind);
            if (asset == null)
                return 0;

            if (Canvas != null)   // a canvas can't be auto-detected, the registration has to say how many frames there are
                return kind == MachineTransition.Startup ? StartupFrames : ShutdownFrames;

            TileObjectData data = TileObjectData.GetTileData(TileType, 0);
            if (data == null || data.CoordinateFullHeight <= 0)
                return 0;

            return Math.Max(1, asset.Value.Height / data.CoordinateFullHeight);
        }

        public int GetTransitionTicksPerFrame(MachineTransition kind)
        {
            int custom = kind == MachineTransition.Startup ? StartupTicksPerFrame : ShutdownTicksPerFrame;
            return custom > 0 ? custom : Math.Max(1, ModContent.GetInstance<FurnaceOffsetConfig>().animationSpeedDivider);
        }

        // Total length of the animation in ticks, 0 when there is no usable art (the OnStartup / OnShutdown hooks still fire).
        public int GetTransitionDuration(MachineTransition kind)
        {
            if (!IsTransitionUsable(kind))
                return 0;

            return GetTransitionFrameCount(kind) * GetTransitionTicksPerFrame(kind);
        }

        // Overhang machines: the startup/shutdown texture must be the same canvas as the On texture, with the registered frame count.
        // Checked once; a bad texture only disables that animation (and logs why), it never breaks the machine.
        bool IsTransitionUsable(MachineTransition kind)
        {
            if (TransitionAsset(kind) == null)
                return false;

            int k = (int)kind;
            if (!transitionChecked[k])
            {
                transitionChecked[k] = true;
                transitionValid[k] = ValidateTransition(kind);
            }

            return transitionValid[k];
        }

        bool ValidateTransition(MachineTransition kind)
        {
            if (Canvas == null)
                return GetTransitionFrameCount(kind) > 0;

            string name = kind == MachineTransition.Startup ? "Startup" : "Shutdown";
            int frames = GetTransitionFrameCount(kind);
            if (frames <= 0)
            {
                MachineVisualRegistry.Warn($"{name} animation for tile {TileType} is ignored: an overhang machine must pass frames (how many frames are stacked in the texture).");
                return false;
            }

            Texture2D on = OnTexture.Value;
            if (!Canvas.Resolve(on, OffTexture.Value))
                return false;   // the overhang system already reports this one

            Texture2D tex = TransitionAsset(kind).Value;
            int expectedHeight = Canvas.CanvasHeight * Canvas.CellSize * frames;
            if (tex.Width != on.Width || (tex.Height != expectedHeight && tex.Height != expectedHeight - Canvas.Padding))
            {
                MachineVisualRegistry.Warn($"{name} animation for tile {TileType} is ignored: texture is {tex.Width}x{tex.Height}, with {frames} frame(s) on the {on.Width}px wide canvas it must be {on.Width}x{expectedHeight}.");
                return false;
            }

            return true;
        }

        // The art to draw INSTEAD of the normal On/Off art while this machine's startup or shutdown is playing.
        // Returns false when none is playing (or it has no usable art): draw the normal state then.
        public bool TryGetActiveTransition(BaseMachine machine, out Texture2D texture, out int frame, out List<LiquidOverlayLayer> overlays)
        {
            texture = null;
            frame = 0;
            overlays = null;

            MachineTransition kind = machine.Transition;
            if (kind == MachineTransition.None || !IsTransitionUsable(kind))
                return false;

            int frames = GetTransitionFrameCount(kind);
            if (frames <= 0)
                return false;

            uint elapsed = Main.GameUpdateCount - machine.TransitionStartTick;
            frame = (int)Math.Min(elapsed / (uint)GetTransitionTicksPerFrame(kind), (uint)(frames - 1));
            texture = TransitionAsset(kind).Value;
            overlays = kind == MachineTransition.Startup ? StartupLiquidOverlays : ShutdownLiquidOverlays;
            return true;
        }
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
                FindEntity = (i, j) => TileEntityHelper.TryGetEntityFromTile(i, j, out TileEntity found, out _) ? found as BaseMachine : null,
                TileType = tileType,
                OnTexture = ModContent.Request<Texture2D>(onPath),
                OffTexture = ModContent.Request<Texture2D>(offPath)
            };
        }
        // A second On texture, drawn instead of the normal On one while useAlt(machine) is true. Call after Register<T>.
        public static void RegisterAlternateOn(int tileType, string altOnPath, Func<BaseMachine, bool> useAlt)
        {
            if (!Definitions.TryGetValue(tileType, out var def))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tileType),
                    $"RegisterAlternateOn failed for tile type {tileType}: call MachineVisualRegistry.Register (base On/Off textures) first."
                );
            }

            def.AltOnTexture = ModContent.Request<Texture2D>(altOnPath);
            def.UseAltOn = useAlt;
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

        // ---- Startup / shutdown animations. Call after Register<T> / RegisterOverhang<T>, in PostSetupContent like everything else. ----
        // RegisterStartup: played ONCE when the machine goes from not (isOn && isWorking) to (isOn && isWorking), then the On art takes over.
        // RegisterShutdown: played ONCE when it goes from (isOn && isWorking) to not, then the Off art takes over.
        // The texture is a vertical strip of frames, same layout as the On texture (normal machine: the usual tile sheet repeated per frame;
        // overhang machine: the same canvas as On/Off, repeated per frame). Frames are auto-detected for normal machines; an overhang
        // machine must pass frames. ticksPerFrame 0 = use the config's animation speed. A missing PNG only logs a warning (RequestIfExists):
        // the machine then skips the animation, but OnStartup / OnShutdown still fire.
        public static void RegisterStartup(int tileType, string texturePath, int frames = 0, int ticksPerFrame = 0)
        {
            MachineVisualDefinition def = RequireDefinition(tileType, nameof(RegisterStartup));
            if (!ModContent.RequestIfExists<Texture2D>(texturePath, out Asset<Texture2D> asset, AssetRequestMode.AsyncLoad))
            {
                Warn($"RegisterStartup: texture '{texturePath}' not found, tile {tileType} has no startup animation yet.");
                return;
            }

            def.StartupTexture = asset;
            def.StartupFrames = frames;
            def.StartupTicksPerFrame = ticksPerFrame;
        }

        public static void RegisterShutdown(int tileType, string texturePath, int frames = 0, int ticksPerFrame = 0)
        {
            MachineVisualDefinition def = RequireDefinition(tileType, nameof(RegisterShutdown));
            if (!ModContent.RequestIfExists<Texture2D>(texturePath, out Asset<Texture2D> asset, AssetRequestMode.AsyncLoad))
            {
                Warn($"RegisterShutdown: texture '{texturePath}' not found, tile {tileType} has no shutdown animation yet.");
                return;
            }

            def.ShutdownTexture = asset;
            def.ShutdownFrames = frames;
            def.ShutdownTicksPerFrame = ticksPerFrame;
        }

        // Liquid layer drawn on top of the startup (or shutdown) animation, recolored per liquid through LiquidTextureCache exactly like
        // RegisterLiquidOverlay. The texture must have the SAME frame layout as the startup (or shutdown) texture it goes with.
        // The cache key is the texture path, so every overlay needs its own PNG.
        public static void RegisterStartupLiquidOverlay(int tileType, string texturePath, Func<BaseMachine, LiquidStack> getLiquidStack)
        {
            MachineVisualDefinition def = RequireDefinition(tileType, nameof(RegisterStartupLiquidOverlay));
            AddTransitionOverlay(def.StartupLiquidOverlays, tileType, texturePath, getLiquidStack, nameof(RegisterStartupLiquidOverlay));
        }

        public static void RegisterShutdownLiquidOverlay(int tileType, string texturePath, Func<BaseMachine, LiquidStack> getLiquidStack)
        {
            MachineVisualDefinition def = RequireDefinition(tileType, nameof(RegisterShutdownLiquidOverlay));
            AddTransitionOverlay(def.ShutdownLiquidOverlays, tileType, texturePath, getLiquidStack, nameof(RegisterShutdownLiquidOverlay));
        }

        static void AddTransitionOverlay(List<LiquidOverlayLayer> layers, int tileType, string texturePath, Func<BaseMachine, LiquidStack> getLiquidStack, string caller)
        {
            if (!ModContent.RequestIfExists<Texture2D>(texturePath, out Asset<Texture2D> asset, AssetRequestMode.AsyncLoad))
            {
                Warn($"{caller}: texture '{texturePath}' not found, tile {tileType} skips that liquid layer for now.");
                return;
            }

            layers.Add(new LiquidOverlayLayer
            {
                SourceTexture = asset,
                GetLiquidStack = getLiquidStack,
                CacheKey = texturePath
            });
        }

        static MachineVisualDefinition RequireDefinition(int tileType, string caller)
        {
            if (!Definitions.TryGetValue(tileType, out var def))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tileType),
                    $"{caller} failed for tile type {tileType}: call MachineVisualRegistry.Register (base On/Off textures) first."
                );
            }

            return def;
        }

        internal static void Warn(string message) => ModContent.GetInstance<global::Factorraria.Factorraria>().Logger.Warn(message);

        // Use INSTEAD of Register<T> when the art is bigger than the tile footprint. BOTH the On and Off textures are
        // canvases of the SAME size (frames stacked vertically, each texture may have its own frame count), containing the
        // footprint tiles too. The footprint's top-left tile sits at (originX, originY) tiles inside the canvas
        // (+x right, +y down). Textures use the normal 16px tile + 2px padding layout (18px cells). Canvas width/height are
        // detected from the On texture (pixels / 18, height also divided by onFrames). Pass onFrames / offFrames for each
        // texture that has more than one frame stacked. Anything outside the footprint that you don't want drawn in a given
        // state (e.g. Off has no overhang) is just left transparent. yOffset (pixels, + is down) nudges the drawn sprite.
        public static void RegisterOverhang<T>(int tileType, string onPath, string offPath,
            int footprintWidth, int footprintHeight, float originX, float originY, int onFrames = 1, int offFrames = 1, float yOffset = 0f) where T : BaseMachine, new()
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
                OnFrames = onFrames,
                OffFrames = offFrames,
                YOffset = yOffset,
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

            BaseMachine entity = def.ResolveEntity(i, j);

            // A multiplayer client without an entity for this tile (the server has not shared one) draws the plain vanilla tile.
            if (entity == null)
                return true;

            InitializeMachine(entity);

            if (def.Canvas != null)
            {
                // The whole machine (footprint tiles + overhang), On or Off, is drawn as one canvas by
                // MachineOverhangSystem.PostDrawTiles, so nothing is drawn per tile here.
                if (def.Canvas.Resolve(def.OnTexture.Value, def.OffTexture.Value) && !def.TryGetActiveTransition(entity, out _, out _, out _))
                {
                    TileEntityHelper.TryGetEntityFromTile(i, j, out _, out Point16 topLeft);
                    entity.NotifyAnimationFrame(TileEntityHelper.GetAnimationFrame(topLeft, def.Canvas.FrameCount(entity.isOn && entity.isWorking)));
                }
                return false;
            }

            float rotation = entity is MotorTileEntityBase motor ? GetMotorRotation(motor.Facing) : 0f;

            // A startup / shutdown that is playing replaces the On/Off art and the On liquid layers. It does not notify
            // OnAnimationFrameChanged, so per-frame sounds of the normal On animation don't fire on its frames.
            if (def.TryGetActiveTransition(entity, out Texture2D transitionTexture, out int transitionFrame, out List<LiquidOverlayLayer> transitionOverlays))
            {
                TileEntityHelper.AnimateTileEntity(spriteBatch, transitionTexture, i, j, rotation, transitionFrame);
                DrawLiquidOverlays(spriteBatch, transitionOverlays, entity, i, j, rotation, transitionFrame);
                return false;
            }

            var texture = entity.isOn && entity.isWorking
                ? (def.UseAltOn != null && def.UseAltOn(entity) ? def.AltOnTexture : def.OnTexture)
                : def.OffTexture;

            int frame = TileEntityHelper.AnimateTileEntity(spriteBatch, texture.Value, i, j, rotation);

            if (entity.isOn && entity.isWorking)
                DrawLiquidOverlays(spriteBatch, def.LiquidOverlays, entity, i, j, rotation, -1);

            entity.NotifyAnimationFrame(frame);
            return false;
        }

        // forcedFrame -1 = the normal looping animation, otherwise the exact frame (startup / shutdown).
        static void DrawLiquidOverlays(SpriteBatch spriteBatch, List<LiquidOverlayLayer> layers, BaseMachine entity, int i, int j, float rotation, int forcedFrame)
        {
            if (layers == null)
                return;

            foreach (var layer in layers)
            {
                LiquidStack stack = layer.GetLiquidStack(entity);
                if (stack == null || stack.IsEmpty)
                    continue;

                LiquidTypeDefinition liquidDef = LiquidTypeRegistry.Get(stack.LiquidType);
                Texture2D recolored = LiquidTextureCache.GetOrCreate(layer.CacheKey, layer.SourceTexture.Value, liquidDef);

                TileEntityHelper.AnimateTileEntity(spriteBatch, recolored, i, j, rotation, forcedFrame);
            }
        }

        void InitializeMachine(BaseMachine machine)
        {
            // Patch 4.1: the real work moved to BaseMachine.InitializeGeometry so a dedicated server can run it too.
            machine.InitializeGeometry();
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

            // Patch 4.5: single-player creates a missing entity here (a safety net behind the bootstrap scan); a multiplayer client only looks up.
            BaseMachine machine = MachinePlacement.EnsureEntity(i, j);
            if (machine == null)
            {
                // Patch 6.1: a multiplayer client with no replica asks the server for it (the open request creates and shares the entity there).
                if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient && global::Factorraria.Common.UI.MachineUIRegistry.Definitions.ContainsKey(type))
                    ModContent.GetInstance<global::Factorraria.Common.Systems.MachineUISystem>().RequestOpenWhenReplicaArrives(i, j);
                return;
            }

            TileEntityHelper.TryGetEntityFromTile(i, j, out _, out Point16 topLeft);

            machine.OnRightClick(topLeft.X, topLeft.Y); // machine decides what happens
        }
        // Patch 4.2: PlaceInWorld moved to MachinePlacedConsumer (Common/Net/MachinePlacement.cs). It must run on the server, not on
        // the placing client.
        public override void KillTile(int i, int j, int type, ref bool fail, ref bool effectOnly, ref bool noItem)
        {
            // Patch 4.4: the tile is only being damaged (fail) or only showing its break effect: the machine stays.
            if (fail || effectOnly)
                return;

            if (!MachineVisualRegistry.Definitions.ContainsKey(type))
                return;

            // Server and single-player drop the contents and remove the entity; a multiplayer client only removes its replica.
            MachineRemoval.OnTileKilled(i, j);
        }

        public override void ModifyLight(int i, int j, int type, ref float r, ref float g, ref float b)
        {
            if (!MachineVisualRegistry.Definitions.TryGetValue(type, out var def))
                return;

            TileEntityHelper.TryGetEntityFromTile(i, j, out TileEntity entity, out Point16 position);
            BaseMachine machine = def.ResolveEntity(position.X, position.Y);

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
