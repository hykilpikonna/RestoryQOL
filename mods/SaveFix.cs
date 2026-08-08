using System;
using System.Threading.Tasks;
using HarmonyLib;

namespace RestoryQOL.Mods
{
    /// <summary>
    /// Fixes the save hang by bypassing the texture conversion wait and the
    /// (very slow) disk-space check. Also shows a "Saving..." label in the
    /// pause menu when saving is triggered.
    /// </summary>
    internal static class SaveFix
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Restory.Gameplay.TextureMasks.TextureCacheService),
            "WaitForAllTexturesConversionCompletion")]
        private static bool WaitForTexturesPrefix(ref Task __result)
        {
            if (!Core.FixSaveHang.Value) return true;
            __result = Task.CompletedTask;
            return false;
        }

        /// <summary>
        /// The game takes ~20 seconds to query disk space; patch it to always
        /// return true. Disk space exhaustion is not a realistic failure mode
        /// for a save file.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Restory.Data.SaveLoad.DiskSpaceService), "IsEnoughDiskSpace")]
        private static bool DiskSpacePrefix(ref bool __result)
        {
            if (!Core.FixSaveHang.Value) return true;
            __result = true;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Restory.UI.Presenters.PauseMenu.GUI_PauseMenu),
            "ResolveOnSaveGameClick")]
        private static void SaveFeedbackPrefix(object __instance)
        {
            if (!Core.SaveFeedback.Value) return;
            try
            {
                var view = Traverse.Create(__instance).Field("view").GetValue();
                if (view == null) return;
                var saveInfo = Traverse.Create(view).Field("saveInfoText").GetValue();
                if (saveInfo == null) return;
                Traverse.Create(saveInfo).Property("text").SetValue("Saving...");
            }
            catch (Exception ex)
            {
                Core.Instance.LoggerInstance.Warning($"[SaveFix] {ex.Message}");
            }
        }
    }
}
