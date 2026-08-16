using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Restory.Gameplay.Devices;
using UnityEngine;
using UnityEngine.UI;

namespace RestoryQOL.Mods
{
    [HarmonyPatch]
    public static class PartsShopPatches
    {
        // Searched fields on the filter's view (NOT reliably present in all builds).
        private const string SelectedDeviceCategoryIndexField = "SelectedDeviceCategoryIndex";
        private const string SelectedModelIndexField = "SelectedModelIndex";
        private const string IsSortToggleOnField = "IsSortToggleOn";

        private static Type _elementsShopPageType;
        private static Type _elementsShopProductsPanelType;
        private static Type _elementsShopProductsPanelFilterType;
        private static Type _elementsShopElementType;
        private static Type _productPanelViewType;
        private static Type _deviceInfoType;
        private static Type _elementInfoType;
        private static Type _elementCategoryType;
        private static Type _deviceServiceType;
        private static Type _workSurfaceType;
        private static Type _placedElementsType;
        private static Type _elementTransformRecordType;

        private static bool _resolved;
        private static bool _resolveFailed;

        static MethodBase TargetMethod()
        {
            var gameAsm = typeof(Restory.Gameplay.Inventory.Wallet).Assembly;

            _elementsShopPageType = gameAsm.GetType("Restory.UI.Presenters.Shops.Elements.GUI_ElementsShopPage");
            _elementsShopProductsPanelType = gameAsm.GetType("Restory.UI.Presenters.Shops.Elements.GUI_ElementsShopProductsPanel");
            _elementsShopProductsPanelFilterType = gameAsm.GetType("Restory.UI.Presenters.Shops.Elements.GUI_ElementsShopProductsPanelFilter");
            _elementsShopElementType = gameAsm.GetType("Restory.UI.Presenters.Shops.Elements.GUI_ElementsShopElement");
            _productPanelViewType = gameAsm.GetType("Restory.UI.Views.Shops.Elements.GUI_ElementsShopProductsPanelView");
            _deviceInfoType = gameAsm.GetType("Restory.Data.Devices.DeviceInfo");
            _elementInfoType = gameAsm.GetType("Restory.Data.Elements.ElementInfo");
            _elementCategoryType = gameAsm.GetType("Restory.Data.Elements.ElementCategory");
            _deviceServiceType = gameAsm.GetType("Restory.Gameplay.Devices.DeviceService");
            _workSurfaceType = gameAsm.GetType("Restory.Gameplay.Workplace.WorkSurface");
            _placedElementsType = gameAsm.GetType("Restory.Gameplay.Elements.PlacedElements");
            _elementTransformRecordType = gameAsm.GetType("Restory.Gameplay.Elements.ElementTransformRecord");

            _resolved = _elementsShopPageType != null && _elementsShopProductsPanelType != null &&
                        _elementsShopProductsPanelFilterType != null && _elementsShopElementType != null &&
                        _productPanelViewType != null && _deviceInfoType != null && _elementInfoType != null &&
                        _elementCategoryType != null && _deviceServiceType != null && _workSurfaceType != null &&
                        _placedElementsType != null && _elementTransformRecordType != null;
            _resolveFailed = !_resolved;

            if (!_resolved)
            {
                Core.Log.Warning("[PartsShop] One or more game types not found; feature disabled.");
                return null;
            }

            // Hook the products panel's list rebuild rather than the page's Show:
            // rows are created inside UpdateShownProductsList, and any later
            // filter change (including AutoPartsPage's category selection on
            // first browser open) rebuilds the list, wiping overlays applied
            // earlier. Applying highlights/sorting here keeps them intact.
            var rebuildMethod = AccessTools.Method(_elementsShopProductsPanelType, "UpdateShownProductsList");
            if (rebuildMethod == null)
            {
                Core.Log.Warning("[PartsShop] Could not find GUI_ElementsShopProductsPanel.UpdateShownProductsList; feature disabled.");
                return null;
            }
            Core.Log.Msg("[PartsShop] GUI_ElementsShopProductsPanel.UpdateShownProductsList patched (postfix).");
            return rebuildMethod;
        }

        [HarmonyPostfix]
        public static void UpdateShownProductsList_Postfix(object __instance)
        {
            if (!_resolved || _resolveFailed) return;

            try
            {
                // __instance is the products panel; the rows have just been
                // (re)created and attached to the products list.
                var filter = Traverse.Create(__instance).Property("Filter").GetValue();
                if (filter == null) return;

                var filterTraverse = Traverse.Create(filter);

                // The currently placed (working) device. This is the same lookup the
                // mod's existing AutoPartsPage feature performs.
                var deviceService = UnityEngine.Object.FindAnyObjectByType(_deviceServiceType);
                if (deviceService == null) return;
                var container = Traverse.Create(deviceService).Property("PlacedDeviceContainer").GetValue();
                if (container == null) return;
                var device = Traverse.Create(container).Property("Device").GetValue();
                if (device == null) return;
                var deviceInfo = Traverse.Create(device).Property("Info").GetValue();
                if (deviceInfo == null || !_deviceInfoType.IsInstanceOfType(deviceInfo)) return;

                // Notebook order: DeviceInfo.Elements in source order.
                var elementsProp = Traverse.Create(deviceInfo).Property("Elements");
                var deviceOrderedParts = (from object it in (IEnumerable) elementsProp.GetValue() where it != null select it).ToList();

                var panelView = Traverse.Create(__instance).Field("view").GetValue();
                if (panelView == null || !_productPanelViewType.IsInstanceOfType(panelView)) return;
                var productsListParent = Traverse.Create(panelView).Field("productsListParent").GetValue() as RectTransform;
                if (productsListParent == null) return;

                // Is this a single-device (model) view? If so the filtered list maps
                // one-to-one to the device's ordered parts.
                var filterView = filterTraverse.Field("view").GetValue();
                if (filterView == null) return;
                var modelCount = GetCountOfIList(filterTraverse.Field("deviceModels").GetValue());
                var isSingleDevice = modelCount == 1;

                HashSet<object> missingElements = null;
                if (Core.HighlightMissingParts.Value)
                    missingElements = CollectMissingElements(container, device);

                if (Core.SortPartsByNotebook.Value && isSingleDevice && modelCount == 1)
                    SortProductsToNotebookOrder(productsListParent, filter, deviceOrderedParts);

                if (missingElements is { Count: > 0 })
                    HighlightMissingProducts(productsListParent, filter, missingElements);
            }
            catch (Exception ex)
            {
                Core.Log.Warning($"[PartsShop] {ex}");
            }
        }

        /// <summary>
        /// Reorders the shop product rows under productsListParent so that parts
        /// appear in the same order as the notebook (DeviceInfo.Elements).
        /// Items that are not part of the current device keep the original
        /// (price-sorted) relative order and are appended at the end.
        /// </summary>
        private static void SortProductsToNotebookOrder(
            RectTransform productsListParent,
            object filter,
            List<object> deviceOrderedParts)
        {
            var filteredItems = Traverse.Create(filter).Property("FilteredElementInfos").GetValue() as IList;
            if (filteredItems == null) return;

            var indexOfPart = new Dictionary<object, int>();
            for (var i = 0; i < deviceOrderedParts.Count; i++)
                indexOfPart[deviceOrderedParts[i]] = i;

            // Collect the shop item transforms in display order.
            var itemTransforms = new List<Transform>();
            for (var i = 0; i < productsListParent.childCount; i++)
            {
                var child = productsListParent.GetChild(i);
                if (child != null && child.GetComponent(_elementsShopElementType) != null)
                    itemTransforms.Add(child);
            }
            if (itemTransforms.Count == 0) return;

            // Map each shop item to its part's order key; -1 = not in device.
            var orderAndIndex = new List<KeyValuePair<int, int>>(itemTransforms.Count);
            for (var i = 0; i < itemTransforms.Count; i++)
            {
                var element = itemTransforms[i].GetComponent(_elementsShopElementType);
                if (element == null) { orderAndIndex.Add(new KeyValuePair<int, int>(-1, i)); continue; }
                var shopItemData = Traverse.Create(element).Property("ShopItemData").GetValue();
                var partInfo = shopItemData == null ? null : Traverse.Create(shopItemData).Field("Element").GetValue();
                orderAndIndex.Add(partInfo != null && indexOfPart.TryGetValue(partInfo, out var order)
                    ? new KeyValuePair<int, int>(order, i)
                    : new KeyValuePair<int, int>(-1, i));
            }

            // Stable sort: parts in notebook order first, then unmatched items
            // in their original (price-sorted) order.
            orderAndIndex.Sort((a, b) =>
            {
                if (a.Key == b.Key) return a.Value.CompareTo(b.Value);
                if (a.Key == -1) return 1;
                if (b.Key == -1) return -1;
                return a.Key.CompareTo(b.Key);
            });

            // Re-sibling in the new order (SetAsLastSibling preserves layout order).
            foreach (var t in orderAndIndex)
                itemTransforms[t.Value].SetAsLastSibling();
        }

        /// <summary>
        /// Computes the set of ElementInfo objects that the notebook would mark
        /// "missing": empty sockets that cannot all be covered by the matching
        /// elements currently lying on the work surface (quantity-aware).
        /// </summary>
        private static HashSet<object> CollectMissingElements(object container, object device)
        {
            var missing = new HashSet<object>();

            var socketList = Traverse.Create(device).Property("SortedSockets").GetValue() as IList;
            if (socketList == null) return missing;

            // Elements currently on the surface (workSurface.PlacedElements or the
            // dismantled pack's PlacedElements), mirroring the notebook's logic.
            var surfaceElements = new List<object>();
            var parentObj = container as UnityEngine.Object;
            if (parentObj == null) return missing;
            var parentTransform = (parentObj as Component)?.transform;
            if (parentTransform == null) return missing;
            var dismantledPackType = parentTransform.GetComponent<DismantledDevicePack>();
            if (dismantledPackType != null)
            {
                var packed = Traverse.Create(dismantledPackType).Property("PlacedElements").GetValue();
                AddSurfaceElementsFromPlaced(packed, surfaceElements);
            }
            else
            {
                var workSurface = UnityEngine.Object.FindAnyObjectByType(_workSurfaceType);
                if (workSurface != null)
                {
                    if (Traverse.Create(workSurface).Property("PlacedElements").GetValue() is IEnumerable placed)
                        surfaceElements.AddRange(placed.Cast<object>().Where(element => element != null));
                }
            }

            // Count available surface elements per ElementInfo so quantity
            // matters: one element lying on the surface can only satisfy one
            // empty socket. Without this, needing 2 of a part while having 1
            // would not flag the part as missing.
            var surfaceCounts = new Dictionary<object, int>();
            foreach (var info in from el in surfaceElements
                     where el != null
                     select Traverse.Create(el).Property("Info").GetValue()
                     into info
                     where info != null
                     select info)
            {
                surfaceCounts.TryGetValue(info, out var count);
                surfaceCounts[info] = count + 1;
            }

            foreach (var socket in socketList)
            {
                if (socket == null) continue;
                var socketTraverse = Traverse.Create(socket);
                var nested = socketTraverse.Property("NestedElement").GetValue();
                var compatible = socketTraverse.Property("CompatibleElementInfo").GetValue();
                if (nested != null || compatible == null) continue;

                if (surfaceCounts.TryGetValue(compatible, out var available) && available > 0)
                    surfaceCounts[compatible] = available - 1;
                else
                    missing.Add(compatible);
            }

            return missing;
        }

        private static void AddSurfaceElementsFromPlaced(object placedElements, List<object> surfaceElements)
        {
            if (placedElements == null || !_placedElementsType.IsInstanceOfType(placedElements)) return;
            if (Traverse.Create(placedElements).Property("ElementsOnSurface").GetValue() is not IEnumerable elementsOnSurface) return;
            surfaceElements.AddRange(from object rec in elementsOnSurface 
                where rec != null select Traverse.Create(rec).Field("Element").GetValue() 
                into it where it != null select it);
        }

        /// <summary>
        /// Adds a semi-transparent tint overlay on top of each shop product row
        /// whose part is in the missing set.
        /// </summary>
        private static void HighlightMissingProducts(
            RectTransform productsListParent,
            object filter,
            HashSet<object> missingElements)
        {
            var filteredItems = Traverse.Create(filter).Property("FilteredElementInfos").GetValue() as IList;
            if (filteredItems == null) return;

            for (var i = 0; i < productsListParent.childCount; i++)
            {
                var child = productsListParent.GetChild(i);
                if (child == null) continue;
                var element = child.GetComponent(_elementsShopElementType);
                if (element == null) continue;
                var shopItemData = Traverse.Create(element).Property("ShopItemData").GetValue();
                var partInfo = shopItemData == null ? null : Traverse.Create(shopItemData).Field("Element").GetValue();
                if (partInfo == null) continue;
                SetMissingOverlay(child, missingElements.Contains(partInfo));
            }
        }

        private static void SetMissingOverlay(Transform productRow, bool isMissing)
        {
            const string overlayName = "RestoryQOL_MissingHighlight";
            var containerTransform = productRow as RectTransform;
            if (containerTransform == null) return;

            var overlay = containerTransform.Find(overlayName);
            if (isMissing)
            {
                if (overlay != null) return;
                var go = new GameObject(overlayName, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(productRow, false);
                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                var img = go.GetComponent<Image>();
                img.color = new Color(1f, 0.35f, 0.2f, 0.18f);
                img.raycastTarget = false;
            }
            else if (overlay != null) UnityEngine.Object.Destroy(overlay.gameObject);
        }

        private static int GetCountOfIList(object value) => value switch
        {
            IList list => list.Count,
            ICollection collection => collection.Count,
            _ => -1
        };
    }
}
