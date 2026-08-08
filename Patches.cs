using HarmonyLib;
using Restory.Gameplay.Inventory;

namespace RestoryQOL
{
    public static class Patches
    {
        public static void Apply(HarmonyLib.Harmony harmony)
        {
            // Wallet money bypass
            var walletTryRemove = AccessTools.Method(typeof(Wallet), nameof(Wallet.TryToRemove));
            harmony.Patch(walletTryRemove, new HarmonyMethod(typeof(Patches).GetMethod(nameof(TryToRemove_Prefix))));
            Core.Instance.LoggerInstance.Msg("[Harmony] Wallet.TryToRemove");

            var walletGetMoney = AccessTools.PropertyGetter(typeof(Wallet), "MoneyAvailable");
            harmony.Patch(walletGetMoney, new HarmonyMethod(typeof(Patches).GetMethod(nameof(GetMoneyAvailable_Prefix))));
            Core.Instance.LoggerInstance.Msg("[Harmony] Wallet.get_MoneyAvailable");

            // Skip logos — patch GUI_LogosIntroSequence.PrepareSequences via reflection
            var logosType = typeof(Wallet).Assembly.GetType("Restory.UserInterface.GUI_LogosIntroSequence");
            if (logosType != null)
            {
                var prepare = AccessTools.Method(logosType, "PrepareSequences");
                harmony.Patch(prepare, new HarmonyMethod(typeof(Patches).GetMethod(nameof(PrepareSequences_Prefix))));
                Core.Instance.LoggerInstance.Msg("[Harmony] GUI_LogosIntroSequence.PrepareSequences");
            }
            else
            {
                Core.Instance.LoggerInstance.Msg("[Harmony] GUI_LogosIntroSequence NOT FOUND");
            }
        }

        public static bool TryToRemove_Prefix(ref bool __result)
        {
            if (!Core.InfinityMoney.Value)
                return true;
            __result = true;
            return false;
        }

        public static bool GetMoneyAvailable_Prefix(ref int __result)
        {
            if (!Core.FakeMoneyUI.Value)
                return true;
            __result = 114514000;
            return false;
        }

        public static bool PrepareSequences_Prefix()
        {
            if (!Core.SkipLogos.Value)
                return true;

            Core.Instance.LoggerInstance.Msg("[SkipLogos] PrepareSequences intercepted.");
            return false;
        }
    }
}