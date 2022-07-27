using IPA;
using IPALogger = IPA.Logging.Logger;
using BeatSaberMarkupLanguage.MenuButtons;
using System;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace wipbot
{
    public delegate void void_str(String str);
    
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
                mux.SendTextMessage(((BeatSaberPlus.SDK.Chat.Services.ChatServiceMultiplexer)mux).Channels[0].Item2, "! " + msg);
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
                serv.DefaultChannel.SendMessage("! " + msg);
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
        public Plugin(IPALogger logger)
        {
            Instance = this;
            Log = logger;
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
            if (msgSplit[0].ToLower().StartsWith("!wip"))
            {
                if (msgSplit.Length != 2 || (msgSplit[1].IndexOf(".") != -1 && msgSplit[1].StartsWith("https://cdn.discordapp.com/") == false && msgSplit[1].StartsWith("https://drive.google.com/file/d/") == false))
                {
                    SendMessage("Invalid request. To request a WIP, go to http://catse.net/wip or upload the .zip anywhere on discord or on google drive, copy the download link and use the command !wip (link)");
                }
                else
                {
                    wipUrl = msgSplit[1];
                    if (msgSplit[1].IndexOf(".") == -1)
                        wipUrl = "http://catse.net/wips/" + msgSplit[1] + ".zip";
                    SendMessage("WIP requested");
                }
            }
            if (msgSplit[0].ToLower().StartsWith("!bsrdl") && isBroadcaster)
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
                SendMessage("WIP download started");
                WebClient webClient = new WebClient();
                if (!Directory.Exists(downloadFolder))
                    Directory.CreateDirectory(downloadFolder);
                webClient.DownloadFile(url, downloadFolder + "\\wipbot_tmp.zip");
                if (Directory.Exists(extractFolder + outputFolderName))
                    Directory.Delete(extractFolder + outputFolderName, true);
                Directory.CreateDirectory(extractFolder + outputFolderName);
                try
                {
                    using (ZipArchive archive = ZipFile.OpenRead(downloadFolder + "\\wipbot_tmp.zip"))
                    {
                        if (archive.Entries.Count > 100)
                        {
                            SendMessage("Error: Zip contains more than 100 entries");
                        }
                        else
                        {
                            long totalUncompressedLength = 0;
                            foreach (ZipArchiveEntry entry in archive.Entries)
                            {
                                if (entry.Name.Split(System.IO.Path.GetInvalidFileNameChars()).Length != 1)
                                {
                                    SendMessage("Error: Zip contains file with invalid name");
                                    break;
                                }
                                if ((totalUncompressedLength = totalUncompressedLength + entry.Length) > 100000000)
                                {
                                    SendMessage("Error: Zip uncompressed length >100MB");
                                    break;
                                }
                                if (entry.Length > 0)
                                {
                                    entry.ExtractToFile(extractFolder + outputFolderName + "\\" + entry.Name);
                                }
                            }
                        }
                    }
                }catch(Exception e)
                {
                    SendMessage("Error: Zip extraction failed");
                }
                File.Delete(downloadFolder + "\\wipbot_tmp.zip");
                if (!File.Exists(extractFolder + outputFolderName + "\\info.dat"))
                {
                    SendMessage("Error: WIP missing info.dat");
                    Directory.Delete(extractFolder + outputFolderName, true);
                    return;
                }
                SongCore.Loader.Instance.RefreshSongs(false);
                SendMessage("WIP download successful");
                wipUrl = null;
            }
            catch (Exception e)
            {
                if (e is WebException)
                    SendMessage("Error: WIP download failed");
                else if (e is ThreadAbortException)
                    SendMessage("WIP download cancelled");
                else
                    SendMessage("Error: " + e.Message);
            }
        }

        private void DownloadButtonPressed()
        {
            if(wipUrl == null)
            {
                SendMessage("no WIP requested");
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
