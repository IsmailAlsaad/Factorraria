using Microsoft.Xna.Framework;
using Factorraria.Content.Items.Discovery;
using Terraria;
using Terraria.ModLoader;

namespace Factorraria.Common.Knowledge
{
    /// <summary>
    /// Test tool for recipe discovery. Remove or hide behind a debug flag before release.
    ///   /recipes scroll [n]   give n sealed scrolls (default 1)
    ///   /recipes blank [n]    give n blank parchments (they roll a recipe on first use)
    ///   /recipes list         list recipes unlocked by parchment in this world
    ///   /recipes reset        forget every parchment unlock in this world
    /// </summary>
    public class RecipeDebugCommand : ModCommand
    {
        public override string Command => "recipes";
        public override CommandType Type => CommandType.Chat;
        public override string Usage => "/recipes scroll [n] | blank [n] | list | reset";
        public override string Description => "Recipe discovery debug tools";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            string sub = args.Length > 0 ? args[0].ToLowerInvariant() : "";
            int n = 1;
            if (args.Length > 1) int.TryParse(args[1], out n);
            n = System.Math.Clamp(n, 1, 20);

            switch (sub)
            {
                case "scroll":
                    caller.Player.QuickSpawnItem(caller.Player.GetSource_Misc("RecipeDebugCommand"), ModContent.ItemType<ClosedScrollItem>(), n);
                    caller.Reply($"Gave {n} sealed scroll(s).", Color.LightGreen);
                    break;
                case "blank":
                    caller.Player.QuickSpawnItem(caller.Player.GetSource_Misc("RecipeDebugCommand"), ModContent.ItemType<RecipeParchmentItem>(), n);
                    caller.Reply($"Gave {n} blank parchment(s).", Color.LightGreen);
                    break;
                case "list":
                    caller.Reply($"{RecipeKnowledgeSystem.UnlockedKeys.Count} recipe(s) unlocked by parchment:", Color.LightGreen);
                    foreach (string key in RecipeKnowledgeSystem.UnlockedKeys) caller.Reply("  " + key, Color.White);
                    break;
                case "reset":
                    RecipeKnowledgeSystem.ResetUnlocked();
                    caller.Reply("Cleared all parchment unlocks for this world.", Color.LightGreen);
                    break;
                default:
                    caller.Reply(Usage, Color.Yellow);
                    break;
            }
        }
    }
}