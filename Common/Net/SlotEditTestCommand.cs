using Microsoft.Xna.Framework;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // Temporary test command for patch 6.2 (remove in Phase 10). A Chat command runs on the client that typed it.
    //   /fslot          shows the slot edit counters of this side
    //   /fslot reject   the NEXT machine slot click is sent with a wrong expected item, so the server rejects it and the rollback of the slot
    //                   and the cursor item can be tested on demand (multiplayer client only)
    public class SlotEditTestCommand : ModCommand
    {
        public override string Command => "fslot";
        public override CommandType Type => CommandType.Chat | CommandType.Console;
        public override string Usage => "/fslot [reject|recipe]";
        public override string Description => "Multiplayer test (6.2): slot edit counters; 'reject' makes the next machine slot click get rejected on purpose.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            if (args.Length > 0 && args[0].ToLowerInvariant() == "reject")
            {
                MachineSlotEdit.CorruptNextRequest = true;
                caller.Reply("The next machine slot click will be sent wrong on purpose: the server rejects it and the click is rolled back.", Color.LightGreen);
                return;
            }

            if (args.Length > 0 && args[0].ToLowerInvariant() == "recipe")
            {
                MachineRecipeSelect.CorruptNextRequest = true;
                caller.Reply("The next recipe pick will be sent wrong on purpose: the server rejects it and the browser shows the real pick again.", Color.LightGreen);
                return;
            }

            caller.Reply(MachineSlotEdit.Describe() + " | " + MachineRecipeSelect.Describe(), Color.LightGreen);
        }
    }
}