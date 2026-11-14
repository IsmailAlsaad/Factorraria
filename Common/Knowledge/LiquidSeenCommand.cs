using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Factorraria.Common.Net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace Factorraria.Common.Knowledge
{
    // /fliquidseen        status of every liquid: has a biome rule / seen (biome visited) / product crafted / revealed in the book,
    //                     plus the zone flags the game gives YOUR player right now.
    // /fliquidseen poll   runs LiquidBiomeVisits.Poll once right now, then prints the status (shows what that poll newly revealed).
    // Reading it: stand in the biome and run it. If the zone flag is false the game does not think you are in the biome
    // (biomes need enough tiles). If the flag is true but "seen" stays false after "poll", the rule table or report is the problem.
    // If "seen" is true and "revealed" is true but the book still shows ???, the book UI is the problem.
    public class LiquidSeenCommand : ModCommand
    {
        public override string Command => "fliquidseen";
        public override CommandType Type => CommandType.Chat;
        public override string Usage => "/fliquidseen [poll]";
        public override string Description => "Debug: shows which liquids the recipe book considers seen/revealed, and the biome zones your player is in.";

        static string YN(bool value) => value ? "yes" : "no";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            Player p = caller.Player;

            if (args.Length > 0 && args[0].ToLowerInvariant() == "poll")
            {
                LiquidBiomeVisits.Poll(p);
                caller.Reply("Ran LiquidBiomeVisits.Poll once (net mode " + Main.netMode + ").", Color.LightGreen);
            }

            caller.Reply("Zones: forest/overworld=" + YN(p.ZoneOverworldHeight) + " underground=" + YN(p.ZoneDirtLayerHeight) + " cavern=" + YN(p.ZoneRockLayerHeight)
                + " underworld=" + YN(p.ZoneUnderworldHeight) + " snow=" + YN(p.ZoneSnow) + " desert=" + YN(p.ZoneDesert) + " ugDesert=" + YN(p.ZoneUndergroundDesert)
                + " jungle=" + YN(p.ZoneJungle) + " shroom=" + YN(p.ZoneGlowshroom) + " ocean=" + YN(p.ZoneBeach) + " corrupt=" + YN(p.ZoneCorrupt)
                + " crimson=" + YN(p.ZoneCrimson) + " hallow=" + YN(p.ZoneHallow) + " shimmer=" + YN(p.ZoneShimmer), Color.White);
            caller.Reply("Poll every " + LiquidBiomeVisits.PollTicks + " ticks. Seen markers stored: " + RecipeKnowledgeSystem.SeenItems.Count + ". (rule / seen / crafted / revealed)", Color.White);

            for (int id = 0; id < LiquidTypeRegistry.definitions.Count; id++)
            {
                bool rule = LiquidBiomeVisits.HasRule(id);
                bool seen = RecipeKnowledgeSystem.IsLiquidSeen(id);
                bool crafted = RecipeVisibility.IsProductCrafted(RecipeOutputKey.ForLiquid(id));
                bool revealed = RecipeVisibility.IsLiquidRevealed(id);
                caller.Reply("#" + id + " " + LiquidTypeRegistry.definitions[id].Name + ": " + YN(rule) + " / " + YN(seen) + " / " + YN(crafted) + " / " + YN(revealed),
                    revealed ? Color.LightGreen : Color.LightGray);
            }
        }
    }
}