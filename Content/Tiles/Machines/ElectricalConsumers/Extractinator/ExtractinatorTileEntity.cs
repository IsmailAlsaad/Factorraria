using Factorraria.Common.Machines;
using Factorraria.Content.VirtualItems;
using Factorraria.Common.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Content.Tiles.Machines.ElectricalConsumers.Extractinator
{
    /// <summary>
    /// Electric extractinator: one input slot, no output slots. Every finished cycle eats one input,
    /// rolls its drop (ExtractinatorRecipeRegistry) and puts the result on a free tile of the 3x3 footprint as a vItem.
    /// With no free footprint tile the machine stops working until one frees up.
    /// </summary>
    public class ExtractinatorTileEntity : ElectricConsumerMachine, IMachineDynamicDraw
    {
        public override int ValidTileType => TileID.Extractinator;
        public override float PowerDemand => 150f;       // placeholder, tune while playtesting
        protected override int InputSlotCount => 1;
        protected override int WorkDuration => 15;      // ticks per input item (vanilla use time), placeholder
        const int InputBuffer = 50;                     // how many items conveyors may stack in the slot

        // Recipes stays null on purpose: the extractinator rolls random drops, which the CustomRecipe pipeline can't express.

        public override void Update()
        {
            base.Update();   // conveyor intake + grid switching

            Item slot = InputSlots[0];
            if (slot.IsAir || !ExtractinatorRecipeRegistry.TryGet(slot.type, out ExtractinatorRecipe recipe) || !TryFindConveyorOutputTile(out Point outputTile))
            {
                WorkProgress = 0;
                isWorking = false;
                return;
            }

            isWorking = true;     // counts toward the grid's demand even during a brownout
            if (!isOn) return;    // browned out: freeze progress

            WorkProgress++;
            if (WorkProgress < WorkDuration) return;
            WorkProgress = 0;

            slot.stack--;
            if (slot.stack <= 0) InputSlots[0] = new Item();
            CraftsFinished++;   // already synced to clients; DynamicUpdate watches it for the grab sound

            if (recipe.TryRoll(out int itemType, out int stack))
                VirtualItemSystem.SpawnVirtualItem(itemType, stack, outputTile.X, outputTile.Y);
        }

        // ---- Client-only effects: grab sound + dust. Nothing here is saved or sent. ----
        int lastSeenCrafts = -1;
        uint lastSeenDetailTick;
        int lastInputType;   // last input seen in the slot; the light sync carries no slots, so a replica with its UI closed falls back to this

        public void DynamicUpdate()
        {
            if (Main.dedServ) return;

            // Grab sound: one per consumed input, only while THIS machine's UI is open on this client.
            // First look or a fresh detail packet (opening the UI) re-baselines the counter instead of playing.
            bool consumed = false;
            if (lastSeenCrafts == -1 || LastDetailTick != lastSeenDetailTick)
            {
                lastSeenCrafts = CraftsFinished;
                lastSeenDetailTick = LastDetailTick;
            }
            else if (CraftsFinished != lastSeenCrafts)
            {
                consumed = CraftsFinished > lastSeenCrafts;
                lastSeenCrafts = CraftsFinished;
            }

            if (consumed && ModContent.GetInstance<MachineUISystem>().OpenMachine == this)
                SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.4f , PitchVariance = 0.5f, MaxInstances = 0}, MachineCenter);

            if (!InputSlots[0].IsAir) lastInputType = InputSlots[0].type;

            // Dust: only while running and on screen (nothing spawns off screen).
            if (!isOn || !isWorking || !IsOnScreen(MachineCenter)) return;
            if (!Main.rand.NextBool(3)) return;

            int dustType = GetInputBlockDust(lastInputType);
            if (dustType < 0) return;

            Dust d = Dust.NewDustPerfect(MachineCenter + Main.rand.NextVector2Circular(8f, 16f) + new Vector2(-9f,0f), dustType,
                new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1.5f, -0.3f)), 0, default, Main.rand.NextFloat(0.9f, 1.2f));
            d.noGravity = true;
        }

        // Dust of the block the input item places. Modded blocks use their own DustType; vanilla has no lookup, so the
        // extractable vanilla blocks are listed here (IDs from the wiki dust list; Desert Fossil is a guess, verify in game).
        int GetInputBlockDust(int itemType)
        {
            if (itemType <= ItemID.None) return -1;

            int tileType = ContentSamples.ItemsByType[itemType].createTile;
            if (tileType < 0) return -1;
            if (TileLoader.GetTile(tileType) is ModTile modTile) return modTile.DustType;

            return tileType switch
            {
                TileID.Silt => 53,
                TileID.Slush => 149,
                TileID.DesertFossil => 32,
                _ => -1,   // not a block (moss, junk), no dust
            };
        }

        public bool TryGetDynamicBounds(out Vector2 center, out float radius)
        {
            center = MachineCenter;
            radius = 0f;
            return false;   // nothing to draw, DynamicUpdate only
        }

        public void DynamicDraw(SpriteBatch spriteBatch) { }

        protected override bool TryGetIntakeSlot(int itemType, out int slot, out int limit)
        {
            slot = 0;
            limit = InputBuffer;
            if (!ExtractinatorRecipeRegistry.TryGet(itemType, out _)) return false;

            Item held = InputSlots[0];
            return held.IsAir || held.type == itemType;
        }

    }
}