using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Restory.Gameplay.Devices;

namespace RestoryQOL.Mods
{
    /// <summary>
    /// Sorts the parts box (InventoryPanel) by device, then assembly order.
    ///
    /// Vanilla sorts the box purely by element condition, which scatters the
    /// parts of the same device all over the list. Instead we order parts so
    /// that they appear exactly like the notebook: each device's parts grouped
    /// together, and within a device the elements appear in assembly order.
    ///
    /// A part is attributed to the FIRST device (in master
    /// DeviceInfoDatabase.Devices order) that contains it, so:
    ///   - single-device tab  -> parts follow that device's assembly order;
    ///   - "all models" tab   -> clean per-device grouping, device order ==
    ///     the notebook's device order.
    /// </summary>
    [HarmonyPatch]
    public static class PartsBoxSortPatches
    {
        private static Type _inventoryPanelType;
        private static Type _deviceInfoDatabaseType;
        private static Type _storageItemElementType;
        internal static bool _resolved;
        internal static bool _resolveFailed;

        static MethodBase TargetMethod()
        {
            var gameAsm = typeof(Restory.Gameplay.Inventory.Wallet).Assembly;

            _inventoryPanelType = gameAsm.GetType("Restory.UI.Presenters.Inventory.InventoryPanel");
            _deviceInfoDatabaseType = gameAsm.GetType("Restory.Data.Devices.DeviceInfoDatabase");
            _storageItemElementType = gameAsm.GetType("Restory.StorageSystem.StorageElements.StorageItemElement");

            _resolved = _inventoryPanelType != null && _deviceInfoDatabaseType != null &&
                        _storageItemElementType != null;
            _resolveFailed = !_resolved;

            if (!_resolved)
            {
                Core.Log.Warning("[PartsBoxSort] Game types not found; feature disabled.");
                return null;
            }

            var sortItems = AccessTools.Method(_inventoryPanelType, "SortItems");
            if (sortItems == null)
            {
                Core.Log.Warning("[PartsBoxSort] InventoryPanel.SortItems not found; feature disabled.");
                return null;
            }
            Core.Log.Msg("[PartsBoxSort] InventoryPanel.SortItems patched (postfix).");
            return sortItems;
        }

        [HarmonyPostfix]
        public static void SortItems_Postfix(object __instance)
        {
            if (!_resolved || _resolveFailed) return;
            if (Core.SortBoxParts == null || !Core.SortBoxParts.Value) return;

            try
            {
                SortByDeviceAssembly(__instance);
            }
            catch (Exception ex)
            {
                Core.Log.Warning($"[PartsBox] {ex}");
            }
        }

        /// <summary>
        /// Re-orders InventoryPanel.filteredItems in place so parts appear in
        /// notebook order (device first, then assembly order within device).
        /// </summary>
        internal static void SortByDeviceAssembly(object panel)
        {
            // filteredItems: List<IReadOnlyStorageSlot> on InventoryPanel.
            var filteredItems = Traverse.Create(panel).Field("filteredItems").GetValue() as IList;
            if (filteredItems == null || filteredItems.Count < 2) return;

            var deviceDatabase = GetDeviceDatabase(panel);
            if (deviceDatabase == null) return;

            var elementOrder = GetDeviceOrder(deviceDatabase);
            if (elementOrder == null) return;

            var slotsWithOrder = new List<SlotOrder>(filteredItems.Count);
            for (var i = 0; i < filteredItems.Count; i++)
            {
                var element = GetElementInfo(filteredItems[i]);
                slotsWithOrder.Add(elementOrder.TryGetValue(element, out var order)
                    ? new SlotOrder(i, order.Device, order.Element)
                    : new SlotOrder(i, -1, -1));
            }

            // Stable by (deviceKey, elementKey), unmatched at the end.
            var sorted = slotsWithOrder
                .OrderBy(s => s.DeviceKey < 0 ? int.MaxValue : s.DeviceKey)
                .ThenBy(s => s.ElementKey < 0 ? int.MaxValue : s.ElementKey)
                .ThenBy(s => s.Index)
                .ToList();

            var reordered = new object[sorted.Count];
            for (var i = 0; i < sorted.Count; i++)
                reordered[i] = filteredItems[sorted[i].Index];

            filteredItems.Clear();
            foreach (var slot in reordered)
                filteredItems.Add(slot);
        }

        private static object GetDeviceDatabase(object panel)
        {
            // filter is InventoryLogFilter holding a DeviceInfoDatabase field.
            var filter = Traverse.Create(panel).Field("filter").GetValue();
            if (filter == null) return null;

            var db = Traverse.Create(filter).Field("deviceDatabase").GetValue();
            if (db == null || !_deviceInfoDatabaseType.IsInstanceOfType(db)) return null;
            return db;
        }

        /// <summary>
        /// For every element that belongs to a device, computes (deviceIndex,
        /// elementIndex). deviceIndex is the position of the device in master
        /// DeviceInfoDatabase.Devices; elementIndex is the position of the
        /// element inside that device's Elements (the notebook order).
        /// The FIRST device that contains an element wins attributes.
        /// </summary>
        private static Dictionary<object, (int Device, int Element)> GetDeviceOrder(object deviceDatabase)
        {
            var devices = Traverse.Create(deviceDatabase).Property("Devices").GetValue() as IEnumerable;
            if (devices == null) return null;

            var result = new Dictionary<object, (int, int)>();
            var deviceIndex = 0;
            foreach (var device in devices)
            {
                if (device != null)
                {
                    if (Traverse.Create(device).Property("Elements").GetValue() is IEnumerable elements)
                    {
                        var elementIndex = 0;
                        foreach (var element in elements)
                        {
                            if (element != null && !result.ContainsKey(element))
                                result[element] = (deviceIndex, elementIndex);
                            elementIndex++;
                        }
                    }
                }
                deviceIndex++;
            }
            return result;
        }

        private static object GetElementInfo(object storageSlot)
        {
            if (storageSlot == null) return null;
            var item = Traverse.Create(storageSlot).Property("Item").GetValue();
            if (item == null || !_storageItemElementType.IsInstanceOfType(item)) return null;
            return Traverse.Create(item).Property("Info").GetValue();
        }

        private struct SlotOrder
        {
            public readonly int Index;
            public readonly int DeviceKey;
            public readonly int ElementKey;

            public SlotOrder(int index, int deviceKey, int elementKey)
            {
                Index = index;
                DeviceKey = deviceKey;
                ElementKey = elementKey;
            }
        }
    }

    [HarmonyPatch]
    public static class PartsBoxRemovePatch
    {
        private static Type _inventoryPanelType;

        static MethodBase TargetMethod()
        {
            _inventoryPanelType = typeof(Restory.Gameplay.Inventory.Wallet)
                .Assembly.GetType("Restory.UI.Presenters.Inventory.InventoryPanel");
            var sortMethod = AccessTools.Method(_inventoryPanelType, "UpdateItems");
            if (sortMethod == null)
            {
                return null;
            }
            Core.Log.Msg("[PartsBoxSort] InventoryPanel.UpdateItems patched (postfix).");
            return sortMethod;
        }

        [HarmonyPostfix]
        public static void UpdateItems_Postfix(object __instance)
        {
            if (!PartsBoxSortPatches._resolved || PartsBoxSortPatches._resolveFailed) return;
            if (Core.SortBoxParts == null || !Core.SortBoxParts.Value) return;

            try
            {
                PartsBoxSortPatches.SortByDeviceAssembly(__instance);
            }
            catch (Exception ex)
            {
                Core.Log.Warning($"[PartsBox] {ex}");
            }
        }
    }
}