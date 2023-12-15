using CatCore;
using CatCore.Services.Twitch.Interfaces;
using CatCore.Models.Twitch.IRC;
using System;
using wipbot.Interfaces;
using wipbot.Models;

namespace wipbot.Interop
{
    internal class CatCoreInterop : IChatIntegration
    {
        public event Action<ChatMessage> OnMessageReceived;
        private readonly ITwitchService Service;
        public CatCoreInterop()
        {
            CatCoreInstance inst = CatCoreInstance.Create();
            Service = inst.RunTwitchServices();
            Service.OnTextMessageReceived += Serv_OnTextMessageReceived;
        }

        private void Serv_OnTextMessageReceived(ITwitchService service, TwitchMessage msg)
        {
            OnMessageReceived?.Invoke(new ChatMessage(
                msg.Sender.UserName, 
                msg.Message, 
                msg.Sender.IsBroadcaster,
                msg.Sender.IsModerator, 
                ((TwitchUser)msg.Sender).IsVip, 
                ((TwitchUser)msg.Sender).IsSubscriber));
        }

        public void SendChatMessage(string message) => Service.DefaultChannel.SendMessage(message);
    }
}
