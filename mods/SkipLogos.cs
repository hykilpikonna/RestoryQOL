using System.Reflection;
using HarmonyLib;
using Restory.Gameplay.Inventory;

namespace RestoryQOL.Mods
{
    /// <summary>
    /// Skips the startup logo sequence by intercepting PrepareSequences on
    /// GUI_LogosIntroSequence. The type is resolved via reflection because it
    /// is not directly referenceable from this project.
    /// </summary>
    [HarmonyPatch]
    internal static class SkipLogos
    {
        static MethodBase TargetMethod()
        {
            var logosType = typeof(Wallet).Assembly
                .GetType("Restory.UserInterface.GUI_LogosIntroSequence");
            if (logosType == null)
            {
                Core.Instance.LoggerInstance.Warning(
                    "[SkipLogos] GUI_LogosIntroSequence not found — patch skipped.");
                return null;
            }
            return AccessTools.Method(logosType, "PrepareSequences");
        }

        [HarmonyPrefix]
        private static bool Prefix()
        {
            if (!Core.SkipLogos.Value) return true;
            Core.Instance.LoggerInstance.Msg("[SkipLogos] PrepareSequences intercepted.");
            return false;
        }
    }
}
