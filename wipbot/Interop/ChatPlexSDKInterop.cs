using CP_SDK.Chat.Services;
using System;
using wipbot.Interfaces;
using wipbot.Models;

namespace wipbot.Interop
{
    internal class ChatPlexSDKInterop : IChatIntegration
    {
        private readonly ChatServiceMultiplexer Multiplexer;

        public event Action<ChatMessage> OnMessageReceived;

        public ChatPlexSDKInterop()
        {
            CP_SDK.Chat.Service.Acquire();
            Multiplexer = CP_SDK.Chat.Service.Multiplexer;
            Multiplexer.OnTextMessageReceived += Mux_OnTextMessageReceived;
        }

        private void Mux_OnTextMessageReceived(CP_SDK.Chat.Interfaces.IChatService service, CP_SDK.Chat.Interfaces.IChatMessage msg)
        {
            OnMessageReceived?.Invoke(new ChatMessage(msg.Sender.UserName, msg.Message, msg.Sender.IsBroadcaster, msg.Sender.IsModerator, msg.Sender.IsVip, msg.Sender.IsSubscriber));
        }

        public void SendChatMessage(string message) => Multiplexer.SendTextMessage(Multiplexer.Channels[0].Item2, message);
    }
}
