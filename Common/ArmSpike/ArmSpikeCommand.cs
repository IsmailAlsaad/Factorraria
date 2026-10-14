using Microsoft.Xna.Framework;
using System;
using Terraria.ModLoader;

namespace Factorraria.Common.ArmSpike
{
    // THROWAWAY SPIKE (Robotic Arm, Phase 0b). Singleplayer chat command: /armspike
    public class ArmSpikeCommand : ModCommand
    {
        public override string Command => "armspike";
        public override CommandType Type => CommandType.Chat;
        public override string Usage => "/armspike <pick|axe|hammer|place> [ticks] [capture=none|onspawn] [filters=on|off] [via=direct|itemcheck]  |  /armspike info  |  /armspike stop";
        public override string Description => "Phase 0b spike: direct vanilla calls on the tile under the cursor, using your held item";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            ArmSpikeSystem system = ModContent.GetInstance<ArmSpikeSystem>();
            string sub = args.Length > 0 ? args[0].ToLowerInvariant() : "help";

            if (sub == "stop")
            {
                system.Stop();
                caller.Reply("Spike stopped.", Color.Yellow);
                return;
            }

            if (sub == "info")
            {
                foreach (string line in system.DescribeApi())
                {
                    system.Mod.Logger.Info("[ArmSpike] info: " + line);
                    caller.Reply(line, Color.LightSkyBlue);
                }
                return;
            }

            if (sub != "pick" && sub != "axe" && sub != "hammer" && sub != "place")
            {
                caller.Reply(Usage + "\nHold the tool or block, put the cursor on the target tile, type the command, then keep the mouse still until the RESULT lines appear.\nPer-strike details go to client.log (lines starting with [ArmSpike]).", Color.LightSkyBlue);
                return;
            }

            int ticks = 300;
            bool capture = false;
            bool filters = true;
            bool viaItemCheck = false;

            for (int i = 1; i < args.Length; i++)
            {
                string arg = args[i].ToLowerInvariant();
                if (int.TryParse(arg, out int number))
                    ticks = Math.Clamp(number, 1, 1200);
                else if (arg == "capture=onspawn")
                    capture = true;
                else if (arg == "capture=none")
                    capture = false;
                else if (arg == "filters=off")
                    filters = false;
                else if (arg == "filters=on")
                    filters = true;
                else if (arg == "via=itemcheck")
                    viaItemCheck = true;
                else if (arg == "via=direct")
                    viaItemCheck = false;
                else
                {
                    caller.Reply($"Unknown option '{args[i]}'.", Color.OrangeRed);
                    return;
                }
            }

            string error = system.TryStart(caller.Player, sub, viaItemCheck, capture, filters, ticks);
            if (error != null)
                caller.Reply(error, Color.OrangeRed);
            else
                caller.Reply($"Spike running (up to {ticks} ticks) on the cursor tile. Keep the mouse still.", Color.LightGreen);
        }
    }
}