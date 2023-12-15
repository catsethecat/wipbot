using wipbot.Interfaces;
using wipbot.UI;
using Zenject;

namespace wipbot.Installers
{
    internal class WBMenuInstaller : Installer
    {
        private readonly WBConfig Config;
        private readonly IChatIntegration ChatIntegration;
        public WBMenuInstaller(WBConfig config, IChatIntegration chatIntegration)
        {
            Config = config;
            ChatIntegration = chatIntegration;
        }
        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<WipbotButtonController>().AsSingle();
            Container.BindInterfacesAndSelfTo<WipbotManager>().AsSingle();
        }
    }
}
