using Terraria;

namespace Factorraria.Common.Net
{
    // Patch 9.4 (findings C4 and C6). NO packet lives here: the cart cargo needs none.
    //
    // Authority: carts are simulated on the server (or in single-player) only, so every world change a cart makes happens on the
    // authoritative side (CartCargo.Update, OnBump and PourTank return on a client). What is sent, and how:
    //  - A dropped world item a cart takes from (CartCargo.FinishWorldItemPickup): VirtualItemSystem.SyncWorldItem sends vanilla's SyncItem
    //    for that item index (switched off or emptied = type 0, a partly taken one = its new stack). Clients never touch it.
    //  - A conveyor vItem a cart takes from: the cart edits the server's real vItem (stackSize, Remove); VItemNet.ServerTick already
    //    sends the new stack or the removal to every subscribed client (7.2). The cart code reads VirtualItemSystem only on the server.
    //  - A spill is a new server vItem (SpawnVirtualItem), replicated the same way. Picking a cart up spawns its items with
    //    QuickSpawnItem on the server (9.3, Item.NewItem broadcasts by itself).
    //  - Liquid a terrarium drains or pours: CartCargo.SendWater marks the tile in LiquidBroadcastSystem (8.1), which sends it.
    //  - A pressure plate track: the server calls Wiring.HitSwitch directly (Cart.CheckPressurePlate); the wired device does its own sync.
    //
    // Everything below is TEMPORARY test code (remove in Phase 10): the counters, Describe, the two /fcarts lines that print them and the
    // rate-limited [CartCargo] log line. The behaviour in CartCargo and Cart stays.
    public static class CartCargoNet
    {
        public static int ChestWorldWhole;      // dropped items a chest cart took completely
        public static int ChestWorldPartial;    // dropped items a chest cart took only part of (the rest stays in the world, synced)
        public static int ChestRefused;         // times a chest cart left a dropped item alone (chest full or a filter refused); counts every tick it happens
        public static int FuelWorldWhole;
        public static int FuelWorldPartial;
        public static int ChestVItems;          // conveyor vItems a chest cart took from (whole or part)
        public static int FuelVItems;
        public static int ItemSyncs;            // SyncItem messages the server sent for cart pickups
        public static int LiquidMarks;          // liquid tiles handed to LiquidBroadcastSystem
        public static int PlatesHit;            // pressure plate tracks tripped by a cart

        static uint lastRefusedLog;

        /// <summary>A chest cart could not take a dropped item. Counts it and logs at most once every 300 ticks (about 5 seconds).</summary>
        public static void NoteChestRefused(int cartId, int itemType)
        {
            ChestRefused++;

            uint now = Main.GameUpdateCount;
            if (now - lastRefusedLog < 300u)
                return;

            lastRefusedLog = now;
            FactorrariaNet.Log("[CartCargo] cart " + cartId + " left a dropped item (type " + itemType + ") in the world: chest full or filtered; refused " + ChestRefused + " time(s) so far");
        }

        public static string Describe() => "cartCargo: chest world items whole " + ChestWorldWhole + " partial " + ChestWorldPartial + " refused " + ChestRefused
            + ", fuel world items whole " + FuelWorldWhole + " partial " + FuelWorldPartial
            + ", vItems taken chest " + ChestVItems + " fuel " + FuelVItems
            + ", SyncItem sent " + ItemSyncs + ", liquid tiles marked " + LiquidMarks + ", pressure plates hit " + PlatesHit;
    }
}