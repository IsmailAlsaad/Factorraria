using Factorraria.Common.Machines;
using Factorraria.Common.UI.RecipeBookUI;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;

namespace Factorraria.Common.Systems
{
    /// <summary>
    /// Registers the "show recipes using the hovered item" keybind (default I). Client only: the recipe book is local UI and
    /// reads knowledge that is already synced, so this needs no packets.
    /// </summary>
    public class RecipeBookKeybindSystem : ModSystem
    {
        public static ModKeybind ItemSearchKey { get; private set; }
        public static ModKeybind ToggleKey { get; private set; }

        public override void Load()
        {
            if (Main.dedServ) return;
            ItemSearchKey = KeybindLoader.RegisterKeybind(Mod, "RecipeBookItemSearch", Keys.I);
            ToggleKey = KeybindLoader.RegisterKeybind(Mod, "RecipeBookToggle", Keys.OemTilde);   // the ` key
        }

        public override void Unload()
        {
            ItemSearchKey = null;
            ToggleKey = null;
        }

        /// <summary>The key the player currently has bound (first one), for tooltips.</summary>
        public static string ItemSearchKeyName()
        {
            if (ItemSearchKey == null) return RecipeBookLayout.Text("KeyUnbound");
            var keys = ItemSearchKey.GetAssignedKeys();
            return keys.Count > 0 ? keys[0] : RecipeBookLayout.Text("KeyUnbound");
        }
    }

    /// <summary>Reads the keybind. ProcessTriggers only runs for the local player, and the keybind is blocked while the search bar is typing (WritingText).</summary>
    public class RecipeBookKeybindPlayer : ModPlayer
    {
        public override void ProcessTriggers(TriggersSet triggersSet)
        {
            if (Main.dedServ) return;

            // Toggle the book. Opening it also opens the inventory (OpenInternal sets Main.playerInventory), so it works from anywhere.
            if (RecipeBookKeybindSystem.ToggleKey != null && RecipeBookKeybindSystem.ToggleKey.JustPressed)
            {
                RecipeBookSystem.Toggle();
                return;
            }

            if (RecipeBookKeybindSystem.ItemSearchKey == null) return;
            if (!RecipeBookKeybindSystem.ItemSearchKey.JustPressed) return;
            if (!Main.playerInventory) return;

            Item hover = Main.HoverItem;   // set by the last draw
            if (hover == null || hover.IsAir) return;

            // No IsIngredient gate: an item no recipe uses still types "#Name" into the book, so the empty list is the feedback.
            RecipeBookSystem.OpenIngredientSearch(hover.type);
        }
    }
}