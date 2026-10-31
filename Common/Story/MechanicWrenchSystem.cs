using Factorraria.Common.Net;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Story
{
    /// <summary>
    /// The Mechanic has no combat wrench until she has her backpack (NPC roadmap patch N10.4, polish note 6). "Has her backpack" is exactly the world flag
    /// MechanicShopUnlocked (the Bag was handed in; on the Skeletron-first route her rescue sets it).
    ///  - AUTHORITY / SYNC: none and no packet. This ModSystem runs on the server and on every client and only reads the synced story flag, so every side
    ///    agrees within a tick of the flag changing. The server runs the attack decisions and the thrown projectile, the clients the animation.
    ///  - ATTACK: while the flag is off, NPCID.Sets.AttackType[NPCID.Mechanic] is -1 (tModLoader documents -1 as "this NPC won't attack"); when it is on,
    ///    it is the vanilla value (0 = she throws a projectile), captured at PostSetupContent. The vanilla value is put back on unload.
    ///  - ART: from the public tModLoader patches only attack types 1 and 3 draw a held item in code (DrawTownAttackGun / DrawTownAttackSwing); the Mechanic
    ///    is type 0, so the wrench drawn on her is part of her sprite sheet. While the flag is off, a client swaps TextureAssets.Npc[Mechanic] for Ismail's
    ///    own sheet without the wrench (NoWrenchTexturePath, same size and frame layout as vanilla NPC_124) and swaps the vanilla sheet back when it turns on.
    ///    If the PNG does not exist the swap is skipped and one log line says so (no crash, no placeholder art).
    /// </summary>
    public class MechanicWrenchSystem : ModSystem
    {
        // Not const, so hot reload can change them.
        public static string NoWrenchTexturePath = "Factorraria/Content/NPCs/Overrides/Mechanic_NoWrench";

        static bool captured;
        static int vanillaAttackType;

        static Asset<Texture2D> vanillaTexture;
        static Asset<Texture2D> noWrenchTexture;
        static bool noWrenchTried;
        static bool textureSwapped;

        /// <summary>True when the Mechanic may fight and show her wrench (the Bag was given, or the dungeon fork rescued her).</summary>
        public static bool WrenchAllowed => StorySystem.Get(StoryFlags.MechanicShopUnlocked);

        public override void PostSetupContent()
        {
            vanillaAttackType = NPCID.Sets.AttackType[NPCID.Mechanic];
            captured = true;
        }

        public override void Unload()
        {
            if (captured)
                NPCID.Sets.AttackType[NPCID.Mechanic] = vanillaAttackType;

            if (textureSwapped && vanillaTexture != null)
                TextureAssets.Npc[NPCID.Mechanic] = vanillaTexture;

            captured = false;
            vanillaTexture = null;
            noWrenchTexture = null;
            noWrenchTried = false;
            textureSwapped = false;
        }

        // Before the NPCs run their AI, so this tick's town NPC AI already sees the right value. Server and clients.
        public override void PreUpdateNPCs()
        {
            if (!captured)
                return;

            int wanted = WrenchAllowed ? vanillaAttackType : -1;
            if (NPCID.Sets.AttackType[NPCID.Mechanic] != wanted)
                NPCID.Sets.AttackType[NPCID.Mechanic] = wanted;
        }

        public override void PostUpdateEverything()
        {
            if (Main.dedServ)
                return;

            // In the main menu there is no story: show the vanilla art there.
            bool wantNoWrench = !Main.gameMenu && !WrenchAllowed;
            if (wantNoWrench == textureSwapped)
                return;

            if (wantNoWrench)
            {
                if (!TryLoadNoWrenchTexture())
                    return;

                Main.instance.LoadNPC(NPCID.Mechanic);
                if (vanillaTexture == null)
                    vanillaTexture = TextureAssets.Npc[NPCID.Mechanic];

                TextureAssets.Npc[NPCID.Mechanic] = noWrenchTexture;
                textureSwapped = true;
                FactorrariaNet.Log("[Story] Mechanic art: wrench-free sheet in use");
            }
            else
            {
                if (vanillaTexture != null)
                    TextureAssets.Npc[NPCID.Mechanic] = vanillaTexture;

                textureSwapped = false;
                FactorrariaNet.Log("[Story] Mechanic art: vanilla sheet back");
            }
        }

        // Tries once. A missing PNG is not an error: the vanilla art stays (Ismail makes the art himself).
        static bool TryLoadNoWrenchTexture()
        {
            if (noWrenchTried)
                return noWrenchTexture != null;

            noWrenchTried = true;
            if (ModContent.RequestIfExists<Texture2D>(NoWrenchTexturePath, out Asset<Texture2D> asset, AssetRequestMode.ImmediateLoad))
                noWrenchTexture = asset;
            else
                FactorrariaNet.Log("[Story] Mechanic art: " + NoWrenchTexturePath + ".png does not exist yet, her vanilla art (with the wrench) stays");

            return noWrenchTexture != null;
        }
    }
}