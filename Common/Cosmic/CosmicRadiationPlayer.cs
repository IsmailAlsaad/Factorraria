using Factorraria.Common.Net;
using Factorraria.Content.Buffs;
using System;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace Factorraria.Common.Cosmic
{
    // C11: the radiation of cosmic particles on the player. CLIENT-LOCAL by design (see the roadmap, C11): each client counts the particles
    // it draws that fly within RadiationRadiusTiles of ITS OWN player (CosmicParticleSystem.CheckRadiation), so nothing is sent and nothing
    // is saved. The only things that reach other machines are what vanilla already syncs: the owner's buff list and the owner's life.
    //
    // Level: each hit adds the particle's multiplier (the same per-color multiplier as the ionization one). The level only exists while the
    // RadiationBuff is on the player, and goes back to 0 when the buff ends. Higher level = more Geiger clicks and faster life loss.
    // A full vanilla lead armor (Lead Helmet, Lead Chainmail, Lead Greaves) makes the player immune.
    public class CosmicRadiationPlayer : ModPlayer
    {
        public float Level;      // owner only, never saved, never sent
        public bool Shielded;    // true while the full lead armor is worn

        public static bool WearsFullLead(Player player)
        {
            return player.armor[0].type == ItemID.LeadHelmet
                && player.armor[1].type == ItemID.LeadChainmail
                && player.armor[2].type == ItemID.LeadGreaves;
        }

        public override void ResetEffects()
        {
            Shielded = false;
        }

        public override void PostUpdateEquips()
        {
            if (!WearsFullLead(Player))
                return;

            Shielded = true;

            string text = Language.GetTextValue("Mods.Factorraria.CosmicRadiation.LeadSetBonus");
            Player.setBonus = string.IsNullOrEmpty(Player.setBonus) ? text : Player.setBonus + "\n" + text;
        }

        // One cosmic particle came within the radius. Called by CosmicParticleSystem for the local player only.
        public void RegisterHit(float multiplier)
        {
            if (Shielded || Player.dead || !FactorrariaNet.IsOwner(Player))
                return;

            CosmicRadiationSystem s = CosmicRadiationSystem.Instance;
            multiplier = Math.Max(0f, multiplier);

            Level = Math.Min(Level + multiplier, Math.Max(0f, s.RadiationMaxLevel));

            int type = ModContent.BuffType<RadiationBuff>();
            int addTicks = (int)(multiplier * 60f);
            int capTicks = (int)(Math.Max(1f, s.RadiationMaxSeconds) * 60f);
            int index = Player.FindBuffIndex(type);

            if (index < 0)
                Player.AddBuff(type, Math.Min(Math.Max(1, addTicks), capTicks));
            else
                Player.buffTime[index] = Math.Min(Player.buffTime[index] + addTicks, capTicks);   // vanilla AddBuff would only keep the longer time
        }

        public override void PostUpdate()
        {
            if (!FactorrariaNet.IsOwner(Player))
                return;

            int type = ModContent.BuffType<RadiationBuff>();

            if (Shielded || Player.dead)
            {
                if (Player.HasBuff(type))
                    Player.ClearBuff(type);

                Level = 0f;
                return;
            }

            if (Level <= 0f)
                return;

            if (!Player.HasBuff(type))
            {
                Level = 0f;   // the debuff ran out (or was cleared): the radiation is gone
                return;
            }

            // Geiger counter: the chance of a click each tick grows with the level.
            float chance = Level * Math.Max(0f, CosmicRadiationSystem.Instance.RadiationClicksPerLevel) / 60f;
            if (Main.rand.NextFloat() < Math.Min(chance, 0.5f))
                CosmicAudioHooks.PlayGeigerCounterClick(Player.Center.X / 16f, Player.Center.Y / 16f);
        }

        // Life loss. lifeRegen is in half HP per second (2 = 1 HP/s). Runs on every client for every player, but Level is only ever
        // above 0 on the owner's own client, and the owner's life is what the server and the others follow.
        public override void UpdateBadLifeRegen()
        {
            if (Level <= 0f || Shielded || !Player.HasBuff(ModContent.BuffType<RadiationBuff>()))
                return;

            if (Player.lifeRegen > 0)
                Player.lifeRegen = 0;

            Player.lifeRegenTime = 0;
            Player.lifeRegen -= Math.Max(1, (int)Math.Round(Level * CosmicRadiationSystem.Instance.RadiationLifeRegenPerLevel));
        }
    }
}