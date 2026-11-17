using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Systems
{
    // Lava slowly turns stone into hellstone in the underworld.
    // Multiplayer: PostUpdateWorld never runs on a client, so only the server (or single player) decides anything. A converted tile
    // is replicated with a vanilla tile square, so there is no packet of our own. Only players' surroundings are sampled
    // (like vanilla random tile updates), so nothing grows in places no one is near.
    // Growth rule: a stone block becomes hellstone when it touches lava (distance 1), or when it is within MaxDistance of lava
    // AND a neighbouring hellstone block is closer to the lava than it is. That makes the hellstone grow outward from the contact
    // point instead of appearing at random. Distance is counted in orthogonal steps (a diamond, not a square).
    // Expected time per block = BaseSecondsToConvert * DistanceSlowdown^(distance - 1). All values are plain fields, so hot reload can tweak them.
    public class HellstoneGrowthSystem : ModSystem
    {
        public bool Enabled = true;
        public int MaxDistance = 4;
        public float BaseSecondsToConvert = 5f;
        public float DistanceSlowdown = 2f;
        public int MinLavaAmount = 16;

        public int SamplesPerPlayerPerTick = 40;
        public int AreaHalfWidth = 100;
        public int AreaHalfHeight = 60;

        int[] boxLeft = new int[Main.maxPlayers];
        int[] boxRight = new int[Main.maxPlayers];
        int[] boxTop = new int[Main.maxPlayers];
        int[] boxBottom = new int[Main.maxPlayers];
        float[] boxSampleRate = new float[Main.maxPlayers];
        int boxCount;

        public override void PostUpdateWorld()
        {
            if (!Enabled || Main.netMode == NetmodeID.MultiplayerClient)
                return;

            boxCount = 0;
            int bottomLimit = Main.maxTilesY - 3;
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
        }

        void TryGrow(int x, int y)
        {
            Tile tile = Main.tile[x, y];
            if (!tile.HasUnactuatedTile || tile.TileType != TileID.Stone)
                return;

            int distance = LavaDistance(x, y);
            if (distance <= 0)
                return;
            if (distance > 1 && !HasCloserHellstone(x, y, distance))
                return;

            // Odds per tick that this block is picked at all (several players' areas can overlap, so add them up),
            // so the expected time stays the same however many samples or players there are.
            float sampleRate = 0f;
            for (int b = 0; b < boxCount; b++)
            {
                if (x >= boxLeft[b] && x <= boxRight[b] && y >= boxTop[b] && y <= boxBottom[b])
                    sampleRate += boxSampleRate[b];
            }
            if (sampleRate <= 0f)
                return;

            float seconds = BaseSecondsToConvert * (float)System.Math.Pow(DistanceSlowdown, distance - 1);
            float chance = 1f / (seconds * 60f * sampleRate);
            if (Main.rand.NextFloat() >= chance)
                return;

            Transform(x, y);
        }

        // Smallest number of orthogonal steps from (x, y) to a lava cell within MaxDistance, or 0 when there is none.
        int LavaDistance(int x, int y)
        {
            int max = System.Math.Max(1, MaxDistance);
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

        void Transform(int x, int y)
        {
            Tile tile = Main.tile[x, y];
            tile.TileType = TileID.Hellstone;
            WorldGen.SquareTileFrame(x, y, true);

            // Neighbours can change frame too (stone and hellstone merge), so send the 3x3 around it.
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendTileSquare(-1, x - 1, y - 1, 3, 3);
        }
    }
}