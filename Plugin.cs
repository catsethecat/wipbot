using IPA;
using IPA.Config.Stores;
using IPALogger = IPA.Logging.Logger;
using BeatSaberMarkupLanguage.MenuButtons;
using System;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Linq;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]
namespace wipbot
{
    public delegate void void_str(String str);

    class Config
    {
        public static Config Instance { get; set; }
        public virtual int ZipMaxEntries { get; set; } = 100;
        public virtual int ZipMaxUncompressedSizeMB { get; set; } = 100;
        public virtual string FileExtensionWhitelist { get; set; } = "png jpg jpeg dat json ogg egg";
        public virtual string RequestCodeDownloadUrl { get; set; } = "http://catse.net/wips/%s.zip";
        public virtual string CommandRequestWip { get; set; } = "!wip";
        public virtual string CommandDownloadBsr { get; set; } = "!bsrdl";
        public virtual string MessageInvalidRequest { get; set; } = "! Invalid request. To request a WIP, go to http://catse.net/wip or upload the .zip anywhere on discord or on google drive, copy the download link and use the command !wip (link)";
        public virtual string MessageWipRequested { get; set; } = "! WIP requested";
        public virtual string MessageDownloadStarted { get; set; } = "! WIP download started";
        public virtual string MessageDownloadSuccess { get; set; } = "! WIP download successful";
        public virtual string MessageNoRequest { get; set; } = "! WIP not requested";
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
    }

    public class BeatSaberPlusStuff
    {
        public BeatSaberPlusStuff()
        {
            BeatSaberPlus.SDK.Chat.Service.Acquire();
            BeatSaberPlus.SDK.Chat.Services.ChatServiceMultiplexer mux;
            mux = BeatSaberPlus.SDK.Chat.Service.Multiplexer;
            mux.OnTextMessageReceived += OnMessageReceived;
            Plugin.Instance.SetSendFunc(delegate (String msg)
            {
                mux.SendTextMessage(((BeatSaberPlus.SDK.Chat.Services.ChatServiceMultiplexer)mux).Channels[0].Item2, msg);
            });
            void OnMessageReceived(BeatSaberPlus.SDK.Chat.Interfaces.IChatService service, BeatSaberPlus.SDK.Chat.Interfaces.IChatMessage msg)
            {
                Plugin.Instance.OnMessageReceived(msg.Message, msg.Sender.IsBroadcaster);
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
                Plugin.Instance.OnMessageReceived(msg.Message, msg.Sender.IsBroadcaster);
            }
        }
    }

    [Plugin(RuntimeOptions.SingleStartInit)]
    public class Plugin
    {
        internal static Plugin Instance { get; private set; }
        internal static IPALogger Log { get; private set; }

        void_str SendMessage;
        string wipUrl;
        Thread downloadThread;

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
            MenuButton button = new MenuButton("Download WIP", "Starts or cancels a WIP download", DownloadButtonPressed, true);
            MenuButtons.instance.RegisterButton(button);
            try
            {
                new BeatSaberPlusStuff();
                Plugin.Log.Info("Using BeatSaberPlus for chat");
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
        }

        [OnExit]
        public void OnApplicationQuit()
        {

        }

        public void SetSendFunc(void_str func)
        {
            SendMessage = func;
        }

        public void OnMessageReceived(String msg, bool isBroadcaster)
        {
            string[] msgSplit = msg.Split(' ');
            if (msgSplit[0].ToLower().StartsWith(Config.Instance.CommandRequestWip))
            {
                if (msgSplit.Length > 1 && msgSplit[1] == "***")
                {
                    SendMessage(Config.Instance.ErrorMessageLinkBlocked);
                    return;
                }
                if (msgSplit.Length != 2 || (msgSplit[1].IndexOf(".") != -1 && msgSplit[1].StartsWith("https://cdn.discordapp.com/") == false && msgSplit[1].StartsWith("https://drive.google.com/file/d/") == false))
                {
                    SendMessage(Config.Instance.MessageInvalidRequest);
                }
                else
                {
                    wipUrl = msgSplit[1];
                    if (msgSplit[1].IndexOf(".") == -1)
                        wipUrl = Config.Instance.RequestCodeDownloadUrl.Replace("%s", msgSplit[1]);
                    SendMessage(Config.Instance.MessageWipRequested);
                }
            }
            if (msgSplit[0].ToLower().StartsWith(Config.Instance.CommandDownloadBsr) && isBroadcaster)
            {
                WebClient webClient = new WebClient();
                string songInfo = webClient.DownloadString("https://beatsaver.com/api/maps/id/" + msgSplit[1]);
                string fileNameNoExt = msgSplit[1] + " (" + GetStringsBetweenStrings(songInfo, "\"songName\": \"", "\"")[0] + " - " + GetStringsBetweenStrings(songInfo, "\"levelAuthorName\": \"", "\"")[0] + ")";
                string downloadUrl = GetStringsBetweenStrings(songInfo, "\"downloadURL\": \"", "\"")[0];
                DownloadAndExtractZip(downloadUrl, "UserData\\wipbot", "Beat Saber_Data\\CustomLevels\\", fileNameNoExt);
            }
        }

        private static string[] GetStringsBetweenStrings(string str, string start, string end)
        {
            List<string> list = new List<string>();
            for (int found = str.IndexOf(start); found > 0; found = str.IndexOf(start, found + 1))
            {
                int startIndex = found + start.Length;
                int endIndex = str.IndexOf(end, startIndex);
                endIndex = endIndex != -1 ? endIndex : str.IndexOf("\n", startIndex);
                list.Add(str.Substring(startIndex, endIndex - startIndex));
            }
            return list.ToArray();
        }

        private void DownloadAndExtractZip(string url, string downloadFolder, string extractFolder, string outputFolderName)
        {
            try
            {
                SendMessage(Config.Instance.MessageDownloadStarted);
                WebClient webClient = new WebClient();
                webClient.Headers.Add(HttpRequestHeader.UserAgent, "Beat Saber wipbot v1.9.0");
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
                            SendMessage(Config.Instance.ErrorMessageTooManyEntries.Replace("%i",""+Config.Instance.ZipMaxEntries));
                        }
                        else
                        {
                            long totalUncompressedLength = 0;
                            foreach (ZipArchiveEntry entry in archive.Entries)
                            {
                                
                                if (entry.Name.Split(System.IO.Path.GetInvalidFileNameChars()).Length != 1)
                                {
                                    SendMessage(Config.Instance.ErrorMessageInvalidFilename);
                                    break;
                                }
                                if ((totalUncompressedLength = totalUncompressedLength + entry.Length) > (Config.Instance.ZipMaxUncompressedSizeMB * 1000000))
                                {
                                    SendMessage(Config.Instance.ErrorMessageMaxLength.Replace("%i", "" + Config.Instance.ZipMaxUncompressedSizeMB));
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
                    SendMessage(Config.Instance.ErrorMessageExtractionFailed);
                }
                File.Delete(downloadFolder + "\\wipbot_tmp.zip");
                if(badFileTypesFound > 0)
                    SendMessage(Config.Instance.ErrorMessageBadExtension.Replace("%i", "" + badFileTypesFound));
                if (!File.Exists(extractFolder + outputFolderName + "\\info.dat"))
                {
                    SendMessage(Config.Instance.ErrorMessageMissingInfoDat);
                    Directory.Delete(extractFolder + outputFolderName, true);
                    return;
                }
                SongCore.Loader.Instance.RefreshSongs(false);
                SendMessage(Config.Instance.MessageDownloadSuccess);
                wipUrl = null;
            }
            catch (Exception e)
            {
                if (e is WebException)
                    SendMessage(Config.Instance.ErrorMessageDownloadFailed);
                else if (e is ThreadAbortException)
                    SendMessage(Config.Instance.MessageDownloadCancelled);
                else
                    SendMessage(Config.Instance.ErrorMessageOther.Replace("%s", e.Message));
            }
        }

        private void DownloadButtonPressed()
        {
            if(wipUrl == null)
            {
                SendMessage(Config.Instance.MessageNoRequest);
                return;
            }
            if(downloadThread != null && downloadThread.IsAlive)
            {
                downloadThread.Abort();
                downloadThread = null;
                return;
            }
            string[] urlSplit = wipUrl.Split('/');
            string folderName = "wipbot_" + Convert.ToString(DateTimeOffset.Now.ToUnixTimeSeconds(), 16);
            if (urlSplit[2] == "drive.google.com")
                downloadThread = new Thread(() => DownloadAndExtractZip("https://drive.google.com/uc?id=" + urlSplit[5] + "&export=download&confirm=t", "UserData\\wipbot", "Beat Saber_Data\\CustomWIPLevels\\", folderName));
            else
                downloadThread = new Thread(() => DownloadAndExtractZip(wipUrl, "UserData\\wipbot", "Beat Saber_Data\\CustomWIPLevels\\", folderName));
            downloadThread.Start();
        }
    }
}
