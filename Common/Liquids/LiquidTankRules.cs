namespace Factorraria.Common.Liquids
{
    // Rules about machine tanks that both the old pipe networks and the new flow networks share.
    public static class LiquidTankRules
    {
        // Which input tank receives the liquid? A tank already holding it wins (even if full:
        // that blocks the pump); otherwise the first empty tank that accepts the liquid.
        public static LiquidStack FindSinkSlot(LiquidStack[] slots, int liquidType)
        {
            LiquidStack firstEmpty = null;
            foreach (var slot in slots)
            {
                if (!slot.Accepts(liquidType)) continue;
                if (slot.IsEmpty) { firstEmpty ??= slot; continue; }
                if (slot.LiquidType == liquidType) return slot;
            }
            return firstEmpty;
        }
    }
}