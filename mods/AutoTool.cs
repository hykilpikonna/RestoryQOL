using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Restory.Data.Equipment;
using Restory.Gameplay.Disassemble.StateMachine;
using Restory.Gameplay.Elements;
using Restory.Gameplay.Equipment;
using Restory.Gameplay.Soldering;

namespace RestoryQOL.Mods
{
    /// <summary>
    /// Auto-selects the most useful tool when the cleaning screen opens:
    /// a dirty element starts with the last used cleaning tool (brush,
    /// air bottle, ...), an element that only needs soldering starts with
    /// the soldering iron, and a dirty+scorched element swaps to the iron
    /// automatically once the game switches to soldering mode.
    /// The game's single persisted tool slot cannot express this on its own.
    /// </summary>
    public static class AutoTool
    {
        private static CleaningToolInfo _lastCleaningTool;
        private static MethodInfo _needsCleaningMethod;
        private static MethodInfo _needsSolderingMethod;

        /// <summary>
        /// Remembers every cleaning tool the player (or the game) selects, so
        /// the memory survives the selection later switching to the soldering iron.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CleaningToolSelectionService), "SelectTool")]
        public static void SelectTool_Postfix(ElementCleanerToolInfoBase toolToSelect)
        {
            if (toolToSelect is CleaningToolInfo cleaningTool)
                _lastCleaningTool = cleaningTool;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CleaningDisassembleState), "Enter")]
        public static void Enter_Prefix(CleaningDisassembleState __instance, ElementBase selectedElement)
        {
            if (!Core.AutoTool.Value) return;

            try
            {
                var inst = Traverse.Create(__instance);
                var cleaner = inst.Field("elementCleaner").GetValue<ElementCleaner>();
                var selection = inst.Field("cleaningToolSelectionService").GetValue<CleaningToolSelectionService>();
                if (cleaner == null || selection == null || selectedElement == null) return;

                // The progress out-parameters live in an unreferenced Mandragora
                // assembly, so call through reflection with placeholder out slots.
                _needsCleaningMethod ??= typeof(ElementCleaner).GetMethod("IsElementNeedsCleaning");
                _needsSolderingMethod ??= typeof(ElementCleaner).GetMethod("IsElementNeedsSoldering");
                if (_needsCleaningMethod == null || _needsSolderingMethod == null) return;

                var needsCleaning = (bool) _needsCleaningMethod.Invoke(cleaner, new object[] { selectedElement, null, null });
                var needsSoldering = (bool) _needsSolderingMethod.Invoke(cleaner, new object[] { selectedElement, null, null });

                if (needsCleaning)
                    SelectCleaningTool(selection);
                else if (needsSoldering)
                    SelectSolderingTool(selection);
            }
            catch (Exception ex)
            {
                Core.Instance.LoggerInstance.Warning($"[AutoTool] {ex}");
            }
        }

        /// <summary>
        /// Once soot is cleaned off a scorched element, the game flips to
        /// soldering mode; swap the brush for the iron at the same moment.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(SolderingService), "SwitchFromCleaningToSolderingMode")]
        public static void SwitchToSolderingMode_Postfix(SolderingService __instance)
        {
            if (!Core.AutoTool.Value || !__instance.InSolderingMode) return;

            try
            {
                var selection = UnityEngine.Object.FindAnyObjectByType<CleaningToolSelectionService>();
                if (selection != null)
                    SelectSolderingTool(selection);
            }
            catch (Exception ex)
            {
                Core.Instance.LoggerInstance.Warning($"[AutoTool] {ex}");
            }
        }

        private static void SelectCleaningTool(CleaningToolSelectionService selection)
        {
            if (_lastCleaningTool != null && selection.TryToSelectTool(_lastCleaningTool))
                return;
            if (selection.CurrentlySelectedTool is CleaningToolInfo)
                return;
            selection.TryToSelectDefaultTool();
        }

        private static void SelectSolderingTool(CleaningToolSelectionService selection)
        {
            if (selection.CurrentlySelectedTool is SolderingToolInfo) return;

            var availableTools = Traverse.Create(selection).Field("availableTools").GetValue<AvailableToolsTrackingService>();
            if (availableTools == null) return;

            var iron = availableTools.AvailableTools
                .OfType<SolderingToolInfo>()
                .OrderByDescending(t => t.ToolLevel)
                .FirstOrDefault();
            if (iron != null)
                selection.TryToSelectTool(iron);
        }
    }
}
