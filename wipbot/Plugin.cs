using IPA;
using IPA.Config.Stores;
using SiraUtil.Zenject;
using System.Linq;
using System.Runtime.CompilerServices;
using wipbot.Installers;
using wipbot.Interfaces;
using wipbot.Interop;
using IPALogger = IPA.Logging.Logger;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]
namespace wipbot
{
    [Plugin(RuntimeOptions.SingleStartInit)]
    [NoEnableDisable]
    public class Plugin
    {
        [Init]
        public Plugin(IPALogger logger, IPA.Config.Config config, Zenjector zenject)
        {
            zenject.UseLogger(logger);
            zenject.Install(Location.App, Container =>
            {
                Container.BindInstance(config.Generated<WBConfig>()).AsSingle();
                Container.BindInstance(InitializeChat(logger)).AsSingle();
            });
            zenject.Install<WBMenuInstaller>(Location.Menu);
        }

        private static IChatIntegration InitializeChat(IPALogger logger)
        {
            if (IPA.Loader.PluginManager.EnabledPlugins.Any(x => x.Id == "ChatPlexSDK_BS"))
            {
                logger.Info("Using ChatPlexSDK for chat");
                return new ChatPlexSDKInterop();
            }
            else if (IPA.Loader.PluginManager.EnabledPlugins.Any(x => x.Id == "CatCore"))
            {
                logger.Info("Using CatCore for chat");
                return new CatCoreInterop();
            }
            else
            {
                logger.Error("Wipbot failed to initialize chat. ChatPlexSDK (BeatSaberPlus) or CatCore have to be installed for wipbot to work");
                return null;
            }
        }
    }
}
