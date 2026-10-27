using Microsoft.Xna.Framework;
using System;
using System.Globalization;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Cosmic
{
    // /fcosmic: debug and tuning command for the cosmic radiation source. Stays (heat and params) after Phase C8; see the roadmap.
    //   /fcosmic heat [tileX]   the color order by section and each color's share of the particles at a column
    //   /fcosmic params         every balancing parameter with its value, unit and what it does
    public class CosmicCommand : ModCommand
    {
        public override string Command => "fcosmic";
        public override CommandType Type => CommandType.Chat | CommandType.Console;
        public override string Usage => "/fcosmic heat [tileX] | params";
        public override string Description => "Cosmic radiation (C1): show the world's color heatmap, or list every balancing parameter.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            string sub = args.Length > 0 ? args[0].ToLowerInvariant() : "";

            if (sub == "heat")
                Heat(caller, args);
            else if (sub == "params")
                Params(caller);
            else
                caller.Reply("Usage: " + Usage, Color.OrangeRed);
        }

        static string Side()
        {
            return Main.netMode == NetmodeID.Server ? "server" : Main.netMode == NetmodeID.MultiplayerClient ? "client" : "single-player";
        }

        static void Heat(CommandCaller caller, string[] args)
        {
            CosmicRadiationSystem system = CosmicRadiationSystem.Instance;
            CosmicHeatmap map = system.Heatmap;

            if (!map.Ready)
            {
                string hint = Main.netMode == NetmodeID.MultiplayerClient ? " (the server sends it on join; try /fjoin)" : " (it is generated on the first world tick)";
                caller.Reply("fcosmic heat [" + Side() + "]: no heatmap yet" + hint, Color.OrangeRed);
                return;
            }

            int worldTiles = Main.maxTilesX;
            float width = map.SectionWidth(worldTiles);

            caller.Reply("fcosmic heat [" + Side() + "]: world " + worldTiles + " tiles wide, 7 sections of " + width.ToString("0.#", CultureInfo.InvariantCulture) + " tiles", Color.LightGreen);

            for (int s = 0; s < 7; s++)
            {
                int from = (int)(s * width);
                int to = (int)((s + 1) * width) - 1;
                caller.Reply("  section " + s + " (tiles " + from + " to " + to + "): hot spot " + map.HotColorOfSection(s), CosmicColors.DrawColor(map.HotColorOfSection(s)));
            }

            int x;
            if (args.Length > 1 && int.TryParse(args[1], out int typed))
                x = typed;
            else if (caller.Player != null && !Main.dedServ)
                x = (int)(caller.Player.Center.X / 16f);
            else
            {
                caller.Reply("  give a tile X to see the shares there: /fcosmic heat <tileX>", Color.OrangeRed);
                return;
            }

            x = Math.Clamp(x, 0, worldTiles - 1);

            float total = 0f;
            float[] weights = new float[7];
            for (int c = 0; c < 7; c++)
            {
                weights[c] = map.Weight((CosmicColor)c, x, worldTiles, system.HeatFloor, system.HeatSigmaFraction);
                total += weights[c];
            }

            caller.Reply("  share of new particles at tile X " + x + " (section " + map.SectionAt(x, worldTiles) + "):", Color.LightGreen);
            for (int c = 0; c < 7; c++)
            {
                CosmicColor color = (CosmicColor)c;
                float share = total > 0f ? weights[c] / total * 100f : 0f;
                caller.Reply("    " + color + ": " + share.ToString("0.0", CultureInfo.InvariantCulture) + "%  (weight " + weights[c].ToString("0.000", CultureInfo.InvariantCulture) + ")", CosmicColors.DrawColor(color));
            }
        }

        static void Params(CommandCaller caller)
        {
            CosmicRadiationSystem system = CosmicRadiationSystem.Instance;
            caller.Reply("fcosmic params [" + Side() + "]: values on this side (they live as public fields on CosmicRadiationSystem). Per-color lists are in the order White, Violet, Blue, Green, Yellow, Orange, Red.", Color.LightGreen);

            foreach (FieldInfo field in typeof(CosmicRadiationSystem).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                CosmicParamAttribute info = field.GetCustomAttribute<CosmicParamAttribute>();
                if (info == null)
                    continue;

                string unit = string.IsNullOrEmpty(info.Unit) ? "" : " " + info.Unit;
                caller.Reply(field.Name + " = " + Format(field.GetValue(system)) + unit + "  -  " + info.Description, Color.White);
            }
        }

        static string Format(object value)
        {
            if (value is Array array)
            {
                string text = "";
                foreach (object item in array)
                    text += (text.Length > 0 ? ", " : "") + Format(item);
                return "[" + text + "]";
            }

            if (value is float f)
                return f.ToString("0.####", CultureInfo.InvariantCulture);

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }
}