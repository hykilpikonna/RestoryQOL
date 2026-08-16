using HarmonyLib;
using UnityEngine;

namespace RestoryQOL
{
    /// <summary>
    /// Loader-agnostic mod core. All patches and per-frame logic live here and
    /// talk only to the game; the mod loader (MelonLoader or BepInEx) is wired
    /// in by the bootstrap types in Bootstrap.cs through ILog/IConfigStore.
    /// </summary>
    public static class Core
    {
        #region Config

        public static BoolEntry MenuEnabled;
        public static BoolEntry NoDeduction;
        public static BoolEntry InfiniteMoney;
        public static BoolEntry SkipLogos;
        public static BoolEntry FixSaveHang;
        public static BoolEntry AutoPartsPage;
        public static BoolEntry SortPartsByNotebook;
        public static BoolEntry SortBoxParts;
        public static BoolEntry HighlightMissingParts;
        public static BoolEntry AutoScrew;
        public static BoolEntry ResetTimerOnFail;
        public static BoolEntry InstantUltrasonic;
        public static BoolEntry AutoTool;
        public static BoolEntry AdBlock;
        public static BoolEntry SnapToSocket;
        public static BoolEntry QuickDispose;
        public static BoolEntry RefreshMarketplace;

        #endregion

        #region Logging

        public static ILog Log;

        #endregion

        #region UI State

        private static Rect _windowRect = new Rect(20f, 80f, 340f, 420f);
        private static bool _visible = true;
        private static bool _dragging;
        private static int _windowId = 266622;

        #endregion

        public static void Initialize(ILog log, IConfigStore config)
        {
            Log = log;

            MenuEnabled          = config.CreateBool("MenuEnabled",          true,  "Start with menu open");
            NoDeduction          = config.CreateBool("InfinityMoney",        false, "Bypass wallet deduction on purchase");
            InfiniteMoney        = config.CreateBool("FakeMoneyUI",          false, "Fake unlimited money for UI checks");
            SkipLogos            = config.CreateBool("SkipLogos",            true,  "Skip startup logos");
            FixSaveHang          = config.CreateBool("FixSaveHang",          true,  "Fix save hang (skip texture conversion wait)");
            AutoPartsPage        = config.CreateBool("AutoPartsPage",        true,  "Auto-open parts page for placed device");
            SortPartsByNotebook  = config.CreateBool("SortPartsByNotebook",  true,  "Sort the parts shop to match the notebook's part order");
            SortBoxParts         = config.CreateBool("SortBoxParts",         true,  "Sort the parts box by device, then assembly order");
            HighlightMissingParts = config.CreateBool("HighlightMissingParts", false, "Highlight parts missing from the current device");
            AutoScrew            = config.CreateBool("AutoScrew",            true,  "Hold Z/X to screw in/unscrew all visible screws");
            ResetTimerOnFail     = config.CreateBool("ResetTimerOnFail",     false, "Reset competition timer when a competition attempt fails");
            InstantUltrasonic    = config.CreateBool("InstantUltrasonic",    false, "Ultrasonic cleaner finishes instantly");
            AutoTool             = config.CreateBool("AutoTool",             true,  "Auto-select cleaning tool or soldering iron based on element");
            AdBlock              = config.CreateBool("AdBlock",              true,  "Hide cross-promo ad banners on browser shop pages");
            SnapToSocket         = config.CreateBool("SnapToSocket",         true,  "Hold ALT to snap a dropped part into its socket");
            QuickDispose         = config.CreateBool("QuickDispose",         true,  "Hold SHIFT on drop: broken->shredder, dirty->cleaner, good->parts box");
            RefreshMarketplace   = config.CreateBool("RefreshMarketplace",   true,  "Press CTRL+R to refresh the device shop marketplace");
            config.Save();

            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Mods.InfinityMoney));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Mods.FakeMoneyUI));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Mods.SkipLogos));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Mods.SaveFixPatches));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Mods.PartsPagePatches));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Mods.PartsShopPatches));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Mods.PartsBoxSortPatches));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Mods.PartsBoxRemovePatch));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Mods.ResetTimerOnFail));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Mods.InstantUltrasonic));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Mods.AutoTool));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Mods.AdBlock));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Mods.SnapToSocket));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Mods.QuickDispose));

            _visible = MenuEnabled.Value;
            Log.Msg("Initialized. Press F8 to toggle the menu.");
        }

        public static void RunFrame()
        {
            if (Input.GetKeyDown(KeyCode.F8))
            {
                _visible = !_visible;
                Log.Msg("[Menu] F8 toggled -> visible: " + _visible);
            }
            Mods.AutoScrew.Run();
            Mods.RefreshMarketplace.Run();
        }

        public static void DrawGUI()
        {
            // NOTE: F8 is NOT handled here on purpose. OnGUI fires multiple
            // times per frame and would toggle the menu several times for a
            // single keypress. It is handled exclusively in RunFrame(), where
            // Input.GetKeyDown fires exactly once per frame.
            if (!_visible) return;

            _windowRect = GUI.Window(_windowId, _windowRect, DrawWindow, "RestoryQOL");
        }

        private static void DrawWindow(int id)
        {
            GUILayout.BeginVertical();

            GUILayout.Label("--- Cheats ---", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            NoDeduction.Value = GUILayout.Toggle(NoDeduction.Value, " Bypass wallet deduction");
            InfiniteMoney.Value   = GUILayout.Toggle(InfiniteMoney.Value,   " Infinite money");
            ResetTimerOnFail.Value = GUILayout.Toggle(ResetTimerOnFail.Value, " Reset competition timer on fail");
            InstantUltrasonic.Value = GUILayout.Toggle(InstantUltrasonic.Value, " Ultrasonic cleaner finishes instantly");

            GUILayout.Space(8f);
            GUILayout.Label("--- QoL ---", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            SkipLogos.Value    = GUILayout.Toggle(SkipLogos.Value,    " Skip startup logos");
            MenuEnabled.Value  = GUILayout.Toggle(MenuEnabled.Value,  " Start with menu open");
            HighlightMissingParts.Value = GUILayout.Toggle(HighlightMissingParts.Value, " Highlight missing parts");
            AutoPartsPage.Value       = GUILayout.Toggle(AutoPartsPage.Value,       " Auto-open parts for placed device");
            SortPartsByNotebook.Value = GUILayout.Toggle(SortPartsByNotebook.Value, " Sort parts shop by notebook order");
            SortBoxParts.Value         = GUILayout.Toggle(SortBoxParts.Value,        " Sort parts box by device/assembly order");
            AutoTool.Value = GUILayout.Toggle(AutoTool.Value, " Auto-select tool (dirty: last cleaner, scorched: iron)");
            AdBlock.Value = GUILayout.Toggle(AdBlock.Value, " Block ad banners on shop pages");
            
            GUILayout.Space(8f);
            GUILayout.Label("--- Hot Keys ---", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            SnapToSocket.Value = GUILayout.Toggle(SnapToSocket.Value, " Hold ALT: snap dropped part into socket");
            QuickDispose.Value = GUILayout.Toggle(QuickDispose.Value, " Hold SHIFT on drop: auto-route part by condition");
            RefreshMarketplace.Value = GUILayout.Toggle(RefreshMarketplace.Value, " CTRL+R: refresh marketplace");
            AutoScrew.Value = GUILayout.Toggle(AutoScrew.Value, " Hold Z: screw in all / X: unscrew all");

            GUILayout.Space(8f);
            GUILayout.Label("--- Bug Fix ---", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            FixSaveHang.Value  = GUILayout.Toggle(FixSaveHang.Value,  " Fix save hang");

            GUILayout.EndVertical();

            if (Event.current.type == EventType.MouseDrag && _dragging)
            {
                _windowRect.position += Event.current.delta;
                _windowRect.position = new Vector2(
                    Mathf.Clamp(_windowRect.position.x, -_windowRect.width + 60f, Screen.width - 40f),
                    Mathf.Clamp(_windowRect.position.y, 0f, Screen.height - 40f));
                Event.current.Use();
            }
            if (Event.current.type == EventType.MouseDown && _windowRect.Contains(Event.current.mousePosition))
                _dragging = true;
            if (Event.current.type == EventType.MouseUp)
                _dragging = false;

            if (Event.current.type == EventType.Repaint)
            {
                Rect last = GUILayoutUtility.GetLastRect();
                if (last.yMax > 0f)
                    _windowRect.height = last.yMax + 34f;
            }

            GUI.DragWindow(_windowRect);
        }
    }
}