using System;
using wipbot.Models;

namespace wipbot.Interfaces
{
    internal interface IChatIntegration
    {
        void SendChatMessage(string message);
        event Action<ChatMessage> OnMessageReceived;
    }
}
