using HarmonyLib;
using Restory.Gameplay.Inventory;

namespace RestoryQOL.Mods
{
    /// <summary>
    /// Makes the money UI display a huge fixed number regardless of the real
    /// wallet balance, so UI affordance checks always pass.
    /// </summary>
    [HarmonyPatch(typeof(Wallet))]
    [HarmonyPatch("get_MoneyAvailable")]
    internal static class FakeMoneyUI
    {
        [HarmonyPrefix]
        private static bool Prefix(ref int __result)
        {
            if (!Core.FakeMoneyUI.Value) return true;
            __result = 114514000;
            return false;
        }
    }
}
