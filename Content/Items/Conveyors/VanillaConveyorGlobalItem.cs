using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Items.Conveyors
{
    public class VanillaConveyorGlobalItem : GlobalItem
    {
        // Restrict this GlobalItem exclusively to vanilla conveyor belt items
        public override bool AppliesToEntity(Item entity, bool lateSearch)
        {
            return entity.type == ItemID.ConveyorBeltLeft || entity.type == ItemID.ConveyorBeltRight;
        }

        public override bool CanRightClick(Item item)
        {
            return Main.keyState.IsKeyDown(Keys.LeftShift) || Main.keyState.IsKeyDown(Keys.RightShift);
        }

        public override void RightClick(Item item, Player player)
        {
            int currentStack = item.stack;
            bool isFavorited = item.favorited;

            // Swap between vanilla left and right conveyor IDs
            int nextType = (item.type == ItemID.ConveyorBeltLeft) ? ItemID.ConveyorBeltRight : ItemID.ConveyorBeltLeft;

            // Re-initialize item properties to match the new item type
            item.SetDefaults(nextType);
            item.stack = currentStack;
            item.favorited = isFavorited;

            Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick);
        }

        public override bool ConsumeItem(Item item, Player player) => false;

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
        {
            // 1. Override the vanilla display name
            TooltipLine nameLine = tooltips.Find(line => line.Name == "ItemName" && line.Mod == "Terraria");
            if (nameLine != null)
            {
                nameLine.Text = (item.type == ItemID.ConveyorBeltLeft)
                    ? "Conveyor Belt (Clockwise)"
                    : "Conveyor Belt (Counter Clockwise)";
            }

            // 2. Add custom tooltip line
            tooltips.Add(new TooltipLine(Mod, "ConveyorSwapHint", "Shift + Right click in inventory to swap direction"));
        }
    }
}