using HarmonyLib;
using Restory.Gameplay.Inventory;

namespace RestoryQOL.Mods
{
    public static class InfinityMoney
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Wallet), nameof(Wallet.TryToRemove))]
        public static bool TryToRemove_Prefix(ref bool __result)
        {
            if (!Core.InfinityMoney.Value)
                return true;
            __result = true;
            return false;
        }
    }
}
