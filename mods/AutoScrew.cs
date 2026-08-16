using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Restory.Data.Equipment;
using Restory.Gameplay.Devices;
using Restory.Gameplay.Elements;
using Restory.Gameplay.Equipment;
using UnityEngine;

namespace RestoryQOL.Mods
{
    /// <summary>
    /// Hold X to instantly unscrew every visible screwed-in screw on the
    /// placed device, or Z to instantly screw in every loose one. Skips
    /// screws hidden behind panels (IsBlocked) and any screw currently
    /// mid-interaction. Replicates the game's AutoUnscrewing (power
    /// screwdriver) code path: jump the tween to its end state, fire the
    /// short-interaction event, then complete the interaction, so sockets,
    /// SFX, and device integrity all update through vanilla listeners.
    /// Unscrewed screws are thrown loose into the small-parts bin,
    /// exactly like vanilla leaving disassembly mode. Screw-in mirrors
    /// clicking the hole's projection: the coupled screw is re-attached
    /// from the bin, then driven to its tightened end state.
    /// Tween endpoints are the exact same local positions the vanilla tween
    /// would reach (PlayImmediately/PlayBackwardsImmediately set them
    /// directly), so screws land exactly where manual screwing would put them.
    /// </summary>
    public static class AutoScrew
    {
        private enum SkipReason { None, NoTweener, Busy }

        private static MethodInfo _completeMethod;
        private static MethodInfo _throwLooseMethod;
        private static MethodInfo _hideProjectionMethod;
        private static bool _warnedNoTool;
        private static readonly HashSet<ThreadedElement> _acted = new HashSet<ThreadedElement>();
        private static float _lastErrorLogTime = -999f;

        public static void Run()
        {
            if (!Core.AutoScrew.Value) return;
            var unscrew = Input.GetKey(KeyCode.X);
            var screwIn = Input.GetKey(KeyCode.Z);
            if (!unscrew && !screwIn)
            {
                _acted.Clear();
                _warnedNoTool = false;
                return;
            }

            var keyEdge = Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Z);

            try
            {
                var deviceService = UnityEngine.Object.FindAnyObjectByType<DeviceService>();
                var container = deviceService == null ? null : deviceService.PlacedDeviceContainer;
                if (container == null)
                {
                    if (keyEdge)
                        Core.Log.Msg("[AutoScrew] Key held, but no device is placed on the work surface.");
                    return;
                }

                var toolService = UnityEngine.Object.FindAnyObjectByType<UnscrewingToolSelectionService>();
                var tool = toolService == null ? null : toolService.CurrentlySelectedTool;
                if (tool == null)
                {
                    if (!_warnedNoTool)
                    {
                        _warnedNoTool = true;
                        Core.Log.Warning("[AutoScrew] No unscrewing tool available (buy/equip a screwdriver first).");
                    }
                    return;
                }

                int total = 0, acted = 0, blocked = 0, busy = 0, alreadyDone = 0, noTweener = 0;
                // NOTE: SortedSockets intentionally excludes Small-category
                // sockets, and most screws are Small. ElementSockets is the
                // full list.
                //
                // Unscrew (X) and screw-in (Z) enumerate differently because
                // that is how the game itself tracks them:
                //  - A tightened screw sits in socket.NestedElement.
                //  - After unscrewing, the socket detaches it and keeps a
                //    reference in socket.LastNestedElement (that is what the
                //    small-screw projection re-attaches when clicked), while
                //    the physical screw lives in the small-parts bin.
                foreach (var socket in container.Device.ElementSockets)
                {
                    if (socket == null) continue;

                    if (screwIn)
                    {
                        // Empty hole with a coupled screw waiting in the bin.
                        if (socket.NestedElement != null) { alreadyDone++; continue; }
                        if (socket.LastNestedElement is not ThreadedElement loose) continue;
                        total++;
                        if (_acted.Contains(loose)) continue;
                        // IsAvailable is the same gate vanilla uses before
                        // showing the hole's projection: it is false while a
                        // blocker part (cover, board, ...) that must be
                        // assembled first is still missing, which is exactly
                        // the "floating screw with no assembled socket" case.
                        if (!socket.IsAvailable) { blocked++; continue; }
                        switch (InstantInteract(null, socket, loose, tool))
                        {
                            case SkipReason.None:
                                _acted.Add(loose);
                                acted++;
                                break;
                            case SkipReason.Busy:
                                busy++;
                                break;
                            default:
                                noTweener++;
                                break;
                        }
                    }
                    else
                    {
                        if (socket.NestedElement is not ThreadedElement element) continue;
                        total++;
                        if (_acted.Contains(element)) continue;
                        if (element.IsBlocked) { blocked++; continue; }
                        if (element.IsInstalling) { alreadyDone++; continue; }
                        switch (InstantInteract(container.Device, null, element, tool))
                        {
                            case SkipReason.None:
                                _acted.Add(element);
                                acted++;
                                break;
                            case SkipReason.Busy:
                                busy++;
                                break;
                            default:
                                noTweener++;
                                break;
                        }
                    }
                }

                if (acted > 0)
                    Core.Log.Msg(
                        $"[AutoScrew] {(unscrew ? "Unscrewed" : "Screwed in")} {acted} screw(s).");
                else if (keyEdge)
                    Core.Log.Msg(
                        $"[AutoScrew] Nothing to do: {total} screw(s) found " +
                        $"({alreadyDone} already {(unscrew ? "loose" : "tightened")}, {blocked} blocked by a panel, " +
                        $"{busy} mid-animation, {noTweener} without tweener).");
            }
            catch (Exception ex)
            {
                if (UnityEngine.Time.unscaledTime - _lastErrorLogTime > 2f)
                {
                    _lastErrorLogTime = UnityEngine.Time.unscaledTime;
                    Core.Log.Warning($"[AutoScrew] {ex}");
                }
            }
        }

        /// <summary>
        /// Jumps a screw's tween straight to its end state and completes the
        /// interaction, replicating the power screwdriver's instant path.
        /// Unscrew additionally throws the screw loose into the small-parts
        /// bin, exactly like vanilla leaving disassembly mode. Screw-in first
        /// re-attaches the coupled screw from the bin to its socket, mirroring
        /// ElementSocket.ResolveProjectionActivated (clicking the hole's
        /// projection): AttachElement re-parents it, fires AttachToDevice
        /// which moves it to the near-final position and flags IsInstalling,
        /// then we drive the tween to the tightened end.
        /// </summary>
        private static SkipReason InstantInteract(Device device, ElementSocket socket, ThreadedElement element, UnscrewingToolInfo tool)
        {
            var elementTraverse = Traverse.Create(element);
            var tweener = elementTraverse.Field("tweener").GetValue();
            if (tweener == null) return SkipReason.NoTweener;
            if (IsTweenPlaying(tweener)) return SkipReason.Busy;

            // Exactly one of socket (screw-in) or device (unscrew) is set.
            if (socket != null)
            {
                // Vanilla ResolveProjectionActivated hides the hole's small
                // projection before attaching; skipping it leaves the screw
                // outline visible after the screw is back in.
                _hideProjectionMethod ??= typeof(ElementSocket).GetMethod("HideSmallElementProjection",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                _hideProjectionMethod?.Invoke(socket, null);
                socket.AttachElement(element);
            }

            var rotationTweener = elementTraverse.Field("rotationTweener").GetValue();
            var playMethod = element.IsInstalling ? "PlayBackwardsImmediately" : "PlayImmediately";
            Traverse.Create(tweener).Method(playMethod).GetValue();
            if (rotationTweener != null)
                Traverse.Create(rotationTweener).Method(playMethod).GetValue();

            element.OnImmediateShortInteraction.Invoke(element, tool);

            _completeMethod ??= typeof(ThreadedElement).GetMethod("CompleteInteraction",
                BindingFlags.NonPublic | BindingFlags.Instance);
            _completeMethod?.Invoke(element, null);

            if (device != null)
            {
                _throwLooseMethod ??= typeof(Device).GetMethod("ThrowLooseElement",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                _throwLooseMethod?.Invoke(device, new object[] { element });
            }
            return SkipReason.None;
        }

        private static bool IsTweenPlaying(object tweener)
        {
            var type = tweener.GetType();
            return type.GetProperty("IsPlaying") is { } prop
                ? (bool) prop.GetValue(tweener)
                : type.GetField("IsPlaying") is { } field && (bool) field.GetValue(tweener);
        }
    }
}
