using HarmonyLib;
using Restory.UI.Presenters.WorkshopRatingsApplication;
using Restory.UI.Views.Shops.Devices;
using Restory.UI.Views.Shops.Elements;

namespace RestoryQOL.Mods
{
    /// <summary>
    /// Removes the built-in cross-promotion "ads" from the in-game browser:
    /// the devices-shop banner on the parts shop page, the license banner on
    /// the devices shop page, and the decor shop banner in the ratings app.
    /// The underlying shops stay fully reachable through their normal tabs.
    /// </summary>
    public static class AdBlock
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(GUI_ElementsShopProductsPanelView), "ToggleBanner")]
        public static void ToggleBanner_Prefix(ref bool isActive)
        {
            if (Core.AdBlock.Value)
                isActive = false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GUI_DeviceShopPanelView), "ToggleLicenseBanner")]
        public static void ToggleLicenseBanner_Prefix(ref bool isActive)
        {
            if (Core.AdBlock.Value)
                isActive = false;
        }

        // The ratings app banner has no toggle method; its visibility comes
        // from the scene, so just deactivate it once it enables.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GUI_WorkshopRatingsBanner), "OnEnable")]
        public static void RatingsBannerOnEnable_Postfix(GUI_WorkshopRatingsBanner __instance)
        {
            if (Core.AdBlock.Value)
                __instance.gameObject.SetActive(false);
        }
    }
}
