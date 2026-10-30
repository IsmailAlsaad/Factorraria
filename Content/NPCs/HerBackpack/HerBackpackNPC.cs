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

        // No interaction in N4.1 (the chat window comes in N4.2).
        public override bool CanChat() => false;

        public override void AI()
        {
            NPC.velocity = Vector2.Zero;
        }
    }
}