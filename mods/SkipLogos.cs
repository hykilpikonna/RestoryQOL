using HarmonyLib;

namespace RestoryQOL.Mods;

public static class SkipLogos
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Restory.UserInterface.GUI_LogosIntroSequence), "PrepareSequences")]
    public static bool PrepareSequences_Prefix() => !Core.SkipLogos.Value;
}

