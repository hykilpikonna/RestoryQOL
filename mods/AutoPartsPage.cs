using System;
using System.Linq;
using HarmonyLib;
using Restory.Gameplay.Inventory;
using UnityEngine;

namespace RestoryQOL.Mods
{
    /// <summary>
    /// When the browser is launched (a device placed on the work surface),
    /// automatically navigates to the Parts tab and pre-selects the device's
    /// category and model in the shop filter.
    /// </summary>
    [HarmonyPatch(typeof(Restory.UI.Presenters.GUI_WebBrowser), "LaunchProcess")]
    internal static class AutoPartsPage
    {
        [HarmonyPostfix]
        private static void Postfix(object __instance)
        {
            if (!Core.AutoPartsPage.Value) return;

            try
            {
                var log = Core.Instance.LoggerInstance;
                var gameAsm = typeof(Wallet).Assembly;

                var deviceService = UnityEngine.Object.FindObjectOfType(
                    gameAsm.GetType("Restory.Gameplay.Devices.DeviceService"));
                if (deviceService == null) return;

                var container = Traverse.Create(deviceService).Property("PlacedDeviceContainer").GetValue();
                if (container == null) return;

                var deviceInfo      = Traverse.Create(container).Property("Device").Property("Info").GetValue();
                var deviceCategory  = Traverse.Create(deviceInfo).Property("Category").GetValue();
                var targetCategoryId = Traverse.Create(deviceCategory).Property("ID").GetValue() as string;
                var targetModelKey   = Traverse.Create(deviceInfo).Property("NameLocalizationKey").GetValue() as string;

                var pageSwitcher = Traverse.Create(__instance).Property("PageSwitcher").GetValue();
                var tabs = Traverse.Create(pageSwitcher).Property("Tabs").GetValue() as System.Collections.IList;
                if (tabs == null) return;

                var elementsShopPageType = gameAsm.GetType(
                    "Restory.UI.Presenters.Shops.Elements.GUI_ElementsShopPage");
                var partsTab = (
                    from object tab in tabs
                    let browserPage = Traverse.Create(tab).Property("BrowserPage").GetValue()
                    where browserPage != null && elementsShopPageType.IsInstanceOfType(browserPage)
                    select tab
                ).FirstOrDefault();
                if (partsTab == null) return;

                pageSwitcher.GetType().GetMethod("ResolveTabClick")?.Invoke(pageSwitcher, [partsTab]);

                var tabBrowserPage   = Traverse.Create(partsTab).Property("BrowserPage").GetValue();
                var shopPanelStateType = gameAsm.GetType("Restory.UI.Presenters.Shops.ShopPanelState");
                tabBrowserPage.GetType().GetProperty("CurrentState")?.SetValue(
                    tabBrowserPage, Enum.Parse(shopPanelStateType, "ProductsSelection"));

                var productsPanel = Traverse.Create(tabBrowserPage).Property("ProductsPanel").GetValue();
                var filter        = Traverse.Create(productsPanel).Property("Filter").GetValue();

                var categories = Traverse.Create(filter).Property("Categories").GetValue()
                    as System.Collections.IEnumerable;
                if (categories == null || targetCategoryId == null) return;

                var categoryIndex = 0;
                var categoryFound = false;
                foreach (var cat in categories)
                {
                    var catObj = Traverse.Create(cat).Field("Category").GetValue();
                    if (catObj != null)
                    {
                        var catId = Traverse.Create(catObj).Property("ID").GetValue() as string;
                        if (catId == targetCategoryId) { categoryFound = true; break; }
                    }
                    categoryIndex++;
                }
                if (!categoryFound) return;

                filter.GetType().GetMethod("SelectCategory", [typeof(int)])
                    ?.Invoke(filter, [categoryIndex]);

                if (Traverse.Create(filter).Field("deviceModels").GetValue()
                    is System.Collections.IList deviceModels && targetModelKey != null)
                {
                    for (var mi = 0; mi < deviceModels.Count; mi++)
                    {
                        if (deviceModels[mi] as string != targetModelKey) continue;
                        var filterView = Traverse.Create(filter).Field("view").GetValue();
                        var dropdown   = Traverse.Create(filterView).Field("modelsDropdown").GetValue();
                        dropdown?.GetType().GetProperty("value")?.SetValue(dropdown, mi);
                        break;
                    }
                }

                log.Msg($"[AutoPartsPage] Opened parts for {targetModelKey} ({targetCategoryId}).");
            }
            catch (Exception ex)
            {
                Core.Instance.LoggerInstance.Warning($"[AutoPartsPage] {ex}");
            }
        }
    }
}
