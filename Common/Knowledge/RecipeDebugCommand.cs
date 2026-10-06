using Microsoft.Xna.Framework;
using Factorraria.Common.Machines;
using Factorraria.Content.Items.Discovery;
using Terraria;
using Terraria.ModLoader;

namespace Factorraria.Common.Knowledge
{
    /// <summary>
    /// Test tool for recipe discovery. Remove or hide behind a debug flag before release.
    ///   /recipes scroll [pool] [n]   give n sealed scrolls of a pool (no pool = whole book; n defaults to 1)
    ///   /recipes blank [pool] [n]    give n unread parchments of a pool (no pool = whole book)
    ///   /recipes pools        list scroll pools and how many of their recipes are still available
    ///   /recipes list         list recipes unlocked by parchment in this world
    ///   /recipes crafted      list recipes crafted by a machine in this world
    ///   /recipes state        list every recipe that is not Hidden for you right now, with its state
    ///   /recipes reset        forget every parchment unlock and every crafted mark in this world
    /// </summary>
    public class RecipeDebugCommand : ModCommand
    {
        public override string Command => "recipes";
        public override CommandType Type => CommandType.Chat;
        public override string Usage => "/recipes scroll [pool] [n] | blank [pool] [n] | pools | list | crafted | state | reset";
        public override string Description => "Recipe discovery debug tools";

        static void Give(CommandCaller caller, int type, string pool, int n, string what)
        {
            Item item = new Item();
            item.SetDefaults(type);
            item.stack = n;
            if (item.ModItem is ClosedScrollItem scroll) scroll.PoolName = pool;
            else if (item.ModItem is RecipeParchmentItem parchment) parchment.PoolName = pool;
            caller.Player.QuickSpawnItem(caller.Player.GetSource_Misc("RecipeDebugCommand"), item);

            string from = pool == null ? "whole book" : "pool " + pool;
            caller.Reply($"Gave {n} {what}(s) ({from}).", Color.LightGreen);
        }

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            string sub = args.Length > 0 ? args[0].ToLowerInvariant() : "";

            // Optional extra args, any order: a number = count, anything else = scroll pool name (e.g. "scroll Dungeon 3").
            int n = 1;
            string poolArg = null;
            for (int a = 1; a < args.Length; a++)
            {
                if (int.TryParse(args[a], out int parsed)) n = parsed;
                else poolArg = args[a];
            }
            n = System.Math.Clamp(n, 1, 20);

            string pool = null;
            if (poolArg != null)
            {
                if (!ScrollPools.TryGet(poolArg, out ScrollPool found))
                {
                    caller.Reply($"No scroll pool named '{poolArg}'. Try /recipes pools.", Color.Yellow);
                    return;
                }
                pool = found.Name;
            }

            switch (sub)
            {
                case "scroll":
                    Give(caller, ModContent.ItemType<ClosedScrollItem>(), pool, n, "sealed scroll");
                    break;
                case "blank":
                    Give(caller, ModContent.ItemType<RecipeParchmentItem>(), pool, n, "unread parchment");
                    break;
                case "pools":
                    caller.Reply($"{ScrollPools.All.Count} scroll pool(s):", Color.LightGreen);
                    foreach (ScrollPool sp in ScrollPools.All)
                        caller.Reply($"  {sp.Name}: {ScrollPools.RemainingCount(sp)} of {sp.Entries.Count} recipe(s) still available", Color.White);
                    break;
                case "list":
                    caller.Reply($"{RecipeKnowledgeSystem.UnlockedKeys.Count} recipe(s) unlocked by parchment:", Color.LightGreen);
                    foreach (string key in RecipeKnowledgeSystem.UnlockedKeys) caller.Reply("  " + key, Color.White);
                    break;
                case "crafted":
                    caller.Reply($"{RecipeKnowledgeSystem.CraftedKeys.Count} recipe(s) crafted in this world:", Color.LightGreen);
                    foreach (string key in RecipeKnowledgeSystem.CraftedKeys) caller.Reply("  " + key, Color.White);
                    break;
                case "state":
                {
                    int shown = 0;
                    foreach (RecipeCatalog.Entry entry in RecipeCatalog.Entries())
                    {
                        RecipeState state = RecipeVisibility.GetState(entry.Key);
                        if (state == RecipeState.Hidden) continue;
                        shown++;
                        caller.Reply($"  {state,-16} {entry.Key}", Color.White);
                    }
                    caller.Reply($"{shown} recipe(s) not hidden.", Color.LightGreen);
                    break;
                }
                case "reset":
                    RecipeKnowledgeSystem.ResetUnlocked();
                    RecipeKnowledgeSystem.ResetCrafted();
                    caller.Reply("Cleared all parchment unlocks and crafted marks for this world.", Color.LightGreen);
                    break;
                default:
                    caller.Reply(Usage, Color.Yellow);
                    break;
            }
        }
    }
}