using System;
using System.Linq;
using HarmonyLib;
using Restory.Gameplay.Disassemble.StateMachine;
using Restory.Gameplay.Devices;
using Restory.Gameplay.Elements;
using UnityEngine;

namespace RestoryQOL.Mods
{
    /// <summary>
    /// While holding ALT, releasing a dragged part snaps it into its socket
    /// even when the cursor is not precisely over the device. It does this by
    /// filling in ElementAssembleController.SelectedSocket with the nearest
    /// available compatible socket before the game's own CompleteDrag runs,
    /// so the entire vanilla attach flow (integrity checks, warnings,
    /// competition end) stays in place. Without ALT, behavior is unchanged,
    /// so disassembly works as usual.
    /// </summary>
    public static class SnapToSocket
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(DraggingDisassembleState), "CompleteDrag")]
        public static void CompleteDrag_Prefix(DraggingDisassembleState __instance)
        {
            if (!Core.SnapToSocket.Value || !Input.GetKey(KeyCode.LeftAlt)) return;

            try
            {
                var inst = Traverse.Create(__instance);
                var assembleController = inst.Field("elementAssembleController").GetValue<ElementAssembleController>();
                var element = inst.Field("selectedElement").GetValue<ElementBase>();
                if (assembleController == null || element == null) return;
                if (assembleController.SelectedSocket != null) return;

                var deviceService = inst.Field("deviceService").GetValue<DeviceService>();
                if (deviceService == null) return;

                var elementPosition = element.transform.position;
                var nearest = deviceService.GetAvailableSockets(element)
                    .OrderBy(s => (s.transform.position - elementPosition).sqrMagnitude)
                    .FirstOrDefault();
                if (nearest == null) return;

                Traverse.Create(assembleController).Property("SelectedSocket").SetValue(nearest);
                Core.Log.Msg($"[SnapToSocket] Snapped '{element.name}' into socket '{nearest.name}'.");
            }
            catch (Exception ex)
            {
                Core.Log.Warning($"[SnapToSocket] {ex}");
            }
        }
    }
}
