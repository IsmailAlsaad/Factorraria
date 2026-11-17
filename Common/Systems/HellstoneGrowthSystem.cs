using Factorraria.Common.Machines;
using Factorraria.Common.Net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Systems
{
    // Lava slowly turns stone into hellstone in the underworld.
    // Multiplayer: PostUpdateWorld never runs on a client, so only the server (or single player) decides anything. A converted tile
    // is replicated with a vanilla tile square, so there is no packet of our own. Only players' surroundings are sampled
    // (like vanilla random tile updates), plus a sparse background sample of the WHOLE underworld (GlobalSamplesPerTick), so everything keeps
    // converting even when no player is near; far from players it is just slower (about one pick per block every area / GlobalSamplesPerTick ticks).
    // Growth rule: a stone block becomes hellstone when it touches lava (distance 1), or when it is within MaxDistance of lava
    // AND a neighbouring hellstone block is closer to the lava than it is. That makes the hellstone grow outward from the contact
    // point instead of appearing at random. Distance is counted in orthogonal steps (a diamond, not a square).
    // Expected time per block = BaseSecondsToConvert * DistanceSlowdown^(distance - 1). All values are plain fields, so hot reload can tweak them.
    // Effects: when a block converts, the server tells nearby clients (HellstoneGrowNetSystem, packet HellstoneGrow) and PlayEffect runs on
    // every client (and in single player): a random sound from Assets/Sounds/HellstoneGrow plus a burst of lava dust. Purely cosmetic.
    // ASH BUFFER (on top of the rules above): hellstone never touches stone directly. Stone becomes ash first, and ash (not stone) is what
    // turns into hellstone. Stone -> ash when it touches lava, or when a hellstone block is within AshLayerDepth steps of it (and lava is
    // within MaxDistance + AshLayerDepth), so a 2-block ash layer always sits between hellstone and stone. Ash converts much faster
    // (AshSecondsToConvert) than hellstone. Ash -> hellstone uses the normal lava/closer-hellstone rule above, and only when a stone block
    // is reachable from it through at most AshLayerDepth ash blocks (the ash sits between the hellstone/lava and the stone).
    // The ash conversion has its own effect (SoundID.LiquidsWaterLava plus a few ash dust): the same HellstoneGrow packet, with a kind byte.
    public class HellstoneGrowthSystem : ModSystem
    {
        public bool Enabled = true;
        public int MaxDistance = 4;
        public float BaseSecondsToConvert = 5f;
        public float DistanceSlowdown = 2f;
        public int MinLavaAmount = 16;

        public int AshLayerDepth = 2;            // thickness of the ash layer between hellstone and stone
        public float AshSecondsToConvert = 1.5f; // expected seconds per stone -> ash block (hellstone uses BaseSecondsToConvert)
        public int AshDustCount = 4;
        public float AshSoundVolume = 0.6f;
        public float AshSoundPitchVariance = 0.15f;

        public int SamplesPerPlayerPerTick = 40;
        public int AreaHalfWidth = 100;
        public int AreaHalfHeight = 60;
        public int GlobalSamplesPerTick = 2000; // background samples over the whole underworld, wherever the players are. 0 = only near players

        // Sound: files Assets/Sounds/HellstoneGrow/HellstoneGrow1.wav (or .ogg) ... HellstoneGrow<SoundVariants>. 0 = no custom files yet, a vanilla placeholder plays.
        public int SoundVariants = 3;
        public float SoundVolume = 0.7f;
        public float SoundPitchVariance = 0.15f;
        public int SoundMaxInstances = 6;

        // Particles (client only). Lava dust drips and falls, embers float up, smoke drifts. Set a count to 0 to drop that part.
        public int LavaDustCount = 10;
        public int EmberDustCount = 8;
        public int SmokeDustCount = 3;
        public float DustCullMargin = 200f; // no dust when the block is further than this off the screen (the sound still plays)

        int[] boxLeft = new int[Main.maxPlayers];
        int[] boxRight = new int[Main.maxPlayers];
        int[] boxTop = new int[Main.maxPlayers];
        int[] boxBottom = new int[Main.maxPlayers];
        float[] boxSampleRate = new float[Main.maxPlayers];
        int boxCount;
        float globalSampleRate;

        public override void PostUpdateWorld()
        {
            if (!Enabled || Main.netMode == NetmodeID.MultiplayerClient)
                return;

            boxCount = 0;
            int bottomLimit = Main.maxTilesY - 3;

            globalSampleRate = 0f;
            int globalTop = Main.UnderworldLayer;
            if (GlobalSamplesPerTick > 0 && bottomLimit >= globalTop && Main.maxTilesX > 6)
                globalSampleRate = GlobalSamplesPerTick / (float)((Main.maxTilesX - 4) * (bottomLimit - globalTop + 1));
            for (int p = 0; p < Main.maxPlayers; p++)
            {
                Player player = Main.player[p];
                if (!player.active)
                    continue;

                int cx = (int)(player.Center.X / 16f);
                int cy = (int)(player.Center.Y / 16f);
                int left = System.Math.Max(2, cx - AreaHalfWidth);
                int right = System.Math.Min(Main.maxTilesX - 3, cx + AreaHalfWidth);
                int top = System.Math.Max(Main.UnderworldLayer, cy - AreaHalfHeight);
                int bottom = System.Math.Min(bottomLimit, cy + AreaHalfHeight);
                if (right < left || bottom < top)
                    continue;

                boxLeft[boxCount] = left;
                boxRight[boxCount] = right;
                boxTop[boxCount] = top;
                boxBottom[boxCount] = bottom;
                boxSampleRate[boxCount] = SamplesPerPlayerPerTick / (float)((right - left + 1) * (bottom - top + 1));
                boxCount++;
            }

            for (int b = 0; b < boxCount; b++)
            {
                for (int s = 0; s < SamplesPerPlayerPerTick; s++)
                {
                    int x = Main.rand.Next(boxLeft[b], boxRight[b] + 1);
                    int y = Main.rand.Next(boxTop[b], boxBottom[b] + 1);
                    TryGrow(x, y);
                }
            }

            // Whole underworld, no matter where the players are.
            if (globalSampleRate > 0f)
            {
                for (int s = 0; s < GlobalSamplesPerTick; s++)
                {
                    int x = Main.rand.Next(2, Main.maxTilesX - 2);
                    int y = Main.rand.Next(globalTop, bottomLimit + 1);
                    TryGrow(x, y);
                }
            }
        }

        void TryGrow(int x, int y)
        {
            Tile tile = Main.tile[x, y];
            if (!tile.HasUnactuatedTile)
                return;

            if (tile.TileType == TileID.Stone)
                TryAsh(x, y);
            else if (tile.TileType == TileID.Ash)
                TryHellstone(x, y);
        }

        // Stone -> ash: touching lava, or a hellstone block within AshLayerDepth steps (that is what keeps the layer between hellstone and stone).
        void TryAsh(int x, int y)
        {
            if (!TouchesLava(x, y))
            {
                if (!HasHellstoneWithin(x, y, AshLayerDepth))
                    return;
                if (LavaDistance(x, y, AshLayerDepth) <= 0)
                    return;
            }

            if (!Roll(x, y, AshSecondsToConvert))
                return;

            Transform(x, y, TileID.Ash);
        }

        // Ash -> hellstone: the lava rules below, plus a stone block reachable through the ash layer.
        void TryHellstone(int x, int y)
        {
            int distance = LavaDistance(x, y);
            if (distance <= 0)
                return;
            if (distance > 1 && !HasCloserHellstone(x, y, distance))
                return;
            if (!HasReachableStone(x, y, AshLayerDepth))
                return;

            float seconds = BaseSecondsToConvert * (float)System.Math.Pow(DistanceSlowdown, distance - 1);
            if (!Roll(x, y, seconds))
                return;

            Transform(x, y, TileID.Hellstone);
        }

        // Odds per tick that this block is picked at all (several players' areas can overlap, so add them up),
        // so the expected time stays the same however many samples or players there are.
        bool Roll(int x, int y, float seconds)
        {
            float sampleRate = globalSampleRate;
            for (int b = 0; b < boxCount; b++)
            {
                if (x >= boxLeft[b] && x <= boxRight[b] && y >= boxTop[b] && y <= boxBottom[b])
                    sampleRate += boxSampleRate[b];
            }
            if (sampleRate <= 0f)
                return false;

            float chance = 1f / (seconds * 60f * sampleRate);
            return Main.rand.NextFloat() < chance;
        }

        bool IsTileType(int x, int y, ushort type)
        {
            if (x < 0 || y < 0 || x >= Main.maxTilesX || y >= Main.maxTilesY)
                return false;

            Tile t = Main.tile[x, y];
            return t.HasUnactuatedTile && t.TileType == type;
        }

        bool TouchesLava(int x, int y)
        {
            return IsLava(x + 1, y) || IsLava(x - 1, y) || IsLava(x, y + 1) || IsLava(x, y - 1);
        }

        // A hellstone block within 'range' orthogonal steps (a diamond).
        bool HasHellstoneWithin(int x, int y, int range)
        {
            for (int dx = -range; dx <= range; dx++)
            {
                int rest = range - System.Math.Abs(dx);
                for (int dy = -rest; dy <= rest; dy++)
                {
                    if ((dx != 0 || dy != 0) && IsTileType(x + dx, y + dy, TileID.Hellstone))
                        return true;
                }
            }
            return false;
        }

        // A stone block reachable from (x, y) by walking through at most 'depth' - 1 ash blocks (depth 1 = a stone neighbour).
        bool HasReachableStone(int x, int y, int depth, int fromX = int.MinValue, int fromY = int.MinValue)
        {
            for (int i = 0; i < 4; i++)
            {
                int nx = x + (i == 0 ? 1 : i == 1 ? -1 : 0);
                int ny = y + (i == 2 ? 1 : i == 3 ? -1 : 0);
                if (nx == fromX && ny == fromY)
                    continue;
                if (IsTileType(nx, ny, TileID.Stone))
                    return true;
                if (depth > 1 && IsTileType(nx, ny, TileID.Ash) && HasReachableStone(nx, ny, depth - 1, x, y))
                    return true;
            }
            return false;
        }

        // Smallest number of orthogonal steps from (x, y) to a lava cell within MaxDistance, or 0 when there is none.
        int LavaDistance(int x, int y, int extra = 0)
        {
            int max = System.Math.Max(1, MaxDistance) + extra;
            for (int r = 1; r <= max; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    int dy = r - System.Math.Abs(dx);
                    if (IsLava(x + dx, y + dy))
                        return r;
                    if (dy != 0 && IsLava(x + dx, y - dy))
                        return r;
                }
            }
            return 0;
        }

        bool IsLava(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Main.maxTilesX || y >= Main.maxTilesY)
                return false;

            Tile t = Main.tile[x, y];
            if (t.LiquidAmount < MinLavaAmount || t.LiquidType != LiquidID.Lava)
                return false;

            // A solid block never counts as lava, even if it still carries a leftover liquid amount.
            return !(t.HasUnactuatedTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType]);
        }

        bool HasCloserHellstone(int x, int y, int distance)
        {
            for (int i = 0; i < 4; i++)
            {
                int nx = x + (i == 0 ? 1 : i == 1 ? -1 : 0);
                int ny = y + (i == 2 ? 1 : i == 3 ? -1 : 0);
                Tile n = Main.tile[nx, ny];
                if (!n.HasUnactuatedTile || n.TileType != TileID.Hellstone)
                    continue;

                int neighbourDistance = LavaDistance(nx, ny);
                if (neighbourDistance > 0 && neighbourDistance < distance)
                    return true;
            }
            return false;
        }

        void Transform(int x, int y, ushort newType)
        {
            Tile tile = Main.tile[x, y];
            tile.TileType = newType;
            WorldGen.SquareTileFrame(x, y, true);

            // Neighbours can change frame too (stone and hellstone merge), so send the 3x3 around it.
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendTileSquare(-1, x - 1, y - 1, 3, 3);

            // Cosmetic only: single player plays it here, a server tells the nearby clients, a client never gets here.
            ModContent.GetInstance<HellstoneGrowNetSystem>().Announce(x, y, newType == TileID.Ash);
        }

        // Stone -> ash: the lava/water hiss and a few ash dust.
        void PlayAshEffect(Vector2 center, int x, int y)
        {
            SoundStyle style;
            if (SoundVariants > 0)
            {
                int index = Main.rand.Next(1, SoundVariants + 1);
                style = new SoundStyle($"Factorraria/Assets/Sounds/AshGrow/AshGrow{index}");
            }
            else
            {
                style = SoundID.LiquidsWaterLava;
            }

            SoundEngine.PlaySound(style with { Volume = AshSoundVolume, PitchVariance = AshSoundPitchVariance, MaxInstances = SoundMaxInstances }, center);

            if (!BaseMachine.IsOnScreen(center, DustCullMargin))
                return;

            Vector2 corner = new Vector2(x * 16f, y * 16f);
            for (int i = 0; i < AshDustCount; i++)
            {
                Vector2 position = corner + new Vector2(Main.rand.NextFloat(16f), Main.rand.NextFloat(16f));
                Vector2 velocity = new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-2f, -0.4f));
                Dust.NewDustPerfect(position, DustID.Ash, velocity, 0, default, Main.rand.NextFloat(0.9f, 1.3f));
            }
        }

        // Client and single player: the sound and the lava burst for the block at tile (x, y). Called by HellstoneGrowNetSystem.
        public void PlayEffect(int x, int y, bool ash = false)
        {
            if (Main.dedServ)
                return;

            Vector2 center = new Vector2(x * 16f + 8f, y * 16f + 8f);

            if (ash)
            {
                PlayAshEffect(center, x, y);
                return;
            }

            SoundStyle style;
            if (SoundVariants > 0)
            {
                int index = Main.rand.Next(1, SoundVariants + 1);
                style = new SoundStyle($"Factorraria/Assets/Sounds/HellstoneGrow/HellstoneGrow{index}");
            }
            else
            {
                style = SoundID.LiquidsHoneyLava; // placeholder until the custom sounds exist
            }
            SoundEngine.PlaySound(style with { Volume = SoundVolume, PitchVariance = SoundPitchVariance, MaxInstances = SoundMaxInstances }, center);

            if (!BaseMachine.IsOnScreen(center, DustCullMargin))
                return;

            Vector2 corner = new Vector2(x * 16f, y * 16f);
            for (int i = 0; i < LavaDustCount; i++)
            {
                Vector2 position = corner + new Vector2(Main.rand.NextFloat(16f), Main.rand.NextFloat(16f));
                Vector2 velocity = new Vector2(Main.rand.NextFloat(-2.5f, 2.5f), Main.rand.NextFloat(-4.5f, -1f));
                Dust.NewDustPerfect(position, DustID.Lava, velocity, 0, default, Main.rand.NextFloat(1.2f, 1.9f));
            }

            for (int i = 0; i < EmberDustCount; i++)
            {
                Vector2 position = corner + new Vector2(Main.rand.NextFloat(16f), Main.rand.NextFloat(16f));
                Vector2 velocity = new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-3f, -0.5f));
                Dust ember = Dust.NewDustPerfect(position, DustID.Torch, velocity, 0, default, Main.rand.NextFloat(1.5f, 2.2f));
                ember.noGravity = true;
            }

            for (int i = 0; i < SmokeDustCount; i++)
            {
                Vector2 position = corner + new Vector2(Main.rand.NextFloat(16f), Main.rand.NextFloat(16f));
                Vector2 velocity = new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(-1.6f, -0.4f));
                Dust smoke = Dust.NewDustPerfect(position, DustID.Smoke, velocity, 120, default, Main.rand.NextFloat(1.0f, 1.5f));
                smoke.noGravity = true;
            }
        }
    }
}