using System;
using System.Threading.Tasks;
using HarmonyLib;

namespace RestoryQOL
{
    public static class SaveFixPatches
    {
        public static void Apply(HarmonyLib.Harmony harmony)
        {
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(SaveFixPatches));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Restory.Gameplay.TextureMasks.TextureCacheService), "WaitForAllTexturesConversionCompletion")]
        public static bool WaitForTexturesPrefix(ref Task __result)
        {
            if (!Core.FixSaveHang.Value) return true;
            __result = Task.CompletedTask;
            return false;
        }
        
        /// The game takes 20 seconds to query disk space on my device, so patching it to true.
        /// Not sure why it exists at all, I don't think people will run out of disk space for saving a game...
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Restory.Data.SaveLoad.DiskSpaceService), "IsEnoughDiskSpace")]
        public static bool DiskSpacePrefix(ref bool __result)
        {
            if (!Core.FixSaveHang.Value) return true;
            __result = true;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Restory.UI.Presenters.PauseMenu.GUI_PauseMenu), "ResolveOnSaveGameClick")]
        public static void SaveFeedbackPrefix(object __instance)
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