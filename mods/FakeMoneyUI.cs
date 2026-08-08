using HarmonyLib;
using Restory.Gameplay.Inventory;

namespace RestoryQOL.Mods
{
    public static class FakeMoneyUI
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Wallet), "get_MoneyAvailable")]
        public static bool GetMoneyAvailable_Prefix(ref int __result)
        {
            if (!Core.InfiniteMoney.Value)
                return true;
            __result = 114514000;
            return false;
        }
    }
}
