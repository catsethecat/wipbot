using IPA;
using IPA.Config.Stores;
using SiraUtil.Zenject;
using System.Linq;
using System.Runtime.CompilerServices;
using wipbot.Installers;
using wipbot.Interfaces;
using wipbot.Interop;
using wipbot.UI;
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
                var chat = InitializeChat(logger);
                
                if (chat != null) Container.BindInstance(chat).AsSingle();
            });
            zenject.Install(Location.Menu, Container =>
            {
                Container.BindInterfacesAndSelfTo<WipbotButtonController>().AsSingle().When((x) => Container.HasBinding<IChatIntegration>());
                Container.BindInterfacesAndSelfTo<WipbotManager>().AsSingle().When((x) => Container.HasBinding<IChatIntegration>());
            });
        }

        private IChatIntegration InitializeChat(IPALogger logger)
        {
            if (IPA.Loader.PluginManager.EnabledPlugins.Any(x => x.Id == "ChatPlexSDK_BS"))
            {
                logger.Info("Using ChatPlexSDK for chat");
                return InitChatPlexSDKInterop();
            }
            else if (IPA.Loader.PluginManager.EnabledPlugins.Any(x => x.Id == "CatCore"))
            {
                logger.Info("Using CatCore for chat");
                return InitCatCoreInterop();
            }
            else
            {
                logger.Error("Wipbot failed to initialize chat. ChatPlexSDK (BeatSaberPlus) or CatCore have to be installed for wipbot to work");
                return null;
            }
        }

        private static IChatIntegration InitCatCoreInterop()
        {
            return new CatCoreInterop();
        }

        private static IChatIntegration InitChatPlexSDKInterop()
        {
            return new ChatPlexSDKInterop();
        }
    }
}
