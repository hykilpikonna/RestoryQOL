using System;
using System.Threading.Tasks;
using HarmonyLib;

namespace RestoryQOL.Mods
{
    public static class SaveFixPatches
    {
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
    }
}
