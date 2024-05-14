using IPA;
using IPA.Config.Stores;
using IPA.Config.Stores.Attributes;
using IPA.Config.Stores.Converters;
using IPALogger = IPA.Logging.Logger;
using BeatSaberMarkupLanguage;
using System;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using HMUI;
using BeatSaberMarkupLanguage.Attributes;
using System.Reflection;
using IPA.Loader;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]
namespace wipbot
{
    public delegate void void_str(String str);

    public struct QueueLimits
    {
        public int User;
        public int Subscriber;
        public int Vip;
        public int Moderator;
    }

    public struct QueueItem
    {
        public string UserName;
        public string DownloadUrl;
    }

    class Config
    {
        public static Config Instance { get; set; }
        public virtual int ZipMaxEntries { get; set; } = 100;
        public virtual int ZipMaxUncompressedSizeMB { get; set; } = 100;
        [UseConverter(typeof(ListConverter<string>))]
        [NonNullable]
        public virtual List<string> FileExtensionWhitelist { get; set; } = new List<string>() { "png", "jpg", "jpeg", "dat", "json", "ogg", "egg", "wav", "" };
        [UseConverter(typeof(ListConverter<string>))]
        [NonNullable]
        public virtual List<string> RequestCodePrefixDownloadUrlPairs { get; set; } = new List<string>() { "0", "http://catse.net/wips/%s.zip" };
        public virtual string RequestCodeCharacterWhitelist { get; set; } = "0123456789abcdefABCDEF";
        [UseConverter(typeof(ListConverter<string>))]
        [NonNullable]
        public virtual List<string> UrlWhitelist { get; set; } = new List<string>() { "https://cdn.discordapp.com/", "https://drive.google.com/file/d/" };
        [UseConverter(typeof(ListConverter<string>))]
        [NonNullable]
        public virtual List<string> UrlFindReplacePairs { get; set; } = new List<string>() { "https://drive.google.com/file/d/", "https://drive.google.com/uc?id=", "/view?usp=sharing", "&export=download&confirm=t", "/view?usp=drive_link", "&export=download&confirm=t" };
        public virtual string WipFolder { get; set; } = "Beat Saber_Data\\CustomWIPLevels\\";
        public virtual string CommandRequestWip { get; set; } = "!wip";
        public virtual string KeywordUndoRequest { get; set; } = "oops";
        public virtual QueueLimits QueueLimits { get; set; } = new QueueLimits { User = 2, Subscriber = 2, Vip = 2, Moderator = 2 };
        public virtual int QueueSize { get; set; } = 9;
        public virtual int ButtonPositionX { get; set; } = 139;
        public virtual int ButtonPositionY { get; set; } = -2;
        public virtual string MessageHelp { get; set; } = "! To request a WIP, go to http://catse.net/wip or upload the .zip anywhere on discord or on google drive, copy the download link and use the command !wip (link)";
        public virtual string MessageInvalidRequest2 { get; set; } = "! Invalid request";
        public virtual string MessageWipRequested { get; set; } = "! WIP requested";
        public virtual string MessageUndoRequest { get; set; } = "! Removed your latest request from wip queue";
        public virtual string MessageDownloadSuccess2 { get; set; } = "! Downloaded WIP from @%s";
        public virtual string MessageDownloadCancelled { get; set; } = "! WIP download cancelled";
        public virtual string ErrorMessageTooManyEntries { get; set; } = "! Error: Zip contains more than %i entries";
        public virtual string ErrorMessageInvalidFilename { get; set; } = "! Error: Zip contains file with invalid name";
        public virtual string ErrorMessageMaxLength { get; set; } = "! Error: Zip uncompressed length >%i MB";
        public virtual string ErrorMessageExtractionFailed { get; set; } = "! Error: Zip extraction failed";
        public virtual string ErrorMessageBadExtension2 { get; set; } = "! Skipped %i files during extraction due to forbidden file extensions %s";
        public virtual string ErrorMessageMissingInfoDat { get; set; } = "! Error: WIP missing info.dat";
        public virtual string ErrorMessageDownloadFailed2 { get; set; } = "! Error: WIP download failed (%s)";
        public virtual string ErrorMessageOther { get; set; } = "! Error: %s";
        public virtual string ErrorMessageLinkBlocked { get; set; } = "! Error: Your link was blocked by the channel's chat moderation settings";
        public virtual string ErrorMessageQueueFull { get; set; } = "! Error: The wip request queue is full";
        public virtual string ErrorMessageUserMaxRequests { get; set; } = "! Error: You already have the maximum number of wip requests in queue";
        public virtual string ErrorMessageNoPermission { get; set; } = "! Error: You don't have permission to use the wip command";
    }

    public class ChatPlexInitializer
    {
        public ChatPlexInitializer()
        {
            CP_SDK.Chat.Service.Acquire();
            CP_SDK.Chat.Services.ChatServiceMultiplexer mux;
            mux = CP_SDK.Chat.Service.Multiplexer;
            mux.OnTextMessageReceived += OnMessageReceived;
            Plugin.Instance.SetSendFunc(delegate (String msg)
            {
                mux.SendTextMessage(((CP_SDK.Chat.Services.ChatServiceMultiplexer)mux).Channels[0].Item2, msg);
            });
            void OnMessageReceived(CP_SDK.Chat.Interfaces.IChatService service, CP_SDK.Chat.Interfaces.IChatMessage msg)
            {
                Plugin.Instance.OnMessageReceived(msg.Sender.UserName, msg.Message, msg.Sender.IsBroadcaster, msg.Sender.IsModerator, msg.Sender.IsVip, msg.Sender.IsSubscriber);
            }
        }
    }

    public class CatCoreInitializer
    {
        public CatCoreInitializer()
        {
            CatCore.CatCoreInstance inst = CatCore.CatCoreInstance.Create();
            CatCore.Services.Twitch.Interfaces.ITwitchService serv = inst.RunTwitchServices();
            serv.OnTextMessageReceived += OnMessageReceived;
            Plugin.Instance.SetSendFunc(delegate (String msg)
            {
                serv.DefaultChannel.SendMessage(msg);
            });
            void OnMessageReceived(CatCore.Services.Twitch.Interfaces.ITwitchService service, CatCore.Models.Twitch.IRC.TwitchMessage msg)
            {
                Plugin.Instance.OnMessageReceived(msg.Sender.UserName, msg.Message, msg.Sender.IsBroadcaster, msg.Sender.IsModerator,
                    ((CatCore.Models.Twitch.IRC.TwitchUser)msg.Sender).IsVip,
                    ((CatCore.Models.Twitch.IRC.TwitchUser)msg.Sender).IsSubscriber);
            }
        }
    }

    [Plugin(RuntimeOptions.SingleStartInit)]
    public class Plugin
    {
        internal static Plugin Instance { get; private set; }
        internal static IPALogger Log { get; private set; }

        static void_str SendChatMessage;
        static List<QueueItem> wipQueue = new List<QueueItem>();
        static string latestDownloadedSongPath;
        static Thread downloadThread;
        static LevelCollectionNavigationController navigationController;
        static SelectLevelCategoryViewController categoryController;
        static LevelSearchViewController searchController;

        [Init]
        public Plugin(IPALogger logger, IPA.Config.Config config)
        {
            Instance = this;
            Log = logger;
            Config.Instance = config.Generated<Config>();
        }

        [OnStart]
        public void OnApplicationStart()
        {
            if (PluginManager.GetPluginFromId("ChatPlexSDK_BS") != null)
            {
                new ChatPlexInitializer();
                Plugin.Log.Info("Using ChatPlexSDK for chat");
            }
            else if (PluginManager.GetPluginFromId("CatCore") != null)
            {
                new CatCoreInitializer();
                Plugin.Log.Info("Using CatCore for chat");
            }
            else
            {
                Plugin.Log.Info("Failed to initialize chat");
            }
            Harmony harmony = new Harmony("Catse.BeatSaber.wipbot");
            harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
            SongCore.Loader.OnLevelPacksRefreshed += OnLevelsRefreshed;
        }

        [OnExit]
        public void OnApplicationQuit()
        {

        }

        public void SetSendFunc(void_str func)
        {
            SendChatMessage = func;
        }

        private static void UpdateButtonState()
        {
            WipbotButtonController.instance.blueButtonText = "wip(" + wipQueue.Count + ")";
            WipbotButtonController.instance.grayButtonActive = wipQueue.Count == 0;
            WipbotButtonController.instance.blueButtonActive = wipQueue.Count > 0;
            string hint = "";
            for (int i = 0; i < wipQueue.Count; i++)
                hint = hint + (i + 1) + " " + wipQueue[i].UserName + " ";
            WipbotButtonController.instance.blueButtonHint = hint;

        }

        public void OnMessageReceived(String userName, String msg, bool isBroadcaster, bool isModerator, bool isVip, bool isSubscriber)
        {
            string[] msgSplit = msg.Split(' ');
            if (msgSplit[0].ToLower().StartsWith(Config.Instance.CommandRequestWip))
            {
                int requestLimit = isBroadcaster ? 99 :
                    isModerator ? Config.Instance.QueueLimits.Moderator :
                    isVip ? Config.Instance.QueueLimits.Vip :
                    isSubscriber ? Config.Instance.QueueLimits.Subscriber :
                    Config.Instance.QueueLimits.User;
                int requestCount = 0;
                foreach (QueueItem request in wipQueue)
                    if (request.UserName == userName)
                        requestCount++;
                if (msgSplit.Length > 1 && msgSplit[1].ToLower() == Config.Instance.KeywordUndoRequest)
                {
                    for (int i = wipQueue.Count - 1; i >= 0; i--)
                    {
                        if (wipQueue[i].UserName == userName)
                        {
                            wipQueue.RemoveAt(i);
                            SendChatMessage(Config.Instance.MessageUndoRequest);
                            UpdateButtonState();
                            break;
                        }
                    }
                }
                else if (requestLimit == 0)
                {
                    SendChatMessage(Config.Instance.ErrorMessageNoPermission);
                }
                else if (requestCount == requestLimit)
                {
                    SendChatMessage(Config.Instance.ErrorMessageUserMaxRequests);
                }
                else if (wipQueue.Count == Config.Instance.QueueSize)
                {
                    SendChatMessage(Config.Instance.ErrorMessageQueueFull);
                }
                else if (msgSplit.Length > 1 && msgSplit[1] == "***")
                {
                    SendChatMessage(Config.Instance.ErrorMessageLinkBlocked);
                }
                else if (msgSplit.Length == 1)
                {
                    SendChatMessage(Config.Instance.MessageHelp);
                }
                else if (msgSplit.Length != 2 || (msgSplit[1].All(Config.Instance.RequestCodeCharacterWhitelist.Contains) == false && Config.Instance.UrlWhitelist.Any(msgSplit[1].Contains) == false))
                {
                    SendChatMessage(Config.Instance.MessageInvalidRequest2);
                }
                else
                {
                    string wipUrl = msgSplit[1];

                    if (msgSplit[1].IndexOf(".") == -1)
                    {
                        wipUrl = "";
                        for (int i = 0; i < Config.Instance.RequestCodePrefixDownloadUrlPairs.Count; i += 2)
                        {
                            if (msgSplit[1].StartsWith(Config.Instance.RequestCodePrefixDownloadUrlPairs[i]))
                            {
                                wipUrl = Config.Instance.RequestCodePrefixDownloadUrlPairs[i + 1].Replace("%s", msgSplit[1]);
                                break;
                            }
                        }
                        if (wipUrl == "")
                        {
                            SendChatMessage(Config.Instance.MessageInvalidRequest2);
                            return;
                        }
                    }
                    for (int i = 0; i < Config.Instance.UrlFindReplacePairs.Count; i += 2)
                        wipUrl = wipUrl.Replace(Config.Instance.UrlFindReplacePairs[i], Config.Instance.UrlFindReplacePairs[i + 1]);
                    wipQueue.Add(new QueueItem() { UserName = userName, DownloadUrl = wipUrl });
                    SendChatMessage(Config.Instance.MessageWipRequested);
                    UpdateButtonState();
                }
            }
        }

        private static void DownloadAndExtractWip()
        {
            string url = wipQueue[0].DownloadUrl;
            string requester = wipQueue[0].UserName;
            string downloadFolder = "UserData\\wipbot";
            string extractFolder = Config.Instance.WipFolder + "\\";
            string outputFolderName = "wipbot_" + Convert.ToString(DateTimeOffset.Now.ToUnixTimeSeconds(), 16);
            wipQueue.RemoveAt(0);
            WebClient webClient = new WebClient();
            try
            {
                WipbotButtonController.instance.blueButtonText = "skip";
                Thread.Sleep(1000);
                webClient.Headers.Add(HttpRequestHeader.UserAgent, "Beat Saber wipbot v1.16.0");
                if (!Directory.Exists(downloadFolder))
                    Directory.CreateDirectory(downloadFolder);
                webClient.DownloadProgressChanged += (sender, args) => { WipbotButtonController.instance.blueButtonText = args.ProgressPercentage + "%"; };
                Exception ex = null;
                webClient.DownloadFileCompleted += (sender, args) => { ex = args.Error; };
                webClient.DownloadFileAsync(new Uri(url), downloadFolder + "\\wipbot_tmp.zip");
                while (webClient.IsBusy)
                    Thread.Sleep(10);
                if (ex != null)
                    throw ex;
                if (Directory.Exists(extractFolder + outputFolderName))
                    Directory.Delete(extractFolder + outputFolderName, true);
                Directory.CreateDirectory(extractFolder + outputFolderName);
                int badFileTypesFound = 0;
                string badFileTypeNames = "";
                try
                {
                    using (ZipArchive archive = ZipFile.OpenRead(downloadFolder + "\\wipbot_tmp.zip"))
                    {
                        if (archive.Entries.Count > Config.Instance.ZipMaxEntries)
                        {
                            SendChatMessage(Config.Instance.ErrorMessageTooManyEntries.Replace("%i", "" + Config.Instance.ZipMaxEntries));
                        }
                        else
                        {
                            long totalUncompressedLength = 0;
                            foreach (ZipArchiveEntry entry in archive.Entries)
                            {

                                if (entry.Name.Split(System.IO.Path.GetInvalidFileNameChars()).Length != 1)
                                {
                                    SendChatMessage(Config.Instance.ErrorMessageInvalidFilename);
                                    break;
                                }
                                if ((totalUncompressedLength = totalUncompressedLength + entry.Length) > (Config.Instance.ZipMaxUncompressedSizeMB * 1000000))
                                {
                                    SendChatMessage(Config.Instance.ErrorMessageMaxLength.Replace("%i", "" + Config.Instance.ZipMaxUncompressedSizeMB));
                                    break;
                                }
                                if (entry.Length > 0)
                                {
                                    string[] entryNameSplit = entry.Name.Split('.');
                                    string extension = "";
                                    if (entryNameSplit.Length > 1)
                                        extension = entryNameSplit[entryNameSplit.Length - 1].ToLower();
                                    if (Config.Instance.FileExtensionWhitelist.Any(extension.Equals))
                                    {
                                        entry.ExtractToFile(extractFolder + outputFolderName + "\\" + entry.Name);
                                    }
                                    else
                                    {
                                        badFileTypesFound++;
                                        badFileTypeNames += "\"" + extension + "\" ";
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    SendChatMessage(Config.Instance.ErrorMessageExtractionFailed);
                }
                File.Delete(downloadFolder + "\\wipbot_tmp.zip");
                if (badFileTypesFound > 0)
                    SendChatMessage(Config.Instance.ErrorMessageBadExtension2.Replace("%i", "" + badFileTypesFound).Replace("%s", badFileTypeNames));
                if (!File.Exists(extractFolder + outputFolderName + "\\info.dat"))
                {
                    SendChatMessage(Config.Instance.ErrorMessageMissingInfoDat);
                    Directory.Delete(extractFolder + outputFolderName, true);
                }
                else
                {
                    latestDownloadedSongPath = extractFolder + outputFolderName;
                    SongCore.Loader.Instance.RefreshSongs(false);
                    SendChatMessage(Config.Instance.MessageDownloadSuccess2.Replace("%s", requester));
                }
            }
            catch (Exception e)
            {
                if (e is WebException)
                    SendChatMessage(Config.Instance.ErrorMessageDownloadFailed2.Replace("%s", e.Message));
                else if (e is ThreadAbortException)
                    SendChatMessage(Config.Instance.MessageDownloadCancelled);
                else
                    SendChatMessage(Config.Instance.ErrorMessageOther.Replace("%s", e.Message));
                webClient.CancelAsync();
            }
            UpdateButtonState();
        }

        public static void OnLevelsRefreshed()
        {
            GameObject songSelecter = new GameObject();
            songSelecter.AddComponent<SongSelecter>();
        }

        public class SongSelecter : MonoBehaviour
        {
            public void Awake()
            {
                StartCoroutine(SelectSongCoroutine());
            }
            public System.Collections.IEnumerator SelectSongCoroutine()
            {
                if (latestDownloadedSongPath != null)
                {
                    var (_,lvl) = SongCore.Loader.LoadCustomLevel(latestDownloadedSongPath).Value;
                    latestDownloadedSongPath = null;
                    SegmentedControl control = categoryController.transform.Find("HorizontalIconSegmentedControl").GetComponent<IconSegmentedControl>();
                    control.SelectCellWithNumber(3);
                    MethodInfo method = typeof(SelectLevelCategoryViewController).GetMethod("LevelFilterCategoryIconSegmentedControlDidSelectCell", BindingFlags.Instance | BindingFlags.NonPublic);
                    object[] args = { control, 3 };
                    method.Invoke(categoryController, args);
                    searchController.ResetFilter(false);
                    yield return new WaitForSeconds(0.5f);
                    foreach (BeatmapLevelPack levelPack in SongCore.Loader.CustomLevelsRepository.beatmapLevelPacks)
                    {
                        foreach (BeatmapLevel level in levelPack.beatmapLevels)
                        {
                            if (level.levelID.StartsWith("custom_level_" + lvl.levelID.Split('_')[2]))
                            {
                                navigationController.SelectLevel(level);
                                yield break;
                            }
                        }
                    }
                }
            }
        }

        public class WipbotButtonController : BeatSaberMarkupLanguage.Util.NotifiableSingleton<WipbotButtonController>
        {
            [UIComponent("gray-button")]
            private readonly RectTransform grayButtonTransform;

            [UIComponent("blue-button")]
            private readonly RectTransform blueButtonTransform;

            bool _grayButtonActive = true;
            bool _blueButtonActive = false;
            string _blueButtonText = "wip";
            string _blueButtonHint = "";

            [UIValue("gray-button-active")]
            public bool grayButtonActive
            {
                get => _grayButtonActive;
                set { _grayButtonActive = value; NotifyPropertyChanged(); }
            }

            [UIValue("blue-button-active")]
            public bool blueButtonActive
            {
                get => _blueButtonActive;
                set { _blueButtonActive = value; NotifyPropertyChanged(); }
            }

            [UIValue("blue-button-text")]
            public string blueButtonText
            {
                get => _blueButtonText;
                set { _blueButtonText = value; NotifyPropertyChanged(); }
            }

            [UIValue("blue-button-hint")]
            public string blueButtonHint
            {
                get => _blueButtonHint;
                set { _blueButtonHint = value; NotifyPropertyChanged(); }
            }

            public void init(GameObject parent)
            {
                if (grayButtonTransform != null) return;
                BSMLParser.instance.Parse(
                    "<bg xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xsi:schemaLocation='https://monkeymanboy.github.io/BSML-Docs/ https://raw.githubusercontent.com/monkeymanboy/BSML-Docs/gh-pages/BSMLSchema.xsd'>" +
                    "<button id='gray-button' active='~gray-button-active' text='wip' font-size='3' on-click='gray-button-click' anchor-pos-x='" + Config.Instance.ButtonPositionX + "' anchor-pos-y='" + (Config.Instance.ButtonPositionY + 2) + "' pref-height='6' pref-width='11' />" +
                    "<action-button id='blue-button' active='~blue-button-active' text='~blue-button-text' hover-hint='~blue-button-hint' word-wrapping='false' font-size='3' on-click='blue-button-click' anchor-pos-x='" + (Config.Instance.ButtonPositionX - 80) + "' anchor-pos-y='" + (Config.Instance.ButtonPositionY + 5) + "' pref-height='6' pref-width='11' />" +
                    "</bg>"
                    , parent, this);
            }

            [UIAction("blue-button-click")]
            void BlueButtonPressed()
            {
                if (downloadThread != null && downloadThread.IsAlive)
                {
                    downloadThread.Abort();
                    downloadThread = null;
                    UpdateButtonState();
                    return;
                }
                downloadThread = new Thread(() => DownloadAndExtractWip());
                downloadThread.Start();
            }

            [UIAction("gray-button-click")]
            void GrayButtonPressed()
            {

            }

            [UIAction("#post-parse")]
            private void PostParse()
            {

            }
        }

        [HarmonyPatch]
        class patches
        {
            [HarmonyPatch(typeof(LevelSelectionNavigationController), "DidActivate")]
            static void Postfix(LevelSelectionNavigationController __instance) { WipbotButtonController.instance.init(__instance.gameObject); }

            [HarmonyPatch(typeof(LevelCollectionNavigationController), "DidActivate")]
            static void Postfix(LevelCollectionNavigationController __instance) { navigationController = __instance; }

            [HarmonyPatch(typeof(SelectLevelCategoryViewController), "DidActivate")]
            static void Postfix(SelectLevelCategoryViewController __instance) { categoryController = __instance; }

            [HarmonyPatch(typeof(LevelSearchViewController), "DidActivate")]
            static void Postfix(LevelSearchViewController __instance) { searchController = __instance; }
        }

    }
}
