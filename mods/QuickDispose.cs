using System;
using HarmonyLib;
using Restory.Data.Elements.Condition;
using Restory.Gameplay.Devices;
using Restory.Gameplay.Disassemble.StateMachine;
using Restory.Gameplay.Elements;
using Restory.Gameplay.Equipment;
using Restory.Gameplay.Equipment.Ultrasonic;
using Restory.Gameplay.Recycle;
using Restory.Gameplay.Shredders;
using Restory.Gameplay.UserInterface;
using UnityEngine;
using Zenject;

namespace RestoryQOL.Mods
{
    /// <summary>
    /// Holding SHIFT while releasing a dragged part skips the need to aim at
    /// the matching station and routes the part by its condition instead:
    /// broken parts go to the shredder (falls back to the trash can), dirty
    /// parts go to the ultrasonic bath (falls back to the manual cleaner),
    /// and everything else goes to the parts box. Vanilla drag-and-drop onto
    /// a station still works when SHIFT is up. Routing the last part off a
    /// device removes the now-empty device box, matching a vanilla drop.
    ///
    /// This is a prefix on DraggingDisassembleState.ResolveButtonJustReleased
    /// (the Rewired release handler). Routing there, before the original, is
    /// the only point early enough: polling in OnUpdate runs a frame later,
    /// after the drag state has already exited and dropped the part.
    /// Returning false suppresses the vanilla drop so only our routing runs.
    /// </summary>
    [HarmonyPatch]
    public static class QuickDispose
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(DraggingDisassembleState), "ResolveButtonJustReleased");
        }

        [HarmonyPrefix]
        public static bool ResolveButtonJustReleased_Prefix(DraggingDisassembleState __instance)
        {
            if (!Core.QuickDispose.Value || !Input.GetKey(KeyCode.LeftShift)) return true;

            try
            {
                var inst = Traverse.Create(__instance);
                var element = inst.Field("selectedElement").GetValue<ElementBase>();
                if (element == null) return true;

                var condition = element.ConditionHandler.ElementData.Condition;
                switch (condition)
                {
                    case DamagedElementCondition _:
                        RouteBroken(inst, __instance, element);
                        break;
                    case DirtyElementCondition _:
                        RouteDirty(inst, __instance, element);
                        break;
                    default:
                        RouteGood(inst, __instance, element);
                        break;
                }
                CleanupIfDeviceEmptied(inst);
                return false; // we handled the release; skip the vanilla drop
            }
            catch (Exception ex)
            {
                Core.Log.Warning($"[QuickDispose] {ex}");
                return true; // on error, fall back to vanilla behavior
            }
        }

        /// <summary>
        /// Vanilla ResolveButtonJustReleased removes the empty device box once
        /// the last part leaves (DestroyDeviceContainer + EmptyDisassembleState).
        /// Our prefix suppresses that method, so replicate the cleanup here.
        /// Recycle/shred complete asynchronously and run the same check in their
        /// own response callback, so this is only needed for the sync routes.
        /// </summary>
        private static void CleanupIfDeviceEmptied(Traverse inst)
        {
            var deviceService = inst.Field("deviceService").GetValue<DeviceService>();
            var stateMachine = inst.Field("stateMachine").GetValue<DisassembleStateMachine>();
            if (deviceService == null || stateMachine == null) return;
            if (deviceService.PlacedDeviceContainer == null) return;
            if (!deviceService.IsPlacedDeviceCompletelyDisassembled()) return;

            deviceService.DestroyDeviceContainer();
            stateMachine.Enter<EmptyDisassembleState>();
        }

        /// <summary>Shredder (pays coins) first, trash can as fallback.</summary>
        private static void RouteBroken(Traverse inst, DraggingDisassembleState state, ElementBase element)
        {
            // Vanilla readiness also requires IsDetected (pointer hovering the
            // station), which is the aiming affordance this feature removes.
            // IsActive alone means the station is present and offering itself
            // for the current drag; SendShred/SendRecycle do their own
            // DamagedElementCondition validation, so this is safe.
            var shredder = Resolve<ShredderService>();
            if (shredder != null && Traverse.Create(shredder).Field("shredder").GetValue<Shredder>() is { } s && s.IsActive)
            {
                inst.Field("isShredding").SetValue(true);
                shredder.SendShredRequest(new ShredElementRequest(state, element));
                return;
            }

            var recycle = Resolve<RecycleService>();
            if (recycle != null && Traverse.Create(recycle).Field("trashCan").GetValue<TrashCan>() is { } t && t.IsActive)
            {
                inst.Field("isRecycling").SetValue(true);
                recycle.SendRecycleRequest(new ElementRecycleRequest(state, element));
                return;
            }

            Core.Log.Msg("[QuickDispose] No shredder or trash can available; dropped normally.");
        }

        /// <summary>Ultrasonic bath first, manual cleaner as fallback.</summary>
        /// <summary>
        /// Route by need, not just condition: a part that is actually dirty goes
        /// to the ultrasonic bath (which only cleans), but a clean part that
        /// only needs soldering goes straight to the soldering station, since
        /// the bath would leave it scorched.
        /// </summary>
        private static void RouteDirty(Traverse inst, DraggingDisassembleState state, ElementBase element)
        {
            var cleaner = inst.Field("elementCleaner").GetValue<ElementCleaner>();
            var cleaningData = cleaner == null ? null : cleaner.DraggingElementInitialCleaningData;
            // IsFullyCleaned means there is no dirt to wash off; if cleaning
            // data exists anyway, the part only needs soldering. CleaningProgress
            // lives in the unreferenced PWSMechanic assembly, so read it
            // reflectively (same approach as AutoTool's out-parameter calls).
            var needsCleaning = cleaningData != null && !IsFullyCleaned(cleaningData);

            if (needsCleaning)
            {
                var ultrasonic = inst.Field("ultrasonicService").GetValue<UltrasonicService>();
                bool bathOk = false;
                if (ultrasonic != null)
                {
                    // TryInsertElement needs the SonicBathElementFitter to hold fit
                    // data for the element, which vanilla only populates by hovering
                    // the part over the bath (TryFitElementToSonicBath). SHIFT-drop
                    // skips that, so prime the fitter with the bath's own position
                    // first. TryInsertElementToSonicBath still validates the rest
                    // (active tool, not running, not full, not damaged).
                    var bath = Traverse.Create(ultrasonic).Field("sonicBath").GetValue<SonicBath>();
                    if (bath != null && bath.ActiveTool != null)
                        ultrasonic.TryFitElementToSonicBath(element, bath.transform.position);
                    bathOk = ultrasonic.TryInsertElementToSonicBath(element);
                }
                if (bathOk)
                {
                    inst.Field("stateMachine").GetValue<DisassembleStateMachine>().Enter<DetectionDisassembleState>();
                    return;
                }
            }

            // Soldering-only part, or the bath refused a dirty one: open the
            // cleaning/soldering station exactly like dropping it there.
            var panel = Resolve<GUI_ElementCleanerPanel>();
            var stateMachine = inst.Field("stateMachine").GetValue<DisassembleStateMachine>();
            if (cleaner == null || panel == null || stateMachine == null) return;
            if (cleaningData == null) return;

            panel.Init(element, cleaningData);
            panel.Show();
            element.IsDragging = false;
            stateMachine.Enter<TransitionToCleaningDisassembleState, ElementBase>(element);
        }

        /// <summary>Into the parts box (storage rejects damaged parts itself).</summary>
        private static void RouteGood(Traverse inst, DraggingDisassembleState state, ElementBase element)
        {
            var elementService = inst.Field("elementService").GetValue<ElementService>();
            var stateMachine = inst.Field("stateMachine").GetValue<DisassembleStateMachine>();
            if (elementService == null || stateMachine == null) return;

            if (elementService.TrySendItemToStorage(element))
                stateMachine.Enter<DetectionDisassembleState>();
        }

        private static bool IsFullyCleaned(Restory.Gameplay.Cleaning.InitialCleaningData data)
        {
            // CleaningProgress is a PWSMechanic value type the mod doesn't
            // reference, so read both the property and IsFullyCleaned via
            // reflection (boxed object), never naming the type at compile time.
            var progress = Traverse.Create(data).Property("CleaningProgress").GetValue();
            if (progress == null) return true;
            var method = progress.GetType().GetMethod("IsFullyCleaned");
            return method == null || (bool) method.Invoke(progress, null);
        }

        private static T Resolve<T>() where T : class
        {
            // SceneContext lives in the core Zenject assembly, not
            // Zenject-usage (which the mod references for DiContainer).
            var sceneContextType = Type.GetType("Zenject.SceneContext, Zenject");
            if (sceneContextType == null)
            {
                Core.Log.Warning($"[QuickDispose] SceneContext type not found resolving {typeof(T).Name}.");
                return null;
            }

            foreach (var context in UnityEngine.Object.FindObjectsByType(sceneContextType, FindObjectsSortMode.None))
            {
                var component = context as Component;
                if (component == null || !component.gameObject.activeInHierarchy) continue;
                try
                {
                    var container = Traverse.Create(component).Property("Container").GetValue();
                    if (container == null) continue;
                    return Traverse.Create(container).Method("Resolve", typeof(T)).GetValue<T>();
                }
                catch
                {
                    // Not bound in this scene's container; try the next one.
                }
            }
            return null;
        }
    }
}
