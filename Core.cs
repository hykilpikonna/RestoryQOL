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
        public static MelonPreferences_Entry<bool> InfinityMoney;
        public static MelonPreferences_Entry<bool> FakeMoneyUI;
        public static MelonPreferences_Entry<bool> SkipLogos;
        public static MelonPreferences_Entry<bool> FixSaveHang;
        public static MelonPreferences_Entry<bool> SaveFeedback;

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

            Category = MelonPreferences.CreateCategory("RestoryQOL");
            MenuEnabled = Category.CreateEntry("MenuEnabled", true, "Start with menu open");
            InfinityMoney = Category.CreateEntry("InfinityMoney", true, "Bypass wallet deduction on purchase");
            FakeMoneyUI = Category.CreateEntry("FakeMoneyUI", true, "Fake unlimited money for UI checks");
            SkipLogos = Category.CreateEntry("SkipLogos", true, "Skip startup logos");
            FixSaveHang = Category.CreateEntry("FixSaveHang", true, "Fix save hang (skip texture conversion wait)");
            SaveFeedback = Category.CreateEntry("SaveFeedback", true, "Show 'Saving...' text when saving");
            Category.SaveToFile(false);

            var harmony = new HarmonyLib.Harmony("RestoryQOL");
            Patches.Apply(harmony);
            SaveFixPatches.Apply(harmony);

            _visible = MenuEnabled.Value;
            LoggerInstance.Msg("Initialized. Press F8 to toggle the menu.");
        }

        public override void OnUpdate()
        {
            if (!Input.GetKeyDown(KeyCode.F8)) return;
            _visible = !_visible;
            LoggerInstance.Msg("[Menu] F8 toggled -> visible: " + _visible);
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

            GUILayout.Label("--- Money ---", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            InfinityMoney.Value = GUILayout.Toggle(InfinityMoney.Value, " Bypass wallet deduction");
            FakeMoneyUI.Value = GUILayout.Toggle(FakeMoneyUI.Value, " Fake unlimited money (UI)");

            GUILayout.Space(8f);
            GUILayout.Label("--- Startup ---", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            SkipLogos.Value = GUILayout.Toggle(SkipLogos.Value, " Skip startup logos");

            GUILayout.Space(8f);
            GUILayout.Label("--- Interface ---", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            MenuEnabled.Value = GUILayout.Toggle(MenuEnabled.Value, " Start with menu open");

            GUILayout.Space(8f);
            GUILayout.Label("--- Save Fix ---", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            FixSaveHang.Value = GUILayout.Toggle(FixSaveHang.Value, " Fix save hang (skip texture wait)");
            SaveFeedback.Value = GUILayout.Toggle(SaveFeedback.Value, " Show 'Saving...' feedback");

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