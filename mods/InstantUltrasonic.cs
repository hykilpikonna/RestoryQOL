using System;
using HarmonyLib;

namespace RestoryQOL.Mods
{
    /// <summary>
    /// Makes the ultrasonic (sonic bath) cleaner finish instantly by
    /// shortening its cleaning duration to zero. The countdown then
    /// completes on the next game-time tick through the vanilla flow
    /// (effects stop, elements marked clean, "DONE" display), so the
    /// state machine and save/load behavior stay untouched.
    /// </summary>
    public static class InstantUltrasonic
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Restory.Gameplay.Equipment.Ultrasonic.SonicBath), "CleaningDuration", MethodType.Getter)]
        public static bool CleaningDuration_Prefix(ref TimeSpan __result)
        {
            if (!Core.InstantUltrasonic.Value) return true;
            __result = TimeSpan.Zero;
            return false;
        }
    }
}
