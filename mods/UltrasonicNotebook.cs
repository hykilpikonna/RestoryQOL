using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using Restory.Gameplay.Equipment.Ultrasonic;

namespace RestoryQOL.Mods
{
    /// <summary>
    /// Makes parts sitting in the ultrasonic (sonic bath) count as "on
    /// surface" in the notebook.
    ///
    /// Inserting an element into the bath calls workSurface.RemoveElement, so
    /// the note(...)pad (which only looks at the work surface) rebuilds and
    /// marks the socket of that part as missing. This postfix on
    /// GUI_NotepadElementsPanel.UpdateElements folds the bath's InsertedElements
    /// into the panel's cachedPlacedElements list, so the view can couple them
    /// to the empty sockets like any part lying on the table.
    /// </summary>
    [HarmonyPatch]
    public static class UltrasonicNotebookPatch
    {
        private static Type _notepadElementsPanelType;

        static MethodBase TargetMethod()
        {
            var gameAsm = typeof(Restory.Gameplay.Inventory.Wallet).Assembly;

            _notepadElementsPanelType = gameAsm.GetType("Restory.UI.Presenters.Notepad.GUI_NotepadElementsPanel");
            if (_notepadElementsPanelType == null)
            {
                Core.Log.Warning("[UltrasonicNotebook] GUI_NotepadElementsPanel not found; feature disabled.");
                return null;
            }

            var updateElements = AccessTools.Method(_notepadElementsPanelType, "UpdateElements");
            if (updateElements == null)
            {
                Core.Log.Warning("[UltrasonicNotebook] GUI_NotepadElementsPanel.UpdateElements not found; feature disabled.");
                return null;
            }
            Core.Log.Msg("[UltrasonicNotebook] GUI_NotepadElementsPanel.UpdateElements patched (postfix).");
            return updateElements;
        }

        [HarmonyPostfix]
        public static void UpdateElements_Postfix(object __instance)
        {
            if (Core.CountUltrasonicInNotebook == null || !Core.CountUltrasonicInNotebook.Value) return;

            try
            {
                var cachedPlaced = Traverse.Create(__instance).Field("cachedPlacedElements").GetValue() as IList;
                if (cachedPlaced == null) return;

                var bath = UnityEngine.Object.FindAnyObjectByType<SonicBath>();
                if (!bath) return;

                foreach (var entry in bath.InsertedElements)
                {
                    var element = entry.Key;
                    if (element == null || cachedPlaced.Contains(element)) continue;
                    cachedPlaced.Add(element);
                }
            }
            catch (Exception ex)
            {
                Core.Log.Warning($"[UltrasonicNotebook] {ex}");
            }
        }
    }
}