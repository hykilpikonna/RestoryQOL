using System;
using HarmonyLib;

namespace RestoryQOL.Mods
{
    /// <summary>
    /// Resets the competition timer to zero when a competition attempt fails
    /// (an element is dropped outside its socket and the device is reset).
    /// The game's ResetDevice() restores element positions but leaves the
    /// timer running from its previous value.
    /// </summary>
    public static class ResetTimerOnFail
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Restory.Gameplay.Competitions.CompetitionGameMode), "ResetDevice")]
        public static void ResetDevice_Postfix(object __instance)
        {
            if (!Core.ResetTimerOnFail.Value) return;

            try
            {
                var instance = Traverse.Create(__instance);

                // No need to touch lastCheckTime: ProcessTimeChanged only
                // accumulates the delta since the previous tick.
                instance.Field("gameSecondsTimer").SetValue(0f);

                var guiTimer = instance.Field("guiCompetitionTimer").GetValue();
                if (guiTimer != null)
                    Traverse.Create(guiTimer).Method("UpdateTimer", 0f).GetValue();

                // Keep the tracked/saved competition time in sync so a save
                // after the fail captures 0 instead of the stale pre-fail value.
                var tracker = instance.Field("competitionsDeviceContainersTracker").GetValue();
                var device = instance.Field("currentDeviceInCompetition").GetValue();
                if (tracker != null && device != null)
                {
                    Traverse.Create(tracker)
                        .Method("TrySetNewCompetitionTimeForExistingCompetition",
                            new[] { device.GetType(), typeof(float), typeof(bool), typeof(bool) },
                            new[] { device, 0f, false, false })
                        .GetValue();
                }

                Core.Log.Msg("[ResetTimerOnFail] Competition timer reset to 0.");
            }
            catch (Exception ex)
            {
                Core.Log.Warning($"[ResetTimerOnFail] {ex}");
            }
        }
    }
}
