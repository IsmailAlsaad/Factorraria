using Terraria.ID;

namespace Factorraria.Common.Carts
{
    /// <summary>
    /// A cart's skin is the vanilla (or modded) minecart ITEM it imitates. It is saved as the item's internal name
    /// ("Terraria/Minecart"), never as a numeric id, because ids shift between loads (same reasoning as RecipeKey).
    /// </summary>
    public static class CartSkins
    {
        public const string VanillaPrefix = "Terraria/";
        public const string DefaultKey = "Terraria/Minecart";

        /// <summary>Item type -> save key. Vanilla names get the "Terraria/" prefix; modded names already carry "Mod/Name".</summary>
        public static string KeyOf(int itemType)
        {
            if (itemType <= 0)
            {
                return DefaultKey;
            }

            string name = ItemID.Search.GetName(itemType);
            if (string.IsNullOrEmpty(name))
            {
                return DefaultKey;
            }

            return name.Contains("/") ? name : VanillaPrefix + name;
        }

        /// <summary>Save key -> item type. Returns false if that item no longer exists (mod removed, etc).</summary>
        public static bool TryTypeOf(string key, out int itemType)
        {
            itemType = 0;

            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            if (ItemID.Search.TryGetId(key, out itemType))
            {
                return true;
            }

            if (key.StartsWith(VanillaPrefix) && ItemID.Search.TryGetId(key.Substring(VanillaPrefix.Length), out itemType))
            {
                return true;
            }

            itemType = 0;
            return false;
        }
    }
}