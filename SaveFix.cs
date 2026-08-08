using System;
using System.Threading.Tasks;
using HarmonyLib;
using Restory.Gameplay.Inventory;
using UnityEngine;

namespace RestoryQOL
{
    public static class SaveFixPatches
    {
        public static void Apply(HarmonyLib.Harmony harmony)
        {
            var gameAsm = typeof(Wallet).Assembly;

            // Save hang fix: skip disk space check (DriveInfo.AvailableFreeSpace can block for 20+ seconds)
            var diskSpaceType = gameAsm.GetType("Restory.Data.SaveLoad.DiskSpaceService");
            if (diskSpaceType != null)
            {
                var isEnough = AccessTools.Method(diskSpaceType, "IsEnoughDiskSpace");
                harmony.Patch(isEnough, new HarmonyMethod(typeof(SaveFixPatches), nameof(DiskSpacePrefix)));
                Core.Instance.LoggerInstance.Msg("[SaveFix] DiskSpaceService.IsEnoughDiskSpace");
            }
            else
            {
                Core.Instance.LoggerInstance.Warning("[SaveFix] DiskSpaceService NOT FOUND");
            }

            var textureCacheType = gameAsm.GetType("Restory.Gameplay.TextureMasks.TextureCacheService");
            if (textureCacheType != null)
            {
                var waitMethod = AccessTools.Method(textureCacheType, "WaitForAllTexturesConversionCompletion");
                harmony.Patch(waitMethod, new HarmonyMethod(typeof(SaveFixPatches), nameof(WaitForTexturesPrefix)));
                Core.Instance.LoggerInstance.Msg("[SaveFix] TextureCacheService.WaitForAllTexturesConversionCompletion");
            }
            else
            {
                Core.Instance.LoggerInstance.Warning("[SaveFix] TextureCacheService NOT FOUND");
            }

            var pauseMenuType = gameAsm.GetType("Restory.UI.Presenters.PauseMenu.GUI_PauseMenu");
            if (pauseMenuType != null)
            {
                var resolveSave = AccessTools.Method(pauseMenuType, "ResolveOnSaveGameClick");
                harmony.Patch(resolveSave, new HarmonyMethod(typeof(SaveFixPatches), nameof(SaveFeedbackPrefix)));
                Core.Instance.LoggerInstance.Msg("[SaveFix] GUI_PauseMenu.ResolveOnSaveGameClick");
            }
            else
            {
                Core.Instance.LoggerInstance.Warning("[SaveFix] GUI_PauseMenu NOT FOUND");
            }
        }

        public static bool WaitForTexturesPrefix(ref Task __result)
        {
            if (!Core.FixSaveHang.Value) return true;
            __result = Task.CompletedTask;
            return false;
        }

        public static bool DiskSpacePrefix(ref bool __result)
        {
            if (!Core.FixSaveHang.Value) return true;
            __result = true;
            return false;
        }

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