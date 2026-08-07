using MelonLoader;

[assembly: MelonInfo(typeof(RestoryQOL.Core), "RestoryQOL", "1.0.0", "Azalea", null)]
[assembly: MelonGame("Mandragora", "Restory")]

namespace RestoryQOL
{
    public class Core : MelonMod
    {
        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Initialized.");
        }
    }
}