using HarmonyLib;
using Restory.Gameplay.Inventory;

namespace RestoryQOL.Mods
{
    [HarmonyPatch]
    public static class SkipLogos
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            var logosType = typeof(Wallet).Assembly.GetType("Restory.UserInterface.GUI_LogosIntroSequence");
            if (logosType != null)
            {
                return AccessTools.Method(logosType, "PrepareSequences");
            }
            else
            {
                Core.Instance?.LoggerInstance.Msg("[Harmony] GUI_LogosIntroSequence NOT FOUND");
                return null;
            }
        }

        [HarmonyPrefix]
        public static bool PrepareSequences_Prefix()
        {
            if (!Core.SkipLogos.Value)
                return true;

            Core.Instance.LoggerInstance.Msg("[SkipLogos] PrepareSequences intercepted.");
            return false;
        }
    }
}
