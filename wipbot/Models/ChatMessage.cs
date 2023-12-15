namespace wipbot.Models
{
    internal struct ChatMessage
    {
        public string UserName;
        public string Content;
        public bool IsBroadcaster;
        public bool IsModerator;
        public bool IsVip;
        public bool IsSubscriber;

        public ChatMessage(string userName, string content, bool isBroadcaster, bool isModerator, bool isVip, bool isSubscriber)
        {
            UserName = userName;
            Content = content;
            IsBroadcaster = isBroadcaster;
            IsModerator = isModerator;
            IsVip = isVip;
            IsSubscriber = isSubscriber;
        }
    }
}
