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

        // Optional startup / shutdown versions of this layer (RegisterLiquidOverlay's last two parameters). Same liquid getter,
        // recolored through LiquidTextureCache under their own path as cache key. A layer without one is simply not drawn during that animation.
        public Asset<Texture2D> StartupTexture;
        public Asset<Texture2D> ShutdownTexture;
        public string StartupCacheKey;
        public string ShutdownCacheKey;

        // None = the normal On layer.
        public bool TryGetTexture(MachineTransition kind, out Texture2D texture, out string cacheKey)
        {
            Asset<Texture2D> asset = kind switch
            {
                MachineTransition.Startup => StartupTexture,
                MachineTransition.Shutdown => ShutdownTexture,
                _ => SourceTexture
            };

            texture = asset?.Value;
            cacheKey = kind switch
            {
                MachineTransition.Startup => StartupCacheKey,
                MachineTransition.Shutdown => ShutdownCacheKey,
                _ => CacheKey
            };
            return texture != null;
        }
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
        public int StyleCount = 1;   // looks (variants) side by side in the texture: the sheet is StyleCount * canvas width wide, style k is the k-th block of columns
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
        public int ClampStyle(int style) => Math.Clamp(style, 0, Math.Max(1, StyleCount) - 1);

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

            StyleCount = Math.Max(1, StyleCount);
            CanvasWidth = (on.Width + Padding) / cell / StyleCount;   // one style's width; the sheet holds StyleCount of them side by side
            CanvasHeight = (on.Height + Padding) / (cell * OnFrames);

            if (CanvasWidth < 1 || CanvasHeight < 1
                || (on.Width != CanvasWidth * StyleCount * cell && on.Width != CanvasWidth * StyleCount * cell - Padding)
                || (on.Height != CanvasHeight * cell * OnFrames && on.Height != CanvasHeight * cell * OnFrames - Padding))
            {
                Error = $"On texture is {on.Width}x{on.Height}; with {cell}px tile cells, {OnFrames} frame(s) and {StyleCount} style(s) it doesn't divide into a whole canvas.";
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

        // Optional extra drawn inside the placement hologram, on top of the canvas (for parts the machine's entity draws itself, like the Bee Wheel's wheel).
        // Arguments: the sprite batch (already begun, world space), the footprint's top-left in world pixels, and the hologram tint (faded, or red when invalid).
        public Action<SpriteBatch, Vector2, Color> HologramExtra;

        // ---- Startup / shutdown animations (optional last two parameters of MachineVisualRegistry.Register / RegisterOverhang) ----
        // Frame count is detected from the texture height and the speed is the config's animation speed, same as the On texture.
        public int TileType;
        public Asset<Texture2D> StartupTexture;
        public Asset<Texture2D> ShutdownTexture;

        readonly bool[] transitionChecked = new bool[3];   // indexed by (int)MachineTransition
        readonly bool[] transitionValid = new bool[3];

        Asset<Texture2D> TransitionAsset(MachineTransition kind) => kind switch
        {
            MachineTransition.Startup => StartupTexture,
            MachineTransition.Shutdown => ShutdownTexture,
            _ => null
        };

        // Frames in the startup/shutdown texture, detected from its height. 0 = unusable.
        // Normal machine: texture height / one tile-sheet frame. Overhang machine: texture height / one canvas frame (the canvas
        // size is detected from the On texture).
        public int GetTransitionFrameCount(MachineTransition kind)
        {
            Asset<Texture2D> asset = TransitionAsset(kind);
            if (asset == null)
                return 0;

            if (Canvas != null)
            {
                if (!Canvas.Resolve(OnTexture.Value, OffTexture.Value))
                    return 0;   // the overhang system already reports an invalid canvas

                int stride = Canvas.FrameStride;
                return stride > 0 ? Math.Max(1, (asset.Value.Height + Canvas.Padding) / stride) : 0;
            }

            TileObjectData data = TileObjectData.GetTileData(TileType, 0);
            if (data == null || data.CoordinateFullHeight <= 0)
                return 0;

            return Math.Max(1, asset.Value.Height / data.CoordinateFullHeight);
        }

        public int GetTransitionTicksPerFrame(MachineTransition kind) =>
            Math.Max(1, ModContent.GetInstance<FurnaceOffsetConfig>().animationSpeedDivider);

        // Total length of the animation in ticks, 0 when there is no usable art (the OnStartup / OnShutdown hooks still fire).
        public int GetTransitionDuration(MachineTransition kind)
        {
            if (!IsTransitionUsable(kind))
                return 0;

            return GetTransitionFrameCount(kind) * GetTransitionTicksPerFrame(kind);
        }

        // Overhang machines: the startup/shutdown texture must be the same canvas width as On and a whole number of canvas frames tall.
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
            int frames = GetTransitionFrameCount(kind);
            if (frames <= 0)
                return false;

            if (Canvas == null)
                return true;

            string name = kind == MachineTransition.Startup ? "Startup" : "Shutdown";
            Texture2D on = OnTexture.Value;
            Texture2D tex = TransitionAsset(kind).Value;
            int expectedHeight = Canvas.FrameStride * frames;
            if (tex.Width != on.Width || (tex.Height != expectedHeight && tex.Height != expectedHeight - Canvas.Padding))
            {
                MachineVisualRegistry.Warn($"{name} animation for tile {TileType} is ignored: texture is {tex.Width}x{tex.Height}, it must be {on.Width}px wide and a whole number of {Canvas.FrameStride}px canvas frames tall.");
                return false;
            }

            return true;
        }

        // The art to draw INSTEAD of the normal On/Off art while this machine's startup or shutdown is playing.
        // Returns false when none is playing (or it has no usable art): draw the normal state then (kind is None).
        // kind tells the liquid layers which of their textures to use (LiquidOverlayLayer.TryGetTexture).
        public bool TryGetActiveTransition(BaseMachine machine, out Texture2D texture, out int frame, out MachineTransition kind)
        {
            texture = null;
            frame = 0;
            kind = MachineTransition.None;

            MachineTransition active = machine.Transition;
            if (active == MachineTransition.None || !IsTransitionUsable(active))
                return false;

            int frames = GetTransitionFrameCount(active);
            if (frames <= 0)
                return false;

            uint elapsed = Main.GameUpdateCount - machine.TransitionStartTick;
            frame = (int)Math.Min(elapsed / (uint)GetTransitionTicksPerFrame(active), (uint)(frames - 1));
            texture = TransitionAsset(active).Value;
            kind = active;
            return true;
        }
    }

    public static class MachineVisualRegistry
    {
        public static Dictionary<int, MachineVisualDefinition> Definitions = new();

        // startupPath / shutdownPath are optional. Leave them out (or pass null) and that animation is skipped: the machine switches
        // between the On and Off textures immediately, exactly as before. A path whose PNG doesn't exist yet only logs a warning.
        // Startup plays once when the machine goes from not (isOn && isWorking) to (isOn && isWorking), then the On texture takes over;
        // shutdown plays once on the way back, then the Off texture takes over. Both are vertical frame strips in the same layout as On.
        public static void Register<T>(int tileType, string onPath, string offPath, string startupPath = null, string shutdownPath = null) where T : BaseMachine, new()
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

            MachineVisualDefinition def = Definitions[tileType];
            def.StartupTexture = RequestOptional(startupPath, tileType, "startup");
            def.ShutdownTexture = RequestOptional(shutdownPath, tileType, "shutdown");
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
        // startupPath / shutdownPath are optional liquid layers for the startup / shutdown animation (same frame layout as that animation's
        // texture). Leave them out and this layer is simply not drawn while that animation plays. Each PNG is its own cache key.
        public static void RegisterLiquidOverlay(int tileType, string texturePath, Func<BaseMachine, LiquidStack> getLiquidStack, string startupPath = null, string shutdownPath = null)
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
                CacheKey = texturePath, // already unique per overlay, no need to invent a separate key
                StartupTexture = RequestOptional(startupPath, tileType, "liquid startup"),
                StartupCacheKey = startupPath,
                ShutdownTexture = RequestOptional(shutdownPath, tileType, "liquid shutdown"),
                ShutdownCacheKey = shutdownPath
            });
        }

        // null / empty path = no animation, silently. A path whose PNG doesn't exist yet = no animation, with a log warning.
        static Asset<Texture2D> RequestOptional(string texturePath, int tileType, string what)
        {
            if (string.IsNullOrEmpty(texturePath))
                return null;

            if (!ModContent.RequestIfExists<Texture2D>(texturePath, out Asset<Texture2D> asset, AssetRequestMode.AsyncLoad))
            {
                Warn($"Tile {tileType}: {what} texture '{texturePath}' not found, skipping that animation for now.");
                return null;
            }

            return asset;
        }

        internal static void Warn(string message) => ModContent.GetInstance<global::Factorraria.Factorraria>().Logger.Warn(message);

        // styleCount (default 1): the texture holds that many looks side by side (the sheet is styleCount * canvas width wide, every style
        // block has the same canvas layout). BaseMachine rolls a random style when the machine is placed (0 to styleCount - 1), saves it and
        // syncs it, so the same machine always draws the same look. Animation (startup / shutdown) and liquid textures use the same layout.
        // Use INSTEAD of Register<T> when the art is bigger than the tile footprint. BOTH the On and Off textures are
        // canvases of the SAME size (frames stacked vertically, each texture may have its own frame count), containing the
        // footprint tiles too. The footprint's top-left tile sits at (originX, originY) tiles inside the canvas
        // (+x right, +y down). Textures use the normal 16px tile + 2px padding layout (18px cells). Canvas width/height are
        // detected from the On texture (pixels / 18, height also divided by onFrames). Pass onFrames / offFrames for each
        // texture that has more than one frame stacked. Anything outside the footprint that you don't want drawn in a given
        // state (e.g. Off has no overhang) is just left transparent. yOffset (pixels, + is down) nudges the drawn sprite.
        // startupPath / shutdownPath are optional: the same canvas as On, stacked frames (the frame count is detected from the height).
        public static void RegisterOverhang<T>(int tileType, string onPath, string offPath,
            int footprintWidth, int footprintHeight, float originX, float originY, int onFrames = 1, int offFrames = 1, float yOffset = 0f,
            string startupPath = null, string shutdownPath = null, int styleCount = 1) where T : BaseMachine, new()
        {
            if (footprintWidth <= 0 || footprintHeight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(footprintWidth), "Footprint size must be at least 1x1 tiles.");
            }

            if (originX < 0f || originY < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(originX), "The footprint origin can't be negative; it is the footprint's top-left position inside the canvas.");
            }

            Register<T>(tileType, onPath, offPath, startupPath, shutdownPath);

            Definitions[tileType].Canvas = new MachineCanvas
            {
                FootprintWidth = footprintWidth,
                FootprintHeight = footprintHeight,
                OnFrames = onFrames,
                OffFrames = offFrames,
                YOffset = yOffset,
                OriginX = originX,
                OriginY = originY,
                StyleCount = Math.Max(1, styleCount)
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
            if (def.TryGetActiveTransition(entity, out Texture2D transitionTexture, out int transitionFrame, out MachineTransition transitionKind))
            {
                TileEntityHelper.AnimateTileEntity(spriteBatch, transitionTexture, i, j, rotation, transitionFrame);
                DrawLiquidOverlays(spriteBatch, def.LiquidOverlays, transitionKind, entity, i, j, rotation, transitionFrame);
                return false;
            }

            var texture = entity.isOn && entity.isWorking
                ? (def.UseAltOn != null && def.UseAltOn(entity) ? def.AltOnTexture : def.OnTexture)
                : def.OffTexture;

            int frame = TileEntityHelper.AnimateTileEntity(spriteBatch, texture.Value, i, j, rotation);

            if (entity.isOn && entity.isWorking)
                DrawLiquidOverlays(spriteBatch, def.LiquidOverlays, MachineTransition.None, entity, i, j, rotation, -1);

            entity.NotifyAnimationFrame(frame);
            return false;
        }

        // forcedFrame -1 = the normal looping animation, otherwise the exact frame (startup / shutdown).
        // kind picks each layer's On / startup / shutdown texture; a layer without that one is skipped.
        static void DrawLiquidOverlays(SpriteBatch spriteBatch, List<LiquidOverlayLayer> layers, MachineTransition kind, BaseMachine entity, int i, int j, float rotation, int forcedFrame)
        {
            if (layers == null)
                return;

            foreach (var layer in layers)
            {
                if (!layer.TryGetTexture(kind, out Texture2D source, out string cacheKey))
                    continue;

                LiquidStack stack = layer.GetLiquidStack(entity);
                if (stack == null || stack.IsEmpty)
                    continue;

                LiquidTypeDefinition liquidDef = LiquidTypeRegistry.Get(stack.LiquidType);
                Texture2D recolored = LiquidTextureCache.GetOrCreate(cacheKey, source, liquidDef);

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
