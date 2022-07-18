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

        SslStream sslStream;
        string channelName;
        string oauthToken;
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
            Thread t = new Thread(new ThreadStart(ChatThread));
            t.Start();
        }

        [OnExit]
        public void OnApplicationQuit()
        {

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

        private void SendMessage(string msg)
        {
            sslStream.Write(Encoding.UTF8.GetBytes("PRIVMSG #" + channelName + " :! " + msg + "\r\n"));
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

        

        private void ChatThread()
        {
            

            try
            {
                string[] test = File.ReadAllLines(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\.beatsaberpluschatcore\\auth.ini");
                foreach (string s in test)
                {
                    string[] ss = s.Split(' ');
                    int count = ss.Length;
                    if (count >= 3 && ss[0] == "Twitch.Channels")
                        channelName = ss[2];
                    if (count >= 3 && ss[0] == "Twitch.OAuthToken")
                        oauthToken = ss[2];
                }
            }
            catch (System.IO.FileNotFoundException e)
            {
                return;
            }



            while (true)
            {
                Thread.Sleep(1000);

                TcpClient client = new TcpClient("irc.chat.twitch.tv", 6697);

                sslStream = new SslStream(client.GetStream(), false);
                try
                {
                    sslStream.AuthenticateAsClient("irc.chat.twitch.tv");
                }
                catch (AuthenticationException e)
                {
                    continue;
                }

                sslStream.Write(Encoding.UTF8.GetBytes("PASS " + oauthToken + "\r\n"));
                sslStream.Write(Encoding.UTF8.GetBytes("NICK " + channelName + "\r\n"));
                sslStream.Write(Encoding.UTF8.GetBytes("JOIN #" + channelName + "\r\n"));

                byte[] buf = new byte[65536];
                int res = sslStream.Read(buf, 0, buf.Length);

                if (res == 0 || Encoding.UTF8.GetString(buf, 0, res).IndexOf("Welcome, GLHF!") == -1)
                {
                    //Plugin.Log.Info("Login failed");
                    continue;
                }

                //Plugin.Log.Info("Login OK!");

                while ((res = sslStream.Read(buf, 0, buf.Length)) > 0)
                {
                    string receivedStr = Encoding.UTF8.GetString(buf, 0, res);
                    //Plugin.Log.Info(receivedStr);
                    if (receivedStr.IndexOf("PING") != -1)
                    {
                        buf[1] = (byte)'O';
                        sslStream.Write(buf, 0, res);
                    }
                    if (receivedStr.IndexOf("PRIVMSG") != -1)
                    {
                        int startIndex = receivedStr.IndexOf(":", 1) + 1;
                        string sender = receivedStr.Substring(1, receivedStr.IndexOf("!") - 1);
                        string msg = receivedStr.Substring(startIndex, receivedStr.Length - startIndex - 2);
                        string[] msgSplit = msg.Split(' ');
                        if (msgSplit[0] == "!wip")
                        {
                            if (msgSplit.Length != 2 || (msgSplit[1].IndexOf("https://cdn.discordapp.com/") == -1 && msgSplit[1].IndexOf("https://drive.google.com/file/d/") == -1))
                            {
                                SendMessage("WIP url invalid. To request a WIP, upload the .zip anywhere on discord or on google drive, copy the download link and use the command !wip (link)");
                            }
                            else
                            {
                                wipUrl = msgSplit[1];
                                SendMessage("WIP requested");
                            }
                        }
                        if (msgSplit[0] == "!bsrdl" && sender == channelName)
                        {
                            WebClient webClient = new WebClient();
                            string songInfo = webClient.DownloadString("https://beatsaver.com/api/maps/id/" + msgSplit[1]);
                            string fileNameNoExt = msgSplit[1] + " (" + GetStringsBetweenStrings(songInfo, "\"songName\": \"", "\"")[0] + " - " + GetStringsBetweenStrings(songInfo, "\"levelAuthorName\": \"", "\"")[0] + ")";
                            string downloadUrl = GetStringsBetweenStrings(songInfo, "\"downloadURL\": \"", "\"")[0];
                            DownloadAndExtractZip(downloadUrl, "Beat Saber_Data\\CustomLevels\\", fileNameNoExt);
                        }
                    }
                }

                //Plugin.Log.Info("Receive failed, reconnecting...\n");

            }
        }
    }
}
