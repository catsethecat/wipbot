using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Util;
using System;
using System.Linq;
using System.Text;
using UnityEngine;
using wipbot.Models;
using Zenject;

#pragma warning disable IDE0051 // Remove unused private members
namespace wipbot.UI
{
    public class WipbotButtonController : NotifiableSingleton<WipbotButtonController>, IInitializable
    {
        [Inject] private readonly WBConfig Config;
        [Inject] private readonly MainThreadDispatcher _mainThreadDispatcher;
        internal event Action OnWipButtonPressed;
        private bool _fakeButtonActive = true;
        private bool _wipButtonActive = true;
        private string _wipButtonText = "wip";
        private string _wipButtonHint = "";

        [UIComponent("wipbot-button")]
        private readonly RectTransform wipbotButtonTransform;

        [UIComponent("wipbot-button2")]
        private readonly RectTransform wipbotButton2Transform;

        [UIValue("FakeButtonActive")]
        public bool FakeButtonActive
        {
            get => _fakeButtonActive;
            set { _fakeButtonActive = value; _mainThreadDispatcher.DispatchOnMainThread(() => NotifyPropertyChanged()); }
        }

        [UIValue("WipButtonActive")]
        public bool WipButtonActive
        {
            get => _wipButtonActive;
            set { _wipButtonActive = value; _mainThreadDispatcher.DispatchOnMainThread(() => NotifyPropertyChanged()); }
        }

        [UIValue("WipButtonText")]
        public string WipButtonText
        {
            get => _wipButtonText;
            set { _wipButtonText = value; _mainThreadDispatcher.DispatchOnMainThread(() => NotifyPropertyChanged()); }
        }

        [UIValue("WipButtonHint")]
        public string WipButtonHint
        {
            get => _wipButtonHint;
            set { _wipButtonHint = value; _mainThreadDispatcher.DispatchOnMainThread(() => NotifyPropertyChanged()); }
        }

        public void Initialize()
        {
            if (wipbotButtonTransform != null) return;
            BSMLParser.instance.Parse(
                "<bg xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xsi:schemaLocation='https://monkeymanboy.github.io/BSML-Docs/ https://raw.githubusercontent.com/monkeymanboy/BSML-Docs/gh-pages/BSMLSchema.xsd'>" +
                "<button id='wipbot-button' active='~FakeButtonActive' text='wip' font-size='3' on-click='wipbot-click' anchor-pos-x='" + Config.ButtonPositionX + "' anchor-pos-y='" + Config.ButtonPositionY + "' pref-height='6' pref-width='11' />" +
                "<action-button id='wipbot-button2' active='~WipButtonActive' text='~WipButtonText' hover-hint='~WipButtonHint' word-wrapping='false' font-size='3' on-click='wipbot-click2' anchor-pos-x='" + (Config.ButtonPositionX - 80) + "' anchor-pos-y='" + (Config.ButtonPositionY + 3) + "' pref-height='6' pref-width='11' />" +
                "</bg>"
                , Resources.FindObjectsOfTypeAll<LevelSelectionNavigationController>().First().gameObject, this);
            WipButtonActive = false;
        }

        [UIAction("wipbot-click2")]
        void DownloadButtonPressed()
        {
            OnWipButtonPressed?.Invoke();
        }

        internal void UpdateButtonState(QueueItem[] queueState)
        {
            WipButtonText = "wip(" + queueState.Length + ")";
            FakeButtonActive = queueState.Length == 0;
            WipButtonActive = queueState.Length > 0;

            var stringBuilder = new StringBuilder();

            for (int i = 0; i < queueState.Length; i++)
            {
                stringBuilder.Append(i + 1);
                stringBuilder.Append(": ");
                stringBuilder.Append(queueState[i].UserName);
                stringBuilder.Append("; ");
            }

            WipButtonHint = stringBuilder.ToString();
        }

        [UIAction("wipbot-click")]
        void Asdf2() { }
    }
}
