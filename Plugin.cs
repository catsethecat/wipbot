using IPA;
using IPALogger = IPA.Logging.Logger;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.MenuButtons;

using TMPro;
using UnityEngine;
using UnityEngine.UI;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Net;
using System.IO;

using System.Threading;

using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.IO;
using System.IO.Compression;



namespace wipbot
{
    [Plugin(RuntimeOptions.SingleStartInit)]
    public class Plugin
    {
        internal static Plugin Instance { get; private set; }
        internal static IPALogger Log { get; private set; }

        BeatSaberPlus.SDK.Chat.Services.ChatServiceMultiplexer mux;
        string wipUrl;


        [Init]
        public Plugin(IPALogger logger)
        {
            Instance = this;
            Log = logger;
        }

        [OnStart]
        public void OnApplicationStart()
        {
            MenuButton testBtn = new MenuButton("Download WIP", "", DownloadButtonPressed, true);
            MenuButtons.instance.RegisterButton(testBtn);

            BeatSaberPlus.SDK.Chat.Service.Acquire();
            mux = BeatSaberPlus.SDK.Chat.Service.Multiplexer;
            mux.OnTextMessageReceived += OnMessageReceived;
        }

        [OnExit]
        public void OnApplicationQuit()
        {

        }

        void OnMessageReceived(BeatSaberPlus.SDK.Chat.Interfaces.IChatService service, BeatSaberPlus.SDK.Chat.Interfaces.IChatMessage msg)
        {
            string[] msgSplit = msg.Message.Split(' ');
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
            if (msgSplit[0].ToLower().StartsWith("!bsrdl") && msg.Sender.IsBroadcaster)
            {
                WebClient webClient = new WebClient();
                string songInfo = webClient.DownloadString("https://beatsaver.com/api/maps/id/" + msgSplit[1]);
                string fileNameNoExt = msgSplit[1] + " (" + GetStringsBetweenStrings(songInfo, "\"songName\": \"", "\"")[0] + " - " + GetStringsBetweenStrings(songInfo, "\"levelAuthorName\": \"", "\"")[0] + ")";
                string downloadUrl = GetStringsBetweenStrings(songInfo, "\"downloadURL\": \"", "\"")[0];
                DownloadAndExtractZip(downloadUrl, "Beat Saber_Data\\CustomLevels\\", fileNameNoExt);
            }
        }

        private void SendMessage(string msg)
        {
            mux.SendTextMessage(mux.Channels[0].Item2, "! " + msg);
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

        

        private void DownloadAndExtractZip(string url, string path, string folderName)
        {
            WebClient webClient = new WebClient();
            try
            {
                webClient.DownloadFile(url, path + folderName + ".zip");
                if (Directory.Exists(path + folderName))
                    Directory.Delete(path + folderName, true);
                ZipFile.ExtractToDirectory(path + folderName + ".zip", path + folderName);
                File.Delete(path + folderName + ".zip");
                if (!File.Exists(path + folderName + "\\info.dat"))
                {
                    string[] dirs = Directory.GetDirectories(path + folderName);
                    if (dirs.Length == 1 && File.Exists(dirs[0] + "\\info.dat"))
                    {
                        Directory.Move(dirs[0], path + folderName + "_tmp");
                        Directory.Delete(path + folderName, true);
                        Directory.Move(path + folderName + "_tmp", path + folderName);
                    }
                    else
                    {
                        SendMessage("WIP missing info.dat");
                        Directory.Delete(path + folderName, true);
                        return;
                    }
                }
                SongCore.Loader.Instance.RefreshSongs(false);
                SendMessage("WIP download successful");
            }
            catch (Exception e)
            {
                if (e is WebException)
                    SendMessage("WIP download failed");
                else
                    SendMessage("WIP extraction failed");
            }
        }

        private void DownloadButtonPressed()
        {
            if(wipUrl == null)
            {
                SendMessage("WIP not requested");
                return;
            }
            string[] urlSplit = wipUrl.Split('/');
            if (urlSplit[2] == "drive.google.com")
                DownloadAndExtractZip("https://drive.google.com/uc?id=" + urlSplit[5] + "&export=download", "Beat Saber_Data\\CustomWIPLevels\\", urlSplit[5]);
            else
                DownloadAndExtractZip(wipUrl, "Beat Saber_Data\\CustomWIPLevels\\", urlSplit[urlSplit.Length - 1].Split('.')[0]);
        }

       
    }
}
