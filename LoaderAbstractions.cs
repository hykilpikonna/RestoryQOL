using System;

namespace RestoryQOL
{
    /// <summary>
    /// Loader-agnostic logging surface. Implemented once per mod loader so the
    /// patches can log to a MelonLoader console or a BepInEx log source without
    /// knowing which one they are running under.
    /// </summary>
    public interface ILog
    {
        void Msg(string message);
        void Warning(string message);
        void Error(string message);
    }

    /// <summary>
    /// Loader-agnostic typed config entry. Backed by a MelonPreferences_Entry
    /// or a BepInEx ConfigEntry, whichever loader is present.
    /// </summary>
    public sealed class BoolEntry
    {
        private readonly Func<bool> _get;
        private readonly Action<bool> _set;

        public bool Value
        {
            get => _get();
            set => _set(value);
        }

        public BoolEntry(Func<bool> get, Action<bool> set)
        {
            _get = get;
            _set = set;
        }
    }

    /// <summary>
    /// Loader-agnostic config store. Lets the shared Core create its settings
    /// without referencing MelonLoader or BepInEx namespaces.
    /// </summary>
    public interface IConfigStore
    {
        BoolEntry CreateBool(string key, bool defaultValue, string description);
        void Save();
    }
}