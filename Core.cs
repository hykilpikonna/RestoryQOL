using System.Reflection;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(RestoryQOL.Core), "RestoryQOL", "1.0.0", "Azalea", null)]
[assembly: MelonGame("Mandragora", "Restory")]

namespace RestoryQOL
{
    public class Core : MelonMod
    {
        public static Core Instance { get; private set; }

        #region Config

        public static MelonPreferences_Category Category;
        public static MelonPreferences_Entry<bool> MenuEnabled;
        public static MelonPreferences_Entry<bool> NoDeduction;
        public static MelonPreferences_Entry<bool> InfiniteMoney;
        public static MelonPreferences_Entry<bool> SkipLogos;
        public static MelonPreferences_Entry<bool> FixSaveHang;
        public static MelonPreferences_Entry<bool> AutoPartsPage;
        public static MelonPreferences_Entry<bool> SortPartsByNotebook;
        public static MelonPreferences_Entry<bool> HighlightMissingParts;
        public static MelonPreferences_Entry<bool> AutoScrew;
        public static MelonPreferences_Entry<bool> ResetTimerOnFail;
        public static MelonPreferences_Entry<bool> InstantUltrasonic;
        public static MelonPreferences_Entry<bool> AutoTool;
        public static MelonPreferences_Entry<bool> AdBlock;
        public static MelonPreferences_Entry<bool> SnapToSocket;
        public static MelonPreferences_Entry<bool> QuickDispose;

        #endregion

        #region UI State

        private Rect _windowRect = new Rect(20f, 80f, 340f, 420f);
        private bool _visible = true;
        private bool _dragging;
        private int _windowId = 266622;

        #endregion

        public override void OnInitializeMelon()
        {
            Instance = this;

            Category             = MelonPreferences.CreateCategory("RestoryQOL");
            MenuEnabled          = Category.CreateEntry("MenuEnabled",          true,  "Start with menu open");
            NoDeduction        = Category.CreateEntry("InfinityMoney",        true,  "Bypass wallet deduction on purchase");
            InfiniteMoney          = Category.CreateEntry("FakeMoneyUI",          true,  "Fake unlimited money for UI checks");
            SkipLogos            = Category.CreateEntry("SkipLogos",            true,  "Skip startup logos");
            FixSaveHang          = Category.CreateEntry("FixSaveHang",          true,  "Fix save hang (skip texture conversion wait)");
            AutoPartsPage        = Category.CreateEntry("AutoPartsPage",        true,  "Auto-open parts page for placed device");
            SortPartsByNotebook  = Category.CreateEntry("SortPartsByNotebook",  true,  "Sort the parts shop to match the notebook's part order");
            HighlightMissingParts = Category.CreateEntry("HighlightMissingParts", false, "Highlight parts missing from the current device");
            AutoScrew            = Category.CreateEntry("AutoScrew",            true,  "Hold Z/X to screw in/unscrew all visible screws");
            ResetTimerOnFail     = Category.CreateEntry("ResetTimerOnFail",     true,  "Reset competition timer when a competition attempt fails");
            InstantUltrasonic    = Category.CreateEntry("InstantUltrasonic",    true,  "Ultrasonic cleaner finishes instantly");
            AutoTool             = Category.CreateEntry("AutoTool",             true,  "Auto-select cleaning tool or soldering iron based on element");
            AdBlock              = Category.CreateEntry("AdBlock",              true,  "Hide cross-promo ad banners on browser shop pages");
            SnapToSocket         = Category.CreateEntry("SnapToSocket",         true,  "Hold ALT to snap a dropped part into its socket");
            QuickDispose         = Category.CreateEntry("QuickDispose",         true,  "Hold SHIFT on drop: broken->shredder, dirty->cleaner, good->parts box");
            Category.SaveToFile(false);

            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Mods.InfinityMoney));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Mods.FakeMoneyUI));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Mods.SkipLogos));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Mods.SaveFixPatches));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Mods.PartsPagePatches));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Mods.PartsShopPatches));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Mods.ResetTimerOnFail));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Mods.InstantUltrasonic));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Mods.AutoTool));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Mods.AdBlock));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Mods.SnapToSocket));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Mods.QuickDispose));

            _visible = MenuEnabled.Value;
            LoggerInstance.Msg("Initialized. Press F8 to toggle the menu.");
        }

        public override void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.F8))
            {
                _visible = !_visible;
                LoggerInstance.Msg("[Menu] F8 toggled -> visible: " + _visible);
            }
            Mods.AutoScrew.Run();
        }

        public override void OnGUI()
        {
            // NOTE: F8 is NOT handled here on purpose. OnGUI fires multiple
            // times per frame and would toggle the menu several times for a
            // single keypress. It is handled exclusively in OnUpdate(), where
            // Input.GetKeyDown fires exactly once per frame.
            if (!_visible) return;

            _windowRect = GUI.Window(_windowId, _windowRect, DrawWindow, "RestoryQOL");
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginVertical();

            GUILayout.Label("--- Cheats ---", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            NoDeduction.Value = GUILayout.Toggle(NoDeduction.Value, " Bypass wallet deduction");
            InfiniteMoney.Value   = GUILayout.Toggle(InfiniteMoney.Value,   " Infinite money");

            GUILayout.Space(8f);
            GUILayout.Label("--- QoL ---", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            SkipLogos.Value    = GUILayout.Toggle(SkipLogos.Value,    " Skip startup logos");
            MenuEnabled.Value  = GUILayout.Toggle(MenuEnabled.Value,  " Start with menu open");
            AutoPartsPage.Value       = GUILayout.Toggle(AutoPartsPage.Value,       " Auto-open parts for placed device");
            SortPartsByNotebook.Value = GUILayout.Toggle(SortPartsByNotebook.Value, " Sort parts shop to notebook order");
            HighlightMissingParts.Value = GUILayout.Toggle(HighlightMissingParts.Value, " Highlight missing parts");
            ResetTimerOnFail.Value = GUILayout.Toggle(ResetTimerOnFail.Value, " Reset competition timer on fail");
            InstantUltrasonic.Value = GUILayout.Toggle(InstantUltrasonic.Value, " Ultrasonic cleaner finishes instantly");
            AutoTool.Value = GUILayout.Toggle(AutoTool.Value, " Auto-select tool (dirty: last cleaner, scorched: iron)");
            AdBlock.Value = GUILayout.Toggle(AdBlock.Value, " Block ad banners on shop pages");
            SnapToSocket.Value = GUILayout.Toggle(SnapToSocket.Value, " Hold ALT: snap dropped part into socket");
            QuickDispose.Value = GUILayout.Toggle(QuickDispose.Value, " Hold SHIFT on drop: auto-route part by condition");

            GUILayout.Space(8f);
            GUILayout.Label("--- Bug Fix ---", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            FixSaveHang.Value  = GUILayout.Toggle(FixSaveHang.Value,  " Fix save hang");

            GUILayout.Space(8f);
            GUILayout.Label("--- Screws ---", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            AutoScrew.Value = GUILayout.Toggle(AutoScrew.Value, " Hold Z: screw in all / X: unscrew all");

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

            GUI.DragWindow(_windowRect);
        }
    }
}
