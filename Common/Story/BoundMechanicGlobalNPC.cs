using Factorraria.Common.Net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Story
{
    /// <summary>
    /// Own art for the TIED Mechanic (vanilla NPCID.BoundMechanic) when she is NOT in the dungeon (NPC roadmap patch N10.7). A tied Mechanic in the
    /// dungeon keeps the vanilla art. Ismail makes the PNG: Content/NPCs/Overrides/BoundMechanic_Underground.png, exactly the size and frame layout of
    /// vanilla NPC_123 (a different size is refused with one log line that names both sizes).
    ///  - AUTHORITY / SYNC: none and no packet. It is purely cosmetic and client-only (the server never draws). Every client decides from the same world
    ///    data (the wall behind the NPC, which is in the world sync), so all clients see the same art.
    ///  - HOW: per draw, PreDraw swaps TextureAssets.Npc[BoundMechanic] for the own sheet and PostDraw puts the vanilla one back (the same trick
    ///    vanilla's DrawNPCCheckAlt uses for town NPC variations). Only this NPC's draw sees the swap.
    ///  - DUNGEON RULE: the NPC is "in the dungeon" when any wall in the 3x3 tiles around her centre is a dungeon wall (Main.wallDungeon).
    ///  - A missing PNG is not an error: the vanilla art stays and one log line says so.
    /// </summary>
    public class BoundMechanicGlobalNPC : GlobalNPC
    {
        public static string UndergroundTexturePath = "Factorraria/Content/NPCs/Overrides/BoundMechanic_Underground";

        static Asset<Texture2D> undergroundTexture;
        static Asset<Texture2D> vanillaTexture;
        static bool tried;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.BoundMechanic;

        public override void Unload()
        {
            undergroundTexture = null;
            vanillaTexture = null;
            tried = false;
        }

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (Main.dedServ || InDungeon(npc) || !TryLoad())
                return true;

            vanillaTexture = TextureAssets.Npc[NPCID.BoundMechanic];
            TextureAssets.Npc[NPCID.BoundMechanic] = undergroundTexture;
            return true;
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (vanillaTexture == null)
                return;

            TextureAssets.Npc[NPCID.BoundMechanic] = vanillaTexture;
            vanillaTexture = null;
        }

        static bool InDungeon(NPC npc)
        {
            Point center = npc.Center.ToTileCoordinates();
            for (int x = center.X - 1; x <= center.X + 1; x++)
            {
                for (int y = center.Y - 1; y <= center.Y + 1; y++)
                {
                    if (!WorldGen.InWorld(x, y, 1))
                        continue;

                    if (Main.wallDungeon[Main.tile[x, y].WallType])
                        return true;
                }
            }

            return false;
        }

        // Tries once. A missing or wrongly sized PNG keeps the vanilla art.
        static bool TryLoad()
        {
            if (tried)
                return undergroundTexture != null;

            tried = true;
            if (!ModContent.RequestIfExists<Texture2D>(UndergroundTexturePath, out Asset<Texture2D> asset, AssetRequestMode.ImmediateLoad))
            {
                FactorrariaNet.Log("[Story] Tied Mechanic art: " + UndergroundTexturePath + ".png does not exist yet, the vanilla art stays");
                return false;
            }

            Main.instance.LoadNPC(NPCID.BoundMechanic);
            Texture2D vanilla = TextureAssets.Npc[NPCID.BoundMechanic].Value;
            Texture2D own = asset.Value;
            if (own.Width != vanilla.Width || own.Height != vanilla.Height)
            {
                FactorrariaNet.Log("[Story] Tied Mechanic art: " + own.Width + "x" + own.Height + " does not match vanilla NPC_123 (" + vanilla.Width + "x" + vanilla.Height + "), the vanilla art stays");
                return false;
            }

            undergroundTexture = asset;
            FactorrariaNet.Log("[Story] Tied Mechanic art: own sheet loaded (" + own.Width + "x" + own.Height + ")");
            return true;
        }
    }
}