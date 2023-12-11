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
        public virtual string FileExtensionWhitelist { get; set; } = "png jpg jpeg dat json ogg egg";
        public virtual string RequestCodeDownloadUrl { get; set; } = "http://catse.net/wips/%s.zip";
        public virtual string RequestCodeCharacterWhitelist { get; set; } = "0123456789abcdefABCDEF";
        [UseConverter(typeof(ListConverter<string>))]
        [NonNullable]
        public virtual List<string> UrlWhitelist { get; set; } = new List<string>() { "https://cdn.discordapp.com/", "https://drive.google.com/file/d/" };
        [UseConverter(typeof(ListConverter<string>))]
        [NonNullable]
        public virtual List<string> UrlFindReplace { get; set; } = new List<string>() { "https://drive.google.com/file/d/", "https://drive.google.com/uc?id=", "/view?usp=sharing", "&export=download&confirm=t", "/view?usp=drive_link", "&export=download&confirm=t" };
        public virtual string WipFolder { get; set; } = "Beat Saber_Data\\CustomWIPLevels\\";
        public virtual string CommandRequestWip { get; set; } = "!wip";
        public virtual string KeywordUndoRequest { get; set; } = "oops";
        public virtual QueueLimits QueueLimits { get; set; } = new QueueLimits { User = 2, Subscriber = 2, Vip = 2, Moderator = 2 };
        public virtual int QueueSize { get; set; } = 9;
        public virtual int ButtonPositionX { get; set; } = 139;
        public virtual int ButtonPositionY { get; set; } = -2;
        public virtual string MessageInvalidRequest { get; set; } = "! Invalid request. To request a WIP, go to http://catse.net/wip or upload the .zip anywhere on discord or on google drive, copy the download link and use the command !wip (link)";
        public virtual string MessageWipRequested { get; set; } = "! WIP requested";
        public virtual string MessageUndoRequest { get; set; } = "! Removed your latest request from wip queue";
        public virtual string MessageDownloadStarted { get; set; } = "! WIP download started";
        public virtual string MessageDownloadSuccess { get; set; } = "! WIP download successful";
        public virtual string MessageDownloadCancelled { get; set; } = "! WIP download cancelled";
        public virtual string ErrorMessageTooManyEntries { get; set; } = "! Error: Zip contains more than %i entries";
        public virtual string ErrorMessageInvalidFilename { get; set; } = "! Error: Zip contains file with invalid name";
        public virtual string ErrorMessageMaxLength { get; set; } = "! Error: Zip uncompressed length >%i MB";
        public virtual string ErrorMessageExtractionFailed { get; set; } = "! Error: Zip extraction failed";
        public virtual string ErrorMessageBadExtension { get; set; } = "! Skipped %i files during extraction due to bad file extension";
        public virtual string ErrorMessageMissingInfoDat { get; set; } = "! Error: WIP missing info.dat";
        public virtual string ErrorMessageDownloadFailed { get; set; } = "! Error: WIP download failed";
        public virtual string ErrorMessageOther { get; set; } = "! Error: %s";
        public virtual string ErrorMessageLinkBlocked { get; set; } = "! Error: Your link was blocked by the channel's chat moderation settings";
        public virtual string ErrorMessageQueueFull { get; set; } = "! Error: The wip request queue is full";
        public virtual string ErrorMessageUserMaxRequests { get; set; } = "! Error: You already have the maximum number of wip requests in queue";
        public virtual string ErrorMessageNoPermission { get; set; } = "! Error: You don't have permission to use the wip command";
    }

    public class ChatPlexSDKStuff
    {
        public ChatPlexSDKStuff()
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

    public class CatCoreStuff
    {
        public CatCoreStuff()
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
        static BeatmapLevelsModel beatmapLevelsModel;
        static LevelCollectionNavigationController navigationController;
        static SelectLevelCategoryViewController categoryController;
        static LevelFilteringNavigationController filteringController;
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
            try
            {
                new ChatPlexSDKStuff();
                Plugin.Log.Info("Using ChatPlexSDK for chat");
            }
            catch (Exception e)
            {
                try
                {
                    new CatCoreStuff();
                    Plugin.Log.Info("Using CatCore for chat");
                }
                catch (Exception e2)
                {
                    Plugin.Log.Info("Failed to initialize chat");
                }
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
            WipbotButtonController.instance.button2Text = "wip(" + wipQueue.Count + ")";
            WipbotButtonController.instance.buttonActive = wipQueue.Count == 0;
            WipbotButtonController.instance.button2Active = wipQueue.Count > 0;
            string hint = "";
            for (int i = 0; i < wipQueue.Count; i++)
                hint = hint + (i+1) + " " + wipQueue[i].UserName + " ";
            WipbotButtonController.instance.button2Hint = hint;

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
                else if (msgSplit.Length != 2 || (msgSplit[1].All(Config.Instance.RequestCodeCharacterWhitelist.Contains) == false && Config.Instance.UrlWhitelist.Any(msgSplit[1].Contains) == false))
                {
                    SendChatMessage(Config.Instance.MessageInvalidRequest);
                }
                else
                {
                    string wipUrl = msgSplit[1];

                    if (msgSplit[1].IndexOf(".") == -1)
                        wipUrl = Config.Instance.RequestCodeDownloadUrl.Replace("%s", msgSplit[1]);
                    for (int i = 0; i < Config.Instance.UrlFindReplace.Count; i += 2)
                        wipUrl = wipUrl.Replace(Config.Instance.UrlFindReplace[i], Config.Instance.UrlFindReplace[i + 1]);
                    wipQueue.Add(new QueueItem() { UserName = userName, DownloadUrl = wipUrl });
                    SendChatMessage(Config.Instance.MessageWipRequested);
                    UpdateButtonState();
                }
            }
        }

        private static void DownloadAndExtractZip(string url, string downloadFolder, string extractFolder, string outputFolderName)
        {
            try
            {
                WipbotButtonController.instance.button2Text = "skip";
                Thread.Sleep(1000);
                SendChatMessage(Config.Instance.MessageDownloadStarted);
                WebClient webClient = new WebClient();
                webClient.Headers.Add(HttpRequestHeader.UserAgent, "Beat Saber wipbot v1.14.0");
                if (!Directory.Exists(downloadFolder))
                    Directory.CreateDirectory(downloadFolder);
                webClient.DownloadFile(url, downloadFolder + "\\wipbot_tmp.zip");
                if (Directory.Exists(extractFolder + outputFolderName))
                    Directory.Delete(extractFolder + outputFolderName, true);
                Directory.CreateDirectory(extractFolder + outputFolderName);
                int badFileTypesFound = 0;
                try
                {
                    using (ZipArchive archive = ZipFile.OpenRead(downloadFolder + "\\wipbot_tmp.zip"))
                    {
                        if (archive.Entries.Count > Config.Instance.ZipMaxEntries)
                        {
                            SendChatMessage(Config.Instance.ErrorMessageTooManyEntries.Replace("%i",""+Config.Instance.ZipMaxEntries));
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
                                    string[] whitelist = Config.Instance.FileExtensionWhitelist.Split(' ');
                                    string[] entryNameSplit = entry.Name.Split('.');
                                    if (whitelist.Any(entryNameSplit[entryNameSplit.Length - 1].ToLower().Contains))
                                    {
                                        entry.ExtractToFile(extractFolder + outputFolderName + "\\" + entry.Name);
                                    }
                                    else
                                    {
                                        badFileTypesFound++;
                                    }
                                }
                            }
                        }
                    }
                }catch(Exception e)
                {
                    SendChatMessage(Config.Instance.ErrorMessageExtractionFailed);
                }
                File.Delete(downloadFolder + "\\wipbot_tmp.zip");
                if(badFileTypesFound > 0)
                    SendChatMessage(Config.Instance.ErrorMessageBadExtension.Replace("%i", "" + badFileTypesFound));
                if (!File.Exists(extractFolder + outputFolderName + "\\info.dat"))
                {
                    SendChatMessage(Config.Instance.ErrorMessageMissingInfoDat);
                    Directory.Delete(extractFolder + outputFolderName, true);
                }
                else
                {
                    latestDownloadedSongPath = extractFolder + outputFolderName;
                    SongCore.Loader.Instance.RefreshSongs(false);
                    SendChatMessage(Config.Instance.MessageDownloadSuccess);
                }
            }
            catch (Exception e)
            {
                if (e is WebException)
                    SendChatMessage(Config.Instance.ErrorMessageDownloadFailed);
                else if (e is ThreadAbortException)
                    SendChatMessage(Config.Instance.MessageDownloadCancelled);
                else
                    SendChatMessage(Config.Instance.ErrorMessageOther.Replace("%s", e.Message));
            }
            UpdateButtonState();
        }

        public static void OnLevelsRefreshed()
        {
            GameObject testObject = new GameObject();
            testObject.AddComponent<CoroutineTest>();
        }


        public class CoroutineTest : MonoBehaviour
        {
            public void Awake()
            {
                StartCoroutine(SelectSongCoroutine());
            }
            private System.Collections.IEnumerator SelectSongCoroutine()
            {
                if (latestDownloadedSongPath != null)
                {
                    SongCore.Data.SongData sd = SongCore.Loader.Instance.LoadCustomLevelSongData(latestDownloadedSongPath);
                    CustomPreviewBeatmapLevel cl = SongCore.Loader.LoadSong(sd.SaveData, latestDownloadedSongPath, out string hash);
                    latestDownloadedSongPath = null;
                    SegmentedControl control = categoryController.transform.Find("HorizontalIconSegmentedControl").GetComponent<IconSegmentedControl>();
                    control.SelectCellWithNumber(3);
                    categoryController.LevelFilterCategoryIconSegmentedControlDidSelectCell(control, 3);
                    searchController.ResetCurrentFilterParams();
                    filteringController.UpdateSecondChildControllerContent(SelectLevelCategoryViewController.LevelCategory.All);
                    yield return new WaitForSeconds(0.5f);
                    foreach (IBeatmapLevelPack levelPack in beatmapLevelsModel.allLoadedBeatmapLevelPackCollection.beatmapLevelPacks)
                    {
                        foreach (IPreviewBeatmapLevel level in levelPack.beatmapLevelCollection.beatmapLevels)
                        {
                            if (level.levelID.StartsWith("custom_level_" + hash))
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
            [UIComponent("wipbot-button")]
            private readonly RectTransform wipbotButtonTransform;

            [UIComponent("wipbot-button2")]
            private readonly RectTransform wipbotButton2Transform;

            bool tmp1 = true;
            bool tmp2 = false;
            string tmp3 = "wip";
            string tmp4 = "";

            [UIValue("button-active")]
            public bool buttonActive
            {
                get => tmp1;
                set { tmp1 = value; NotifyPropertyChanged(); }
            }

            [UIValue("button2-active")]
            public bool button2Active
            {
                get => tmp2;
                set { tmp2 = value; NotifyPropertyChanged(); }
            }

            [UIValue("button2-text")]
            public string button2Text
            {
                get => tmp3;
                set { tmp3 = value; NotifyPropertyChanged(); }
            }

            [UIValue("button2-hint")]
            public string button2Hint
            {
                get => tmp4;
                set { tmp4 = value; NotifyPropertyChanged(); }
            }

            public void init(GameObject parent)
            {
                if (wipbotButtonTransform != null) return;
                BSMLParser.instance.Parse(
                    "<bg xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xsi:schemaLocation='https://monkeymanboy.github.io/BSML-Docs/ https://raw.githubusercontent.com/monkeymanboy/BSML-Docs/gh-pages/BSMLSchema.xsd'>" +
                    "<button id='wipbot-button' active='~button-active' text='wip' font-size='3' on-click='wipbot-click' anchor-pos-x='"+Config.Instance.ButtonPositionX+"' anchor-pos-y='"+Config.Instance.ButtonPositionY+"' pref-height='6' pref-width='11' />" +
                    "<action-button id='wipbot-button2' active='~button2-active' text='~button2-text' hover-hint='~button2-hint' word-wrapping='false' font-size='3' on-click='wipbot-click2' anchor-pos-x='"+(Config.Instance.ButtonPositionX-80)+"' anchor-pos-y='"+(Config.Instance.ButtonPositionY+3)+"' pref-height='6' pref-width='11' />" +
                    "</bg>"
                    , parent, this);
            }

            [UIAction("wipbot-click2")]
            void DownloadButtonPressed()
            {
                if (downloadThread != null && downloadThread.IsAlive)
                {
                    downloadThread.Abort();
                    downloadThread = null;
                    UpdateButtonState();
                    return;
                }
                string wipUrl = wipQueue[0].DownloadUrl;
                string folderName = "wipbot_" + Convert.ToString(DateTimeOffset.Now.ToUnixTimeSeconds(), 16);
                downloadThread = new Thread(() => DownloadAndExtractZip(wipUrl, "UserData\\wipbot", Config.Instance.WipFolder, folderName));
                downloadThread.Start();
                wipQueue.RemoveAt(0);
            }

            [UIAction("wipbot-click")]
            void Asdf2()
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

            [HarmonyPatch(typeof(BeatmapLevelsModel), "Init")]
            static void Postfix(BeatmapLevelsModel __instance) { beatmapLevelsModel = __instance; }

            [HarmonyPatch(typeof(LevelCollectionNavigationController), "DidActivate")]
            static void Postfix(LevelCollectionNavigationController __instance) { navigationController = __instance; }

            [HarmonyPatch(typeof(LevelFilteringNavigationController), "DidActivate")]
            static void Postfix(LevelFilteringNavigationController __instance) { filteringController = __instance; }

            [HarmonyPatch(typeof(SelectLevelCategoryViewController), "DidActivate")]
            static void Postfix(SelectLevelCategoryViewController __instance) { categoryController = __instance; }

            [HarmonyPatch(typeof(LevelSearchViewController), "DidActivate")]
            static void Postfix(LevelSearchViewController __instance) { searchController = __instance; }
        }

    }
}
