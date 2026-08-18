using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Restory.Data.Elements;
using Restory.Gameplay.Elements;
using Restory.Gameplay.Workplace;
using UnityEngine;

namespace RestoryQOL.Mods
{
    /// <summary>
    /// Press G to gather every loose part on the work surface back onto the
    /// mat. The game only auto-rescues a part that lands outside the mat when
    /// the detection state (re)opens, and never in competitions, so a part can
    /// get stuck out of reach.
    ///
    /// Uses the game's own placement system (ElementPlacementController +
    /// PlacementPositionFinder) to find free spots: each part is placed at an
    /// available position on the mat, spiralling outward from the default
    /// placement point exactly like the game does when it has to relocate a
    /// part (e.g. pulling something onto an already occupied desk). No manual
    /// grid is used. Screws (ElementCategory.Small) are left alone.
    ///
    /// The painting tool's colliders are on the InteractiveObjects layer, which
    /// the game's placement raycasts ignore, so parts can end up inside it.
    /// GatherParts builds an exclusion zone from the DevicePainter's own
    /// colliders and retries from alternate seeds when a candidate lands there.
    /// </summary>
    public static class GatherParts
    {
        /// <summary>Name of the painting tool's root in the equipment prefab.</summary>
        private const string PainterRootName = "DevicePainter";

        /// <summary>Extra space kept clear around the painting tool (world units).</summary>
        private const float PainterZonePadding = 0.15f;

        public static void Run()
        {
            if (!Core.GatherParts.Value) return;
            if (!Input.GetKeyDown(KeyCode.G)) return;

            try
            {
                Gather();
            }
            catch (Exception ex)
            {
                Core.Log.Warning($"[GatherParts] {ex}");
            }
        }

        private static void Gather()
        {
            var surface = UnityEngine.Object.FindAnyObjectByType<WorkSurface>();
            if (surface == null)
            {
                Core.Log.Msg("[GatherParts] Work surface not found.");
                return;
            }

            var elements = surface.PlacedElements
                .Where(element => element != null
                    && !element.IsDragging
                    && element.Info.Category != ElementCategory.Small) // screws, not parts
                .ToList();
            if (elements.Count == 0)
            {
                Core.Log.Msg("[GatherParts] No loose parts on the surface (screws are ignored).");
                return;
            }

            var placement = Resolve<ElementPlacementController>();
            if (placement == null)
            {
                Core.Log.Warning("[GatherParts] ElementPlacementController not resolved; aborting.");
                return;
            }

            var painterZone = BuildPainterZone();
            if (painterZone.Count == 0)
                Core.Log.Msg("[GatherParts] DevicePainter not found; no exclusion zone applied.");

            var defaultSeed = surface.DefaultPlacementPosition;
            var seeds = BuildSeeds(defaultSeed);

            var placed = 0;
            var awaiting = 0;
            foreach (var element in elements)
            {
                try
                {
                    placement.SetTargetElement(element);
                    if (TryPlaceAvoidingZone(placement, seeds, painterZone))
                    {
                        placement.SetPlacementPosition();
                        element.BehaviorSwitcher.SwitchToPlacedBehavior();
                        placed++;
                    }
                    else
                    {
                        awaiting++;
                        Core.Log.Warning(
                            $"[GatherParts] No free spot for {element.Info.ID}; left in place.");
                    }
                    placement.Clear();
                }
                catch (Exception ex)
                {
                    placement.Clear();
                    Core.Log.Warning($"[GatherParts] Failed to place {element.Info.ID}: {ex}");
                }
            }

            Core.Log.Msg($"[GatherParts] Gathered {placed} of {elements.Count} part(s) ({awaiting} left in place).");
        }

        /// <summary>
        /// Runs the game's placement search from each seed in turn, accepting
        /// the first candidate that does not fall inside the painting tool's
        /// exclusion zone.
        /// </summary>
        private static bool TryPlaceAvoidingZone(
            ElementPlacementController placement, Vector3[] seeds, List<Bounds> painterZone)
        {
            foreach (var seed in seeds)
            {
                Vector3 candidate;
                if (!placement.TryFindAvailablePlacementPosition(seed, out candidate))
                    continue;
                if (IsInsidePainterZone(candidate, painterZone))
                    continue;
                return true;
            }
            return false;
        }

        /// <summary>
        /// The default placement point, plus a few neighbours in each direction,
        /// so a spot rejected by the painter zone still has alternatives on the
        /// rest of the mat.
        /// </summary>
        private static Vector3[] BuildSeeds(Vector3 defaultSeed)
        {
            return new[]
            {
                defaultSeed,
                defaultSeed + new Vector3(0.35f, 0f, 0f),
                defaultSeed + new Vector3(-0.25f, 0f, 0.3f),
                defaultSeed + new Vector3(0f, 0f, 0.35f),
                defaultSeed + new Vector3(0.4f, 0f, 0.3f),
                defaultSeed + new Vector3(-0.3f, 0f, -0.25f)
            };
        }

        /// <summary>
        /// Collects the world-space bounds of the painting tool's colliders into
        /// a single exclusion zone. Empty when the painter is not present.
        /// </summary>
        private static List<Bounds> BuildPainterZone()
        {
            var zone = new List<Bounds>();

            var painter = GameObject.Find(PainterRootName);
            if (painter == null)
            {
                // Try the inactive form lookup as a fallback in case the tool
                // is currently disabled in the scene.
                painter = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                    .Select(t => t.gameObject)
                    .FirstOrDefault(go => go.name == PainterRootName);
            }
            if (painter == null) return zone;

            // Root collider plus every child collider.
            var colliders = new List<Collider>();
            colliders.AddRange(painter.GetComponents<Collider>());
            colliders.AddRange(painter.GetComponentsInChildren<Collider>(true));

            foreach (var collider in colliders)
            {
                if (collider == null || !collider.enabled) continue;
                if (collider.bounds.size.sqrMagnitude <= 0f) continue;
                zone.Add(collider.bounds);
            }

            if (zone.Count > 0)
                Core.Log.Msg($"[GatherParts] Painting tool exclusion zone: {zone.Count} collider(s).");
            return zone;
        }

        private static bool IsInsidePainterZone(Vector3 position, List<Bounds> zone)
        {
            foreach (var bounds in zone)
            {
                var b = bounds;
                b.Expand(PainterZonePadding * 2f);
                if (b.Contains(position))
                    return true;
            }
            return false;
        }

        private static T Resolve<T>() where T : class
        {
            // SceneContext lives in the core Zenject assembly, not
            // Zenject-usage (which the mod references for DiContainer).
            var sceneContextType = Type.GetType("Zenject.SceneContext, Zenject");
            if (sceneContextType == null)
            {
                Core.Log.Warning($"[GatherParts] SceneContext type not found resolving {typeof(T).Name}.");
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