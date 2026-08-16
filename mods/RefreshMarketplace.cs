using System;
using System.Collections.Generic;
using HarmonyLib;
using Restory.Gameplay.Shops;
using Restory.Gameplay.Shops.Devices;
using Restory.UI.Presenters.Shops.Devices;
using UnityEngine;

namespace RestoryQOL.Mods
{
    /// <summary>
    /// CTRL+R clears the device shop (flea market) and forces the supplier to
    /// generate a fresh batch of lots, instead of waiting for the next
    /// in-game morning. Lots already in the shopping cart are left in the cart
    /// but dropped from the listing, exactly like lots that time out. If the
    /// shop panel is open it is re-shown so the new lots appear immediately.
    /// </summary>
    public static class RefreshMarketplace
    {
        public static void Run()
        {
            if (!Core.RefreshMarketplace.Value) return;
            if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl)) return;
            if (!Input.GetKeyDown(KeyCode.R)) return;

            try
            {
                Refresh();
            }
            catch (Exception ex)
            {
                Core.Log.Warning($"[RefreshMarketplace] {ex}");
            }
        }

        private static void Refresh()
        {
            var shopsService = UnityEngine.Object.FindAnyObjectByType<ShopsService>();
            var supplier = UnityEngine.Object.FindAnyObjectByType<DeviceShopSupplier>();
            if (shopsService == null || supplier == null)
            {
                Core.Log.Msg("[RefreshMarketplace] Shop not available yet.");
                return;
            }

            // Remove every current device-shop lot. Removing also drops it from
            // the visible listing; anything already in the cart stays in the cart
            // (same as a lot timing out).
            var lots = new List<ILot>(shopsService.Lots);
            foreach (var lot in lots)
                shopsService.RemoveDeviceFromShop(lot);

            // SupplyNextBatchIfNecessary only generates once per day, gated on
            // lastSupplyDayNumber. Roll it back so the call regenerates now.
            var supplierTraverse = Traverse.Create(supplier);
            var lastSupplyDay = supplierTraverse.Field("lastSupplyDayNumber");
            lastSupplyDay.SetValue(lastSupplyDay.GetValue<int>() - 1);
            supplierTraverse.Method("SupplyNextBatchIfNecessary").GetValue();

            // New lots land in the "for today" pending lists with staggered
            // posting times later in the day; ProcessTimeChanged would only
            // publish them once game time passes. Move them straight into the
            // live listing so the refresh is visible immediately.
            MovePendingToShop(shopsService,
                supplierTraverse.Field("lotsForToday").GetValue<List<IDeviceShopLot>>());
            MovePendingToShop(shopsService,
                supplierTraverse.Field("elementsBoxesForToday").GetValue<List<IElementsBoxLot>>());

            // If the shop panel is open, rebuild it so the new lots show.
            var panel = UnityEngine.Object.FindAnyObjectByType<GUI_DeviceShopPanel>();
            if (panel != null && panel.isActiveAndEnabled)
                panel.Show();

            Core.Log.Msg(
                $"[RefreshMarketplace] Refreshed: removed {lots.Count}, now {shopsService.Lots.Count} lot(s).");
        }

        private static void MovePendingToShop<T>(ShopsService shopsService, List<T> pending) where T : class, ILot
        {
            foreach (var lot in pending)
            {
                if (lot != null)
                    shopsService.SupplyDeviceLot(lot);
            }
            pending.Clear();
        }
    }
}
