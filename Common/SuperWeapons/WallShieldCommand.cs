using Factorraria.Content.Configs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.SuperWeapons
{
    // /fshield: debug command for the Wall of Flesh stone shield (super weapons patch S1.1).
    //   /fshield [status]   every Wall of Flesh part and every Hungry / leech alive on this side, with the shield flag and life
    //   /fshield break      drops the shield of the live wall (test command: needs EnableDebugs)
    //   /fshield raise      puts the shield back on the live wall (test command: needs EnableDebugs)
    //   /fshield flip       (S1.2, drawing only) the stone body strip trails the other side of the mouth
    //   /fshield offset <px>  (S1.2, drawing only) moves the front edge of the stone body strip away from the mouth (negative = toward it)
    // status runs on every side (a client shows its own copy of the flag, so it proves the sync). break and raise only work where the
    // simulation runs: single-player and the server console. Removed in patch S6 (roadmap section 11).
    public class WallShieldCommand : ModCommand
    {
        public override string Command => "fshield";
        public override CommandType Type => CommandType.Chat | CommandType.Console;
        public override string Usage => "/fshield [status|break|raise|flip|offset <px>]";
        public override string Description => "Wall of Flesh stone shield: status, break (test), raise (test).";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            string sub = args.Length > 0 ? args[0].ToLowerInvariant() : "status";

            if (sub == "status")
                Status(caller);
            else if (sub == "break" || sub == "raise")
                Change(caller, sub == "break");
            else if (sub == "flip")
                Flip(caller);
            else if (sub == "offset")
                Offset(caller, args);
            else
                caller.Reply("Usage: " + Usage, Color.OrangeRed);
        }

        static string Side()
        {
            return Main.netMode == NetmodeID.Server ? "server" : Main.netMode == NetmodeID.MultiplayerClient ? "client" : "single-player";
        }

        static void Status(CommandCaller caller)
        {
            int parts = 0;
            int unshielded = 0;
            caller.Reply("fshield [" + Side() + "]:", Color.LightGreen);

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active)
                    continue;

                if (WallShieldGlobalNPC.IsWallType(npc.type))
                {
                    parts++;
                    bool up = WallShieldGlobalNPC.IsShielded(npc);
                    string size = ", hitbox " + npc.width + "x" + npc.height;
                    if (!Main.dedServ)
                    {
                        Texture2D sheet = TextureAssets.Npc[npc.type].Value;
                        size += ", sheet " + sheet.Width + "x" + sheet.Height + ", frame " + npc.frame.Width + "x" + npc.frame.Height;
                    }

                    caller.Reply("  [" + i + "] " + npc.TypeName + ": shield " + (up ? "UP" : "down") + ", life " + npc.life + "/" + npc.lifeMax + size, up ? Color.LightSkyBlue : Color.Orange);
                }
                else if (npc.type == NPCID.TheHungry || npc.type == NPCID.TheHungryII || npc.type == NPCID.LeechHead || npc.type == NPCID.LeechBody || npc.type == NPCID.LeechTail)
                {
                    unshielded++;
                }
            }

            if (parts == 0)
                caller.Reply("  no Wall of Flesh alive on this side", Color.Gray);
            caller.Reply("  Hungry / leech NPCs alive (never shielded): " + unshielded, Color.Gray);
            if (!Main.dedServ)
                caller.Reply("  overlay art: " + ModContent.GetInstance<WallShieldDrawSystem>().ArtStatus() + " (a missing one draws as a grey block)", Color.Gray);
        }

        // S1.2: drawing settings of the stone overlay. They only exist where something is drawn.
        static void Flip(CommandCaller caller)
        {
            if (Main.dedServ)
            {
                caller.Reply("fshield flip is a drawing setting: it only works on a client or in single-player.", Color.OrangeRed);
                return;
            }

            WallShieldDrawSystem draw = ModContent.GetInstance<WallShieldDrawSystem>();
            draw.FlipBody = !draw.FlipBody;
            caller.Reply("fshield flip: the stone body now trails " + (draw.FlipBody ? "the other way (the side the mouth faces)" : "behind the mouth (default)"), Color.LightGreen);
        }

        static void Offset(CommandCaller caller, string[] args)
        {
            if (Main.dedServ)
            {
                caller.Reply("fshield offset is a drawing setting: it only works on a client or in single-player.", Color.OrangeRed);
                return;
            }

            WallShieldDrawSystem draw = ModContent.GetInstance<WallShieldDrawSystem>();
            if (args.Length > 1 && float.TryParse(args[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float pixels))
                draw.BodyStartOffsetPx = pixels;
            else if (args.Length > 1)
            {
                caller.Reply("Usage: /fshield offset <px>", Color.OrangeRed);
                return;
            }

            caller.Reply("fshield offset: the stone body starts " + draw.BodyStartOffsetPx + " px from the mouth's centre", Color.LightGreen);
        }

        static void Change(CommandCaller caller, bool breaking)
        {
            string what = breaking ? "break" : "raise";

            FurnaceOffsetConfig config = ModContent.GetInstance<FurnaceOffsetConfig>();
            if (config == null || !config.EnableDebugs)
            {
                caller.Reply("fshield " + what + " is a test command: turn on EnableDebugs in the Factorraria mod config first.", Color.OrangeRed);
                return;
            }

            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                caller.Reply("fshield " + what + " only works where the simulation runs: single-player or the server console.", Color.OrangeRed);
                return;
            }

            int changed = breaking ? WallShieldGlobalNPC.BreakShield() : WallShieldGlobalNPC.RaiseShield();
            caller.Reply("fshield " + what + " [" + Side() + "]: " + changed + " NPC(s) changed" + (changed == 0 ? " (no Wall of Flesh alive, or already in that state)" : ""), changed == 0 ? Color.Orange : Color.LightGreen);
        }
    }
}