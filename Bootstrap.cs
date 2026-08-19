using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using MelonLoader;
#if BEPINEX6
// BepInEx 6 moved BaseUnityPlugin out of the BepInEx namespace.
using BepInEx.Unity.Mono;
#endif

[assembly: MelonInfo(typeof(RestoryQOL.MelonLoaderBootstrap), "RestoryQOL", "1.0.0", "Azalea", null)]
[assembly: MelonGame("Mandragora", "Restory")]
// Universal build: this assembly also references BepInEx for its BepInEx
// entry point. Under MelonLoader that reference is intentionally unresolved,
// so mark it optional or MelonLoader warns (and future versions refuse to load).
[assembly: MelonOptionalDependencies("BepInEx", "BepInEx.Core", "BepInEx.Unity.Mono")]
// MelonLoader auto-patches every [HarmonyPatch] class during HarmonyInit, which
// runs BEFORE OnInitializeMelon. Core.Log is not set yet, so any TargetMethod
// that logs throws a NullReferenceException, and the attribute-based patches
// end up applied twice (auto-patch + Core.Initialize's CreateAndPatchAll).
// All patches are registered explicitly in Core.Initialize instead.
[assembly: MelonLoader.HarmonyDontPatchAll]

namespace RestoryQOL
{
    /// <summary>
    /// MelonLoader entry point. Installed with MelonLoader; ignored by BepInEx.
    /// </summary>
    public class MelonLoaderBootstrap : MelonMod
    {
        public override void OnInitializeMelon()
        {
            Core.Initialize(
                new MelonLog(LoggerInstance),
                new MelonConfigStore(MelonPreferences.CreateCategory("RestoryQOL")));
        }

        public override void OnUpdate() => Core.RunFrame();

        public override void OnGUI() => Core.DrawGUI();
    }

    /// <summary>
    /// BepInEx entry point. Installed with BepInEx; ignored by MelonLoader.
    /// </summary>
    [BepInPlugin("aza.restoryqol", "RestoryQOL", "1.0.1")]
    public class BepInExBootstrap : BaseUnityPlugin
    {
        private void Awake()
        {
            Core.Initialize(new BepLog(Logger), new BepConfigStore(Config));
        }

        private void Update() => Core.RunFrame();

        private void OnGUI() => Core.DrawGUI();
    }

    internal sealed class MelonLog : ILog
    {
        private readonly MelonLogger.Instance _log;

        public MelonLog(MelonLogger.Instance log) => _log = log;

        public void Msg(string message)     => _log.Msg(message);
        public void Warning(string message) => _log.Warning(message);
        public void Error(string message)   => _log.Error(message);
    }

    internal sealed class MelonConfigStore : IConfigStore
    {
        private readonly MelonPreferences_Category _category;

        public MelonConfigStore(MelonPreferences_Category category) => _category = category;

        public BoolEntry CreateBool(string key, bool defaultValue, string description)
        {
            var entry = _category.CreateEntry(key, defaultValue, description);
            return new BoolEntry(() => entry.Value, value => entry.Value = value);
        }

        public void Save() => _category.SaveToFile(false);
    }

    internal sealed class BepLog : ILog
    {
        private readonly ManualLogSource _log;

        public BepLog(ManualLogSource log) => _log = log;

        public void Msg(string message)     => _log.LogInfo(message);
        public void Warning(string message) => _log.LogWarning(message);
        public void Error(string message)   => _log.LogError(message);
    }

    internal sealed class BepConfigStore : IConfigStore
    {
        private readonly ConfigFile _file;

        public BepConfigStore(ConfigFile file) => _file = file;

        public BoolEntry CreateBool(string key, bool defaultValue, string description)
        {
            var entry = _file.Bind("RestoryQOL", key, defaultValue, description);
            return new BoolEntry(() => entry.Value, value => entry.Value = value);
        }

        public void Save()
        {
            try { _file.Save(); } catch { /* config dir not writable; keep defaults */ }
        }
    }
}
