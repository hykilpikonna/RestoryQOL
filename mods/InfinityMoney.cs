using HarmonyLib;
using Restory.Gameplay.Inventory;

namespace RestoryQOL.Mods
{
    /// <summary>
    /// Bypasses wallet deduction on purchase — returns success without
    /// actually removing money from the player's wallet.
    /// </summary>
    [HarmonyPatch(typeof(Wallet), nameof(Wallet.TryToRemove))]
    internal static class InfinityMoney
    {
        [HarmonyPrefix]
        private static bool Prefix(ref bool __result)
        {
            if (!Core.InfinityMoney.Value) return true;
            __result = true;
            return false;
        }
    }
}
