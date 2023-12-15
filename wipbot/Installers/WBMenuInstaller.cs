using wipbot.Interfaces;
using wipbot.UI;
using Zenject;

namespace wipbot.Installers
{
    internal class WBMenuInstaller : Installer
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<WipbotButtonController>().AsSingle();
            Container.BindInterfacesAndSelfTo<WipbotManager>().AsSingle();
        }
    }
}
