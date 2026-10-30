using Factorraria.Common.Net;
using Factorraria.Common.Story;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace Factorraria.Content.NPCs.HerBackpack
{
    /// <summary>
    /// Her Backpack (NPC roadmap patch N4.1, rule M3): the Mechanic's lost backpack, lying in the dungeon. A rare spawn: SpawnChance lets it
    /// appear near a player in the dungeon after Skeletron (rules in RareNpcRules.BackpackWeight). It stands still, cannot be hurt and is not
    /// saved with the world, so it may despawn like any NPC when the player leaves. N4.1 has no interaction yet; the chat window with
    /// "Take it" and the Bag come in N4.2.
    /// Server (or single-player) spawns it and the vanilla NPC sync shows it to every client; no packet of its own.
    /// </summary>
    public class HerBackpackNPC : ModNPC
    {
        // Hitbox size (not const, so hot reload can change it). The drawn picture is the whole texture, centered on the hitbox.
        public static int BackpackWidth = 28;
        public static int BackpackHeight = 32;

        // Until HerBackpack.png exists the NPC borrows a vanilla item icon, so the mod loads without the art.
        string artPath = "Factorraria/Content/NPCs/HerBackpack/HerBackpack";
        public override string Texture => ModContent.HasAsset(artPath) ? artPath : "Terraria/Images/Item_" + ItemID.Barrel;

        // Placeholder name until the localization pass (N9).
        public override LocalizedText DisplayName => Language.GetOrRegister("Mods.Factorraria.Story.HerBackpackName", () => "Her Backpack");

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 1;

            // Not a town NPC: no Happiness button in its chat window (to confirm in game).
            NPCID.Sets.NoTownNPCHappiness[Type] = true;
            NPCID.Sets.NPCBestiaryDrawOffset[Type] = new NPCID.Sets.NPCBestiaryDrawModifiers { Hide = true };
        }

        public override void SetDefaults()
        {
            NPC.width = BackpackWidth;
            NPC.height = BackpackHeight;
            NPC.aiStyle = -1;
            NPC.friendly = true;
            NPC.damage = 0;
            NPC.defense = 0;
            NPC.lifeMax = 250;
            NPC.knockBackResist = 0f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.dontTakeDamage = true;
            NPC.immortal = true;
            NPC.chaseable = false;
            NPC.npcSlots = 0f;
            NPC.value = 0f;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;

            // Above 0 so the Lifeform Analyzer can list it (roadmap V12: confirm in game that a friendly NPC is listed).
            NPC.rarity = RareNpcRules.RareNpcRarity;
        }

        // Rare spawn rules: dungeon, after Skeletron, Bag not taken yet, one at a time (RareNpcRules.BackpackWeight).
        public override float SpawnChance(NPCSpawnInfo spawnInfo) => RareNpcRules.BackpackWeight(spawnInfo.Player, out _);

        // Right click opens the chat window with one button, "Take it" (the vanilla "Close" button is always there).
        // Placeholder texts until the localization pass (N9).
        public override bool CanChat() => true;

        public override string GetChat() => Language.GetOrRegister("Mods.Factorraria.Story.HerBackpackChat", () => "Looks dirty and full of wires.").Value;

        public override void SetChatButtons(ref string button, ref string button2)
        {
            button = Language.GetOrRegister("Mods.Factorraria.Story.HerBackpackTake", () => "Take it").Value;
        }

        // The server decides (StoryNet.HandleActionRequest): the Bag goes to the first player whose request arrives, the Backpack vanishes for everyone.
        public override void OnChatButtonClicked(bool firstButton, ref string shopName)
        {
            if (firstButton)
                StoryNet.RequestTakeBag(NPC.whoAmI);
        }

        public override void AI()
        {
            NPC.velocity = Vector2.Zero;
        }
    }
}