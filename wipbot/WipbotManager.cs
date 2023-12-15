using HMUI;
using IPA.Utilities;
using Newtonsoft.Json;
using SiraUtil.Logging;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using wipbot.Interfaces;
using wipbot.Models;
using wipbot.UI;
using wipbot.Utils;
using Zenject;

namespace wipbot
{
    internal class WipbotManager : IInitializable
    {
        [Inject] private readonly LevelCollectionNavigationController navigationController;
        [Inject] private readonly SelectLevelCategoryViewController categoryController;
        [Inject] private readonly LevelFilteringNavigationController filteringController;
        [Inject] private readonly LevelSearchViewController searchController;
        [Inject] private readonly WipbotButtonController WipbotButtonController;
        [Inject] private readonly WBConfig Config;
        [Inject] private readonly SiraLog Logger;
        [Inject] private readonly IChatIntegration ChatIntegration;

        private readonly ExtendedQueue<QueueItem> WipQueue = new ExtendedQueue<QueueItem>();
        private readonly BlockingCollection<QueueItem> DownloadQueue = new BlockingCollection<QueueItem>();
        private Thread DownloadThread;

        private bool IsDownloading = false;

        public void Initialize()
        {
            WipbotButtonController.OnWipButtonPressed += OnWipButtonPressed;
            ChatIntegration.OnMessageReceived += OnMessageReceived;
            RenameOldSongFolders();
        }

        public void OnMessageReceived(ChatMessage ChatMessage)
        {
            string[] msgSplit = ChatMessage.Content.Split(' ');
            if (msgSplit[0].ToLower().StartsWith(Config.CommandRequestWip))
            {
                int requestLimit = 
                    ChatMessage.IsBroadcaster ? 99 :
                    ChatMessage.IsModerator ? Config.QueueLimits.Moderator :
                    ChatMessage.IsVip ? Config.QueueLimits.Vip :
                    ChatMessage.IsSubscriber ? Config.QueueLimits.Subscriber :
                    Config.QueueLimits.User;
                int requestCount = WipQueue.Count(item => item.UserName == ChatMessage.UserName);
                if (msgSplit.Length > 1 && msgSplit[1].ToLower() == Config.KeywordUndoRequest)
                {
                    WipQueue.Remove(WipQueue.Where(x => x.UserName == ChatMessage.UserName).FirstOrDefault());
                }
                else if (requestLimit == 0)
                {
                    ChatIntegration.SendChatMessage(Config.ErrorMessageNoPermission);
                }
                else if (requestCount >= requestLimit)
                {
                    ChatIntegration.SendChatMessage(Config.ErrorMessageUserMaxRequests);
                }
                else if (WipQueue.Count >= Config.QueueSize)
                {
                    ChatIntegration.SendChatMessage(Config.ErrorMessageQueueFull);
                }
                else if (msgSplit.Length > 1 && msgSplit[1] == "***")
                {
                    ChatIntegration.SendChatMessage(Config.ErrorMessageLinkBlocked);
                }
                else if (msgSplit.Length != 2 || (msgSplit[1].All(Config.RequestCodeCharacterWhitelist.Contains) == false && Config.UrlWhitelist.Any(msgSplit[1].Contains) == false))
                {
                    ChatIntegration.SendChatMessage(Config.MessageInvalidRequest);
                }
                else
                {
                    string wipUrl = msgSplit[1];

                    if (msgSplit[1].IndexOf(".") == -1)
                        wipUrl = Config.RequestCodeDownloadUrl.Replace("%s", msgSplit[1]);
                    for (int i = 0; i < Config.UrlFindReplace.Count; i += 2)
                        wipUrl = wipUrl.Replace(Config.UrlFindReplace[i], Config.UrlFindReplace[i + 1]);
                    WipQueue.Enqueue(new QueueItem() { UserName = ChatMessage.UserName, DownloadUrl = wipUrl });
                    ChatIntegration.SendChatMessage(Config.MessageWipRequested);
                    WipbotButtonController.UpdateButtonState(WipQueue.ToArray());
                }
            }
        }

        private void OnWipButtonPressed()
        {
            if (IsDownloading)
            {
                IsDownloading = false;
                DownloadThread.Abort();
                return;
            }

            DownloadQueue.Add(WipQueue.Dequeue());
            if (DownloadThread == null || !DownloadThread.IsAlive)
            {
                DownloadThread = new Thread(DownloadThreadLoop)
                {
                    IsBackground = true
                };
                DownloadThread.Start();

                // Make sure that we dont leave threads behind after a game exit
                // Make sure that we are not subscribed to the event multiple times
                Application.quitting -= Application_quitting;
                Application.quitting += Application_quitting;
            }
        }

        private void Application_quitting() => DownloadThread.Abort();

        private async void DownloadThreadLoop()
        {
            try
            {
                foreach (var item in DownloadQueue.GetConsumingEnumerable())
                {
                    IsDownloading = true;
                    await DownloadAndExtractZipAsync(item.DownloadUrl, "UserData\\wipbot", Path.Combine(UnityGame.InstallPath, Config.WipFolder));
                    IsDownloading = false;
                }
            }
            catch (ThreadAbortException)
            {
                return;
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
            IsDownloading = false;
        }

        private async Task DownloadAndExtractZipAsync(string url, string downloadFolder, string extractFolder)
        {
            string tempFolderName = "wipbot_" + Convert.ToString(DateTimeOffset.Now.ToUnixTimeSeconds(), 16);
            string wipFolderPath = "";
            try
            {
                WipbotButtonController.WipButtonText = "skip (0%)";
                ChatIntegration.SendChatMessage(Config.MessageDownloadStarted);
                WebClient webClient = new WebClient();
                webClient.DownloadProgressChanged += (s, e) =>
                {
                    WipbotButtonController.WipButtonText = "skip (" + e.ProgressPercentage + "%)";
                };
                webClient.Headers.Add(HttpRequestHeader.UserAgent, "Beat Saber wipbot v1.14.0");

                if (!Directory.Exists(downloadFolder)) Directory.CreateDirectory(downloadFolder);
                await webClient.DownloadFileTaskAsync(new Uri(url), downloadFolder + "\\wipbot_tmp.zip");

                if (Directory.Exists(Path.Combine(extractFolder, tempFolderName))) Directory.Delete(Path.Combine(extractFolder, tempFolderName), true);
                Directory.CreateDirectory(Path.Combine(extractFolder, tempFolderName));

                try
                {
                    using (ZipArchive archive = ZipFile.OpenRead(downloadFolder + "\\wipbot_tmp.zip"))
                    {
                        if (archive.Entries.Count > Config.ZipMaxEntries)
                        {
                            ChatIntegration.SendChatMessage(Config.ErrorMessageTooManyEntries.Replace("%i", "" + Config.ZipMaxEntries));
                            return;
                        }

                        // We can do this here because we dont need to extract the zip to check this
                        if (!archive.Entries.Any(entry => Path.GetFileName(entry.FullName) == "Info.dat" ))
                        {
                            ChatIntegration.SendChatMessage(Config.ErrorMessageMissingInfoDat);
                            return;
                        }

                        // If an entry is a folder, dont extract it, could be a zip bomb
                        if (archive.Entries.Any(entry => entry.FullName.EndsWith("/")))
                        {
                            ChatIntegration.SendChatMessage(Config.ErrorMessageZipContainsSubfolders);
                            return;
                        }

                        if (archive.Entries.Sum(entry => entry.Length) > (Config.ZipMaxUncompressedSizeMB * 1000000))
                        {
                            ChatIntegration.SendChatMessage(Config.ErrorMessageMaxLength.Replace("%i", "" + Config.ZipMaxUncompressedSizeMB));
                            return;
                        }

                        if (archive.Entries.All(entry => Config.FileExtensionWhitelist.Contains(Path.GetExtension(entry.FullName).Remove(0, 1))))
                        {
                            archive.ExtractToDirectory(Path.Combine(extractFolder, tempFolderName));
                        }
                        else
                        {
                            int badFileTypesFound = 0;

                            foreach (ZipArchiveEntry entry in archive.Entries)
                            {
                                if (Config.FileExtensionWhitelist.Contains(Path.GetExtension(entry.FullName).Remove(0, 1))) entry.ExtractToFile(Path.Combine(extractFolder, tempFolderName, entry.FullName));
                                else badFileTypesFound++;
                            }

                            if (badFileTypesFound > 0)
                                ChatIntegration.SendChatMessage(Config.ErrorMessageBadExtension.Replace("%i", "" + badFileTypesFound));
                        }
                    }

                    // Rename the folder to the song name and date
                    var songDat = JsonConvert.DeserializeObject<InfoDat>(File.ReadAllText(Path.Combine(extractFolder, tempFolderName, "Info.dat")));
                    wipFolderPath = Path.Combine(extractFolder, GetFolderName(songDat, DateTimeOffset.Now));
                    Logger.Info($"Renaming {Path.Combine(extractFolder, tempFolderName)} to {wipFolderPath}");
                    Directory.Move(Path.Combine(extractFolder, tempFolderName), wipFolderPath);
                }
                catch (ThreadAbortException)
                {
                    throw; // Rethrow so we can catch it in the thread loop
                }
                catch (Exception e)
                {
                    ChatIntegration.SendChatMessage(Config.ErrorMessageExtractionFailed);
                    Logger.Error(e);
                    return;
                }
                finally
                {
                    File.Delete(downloadFolder + "\\wipbot_tmp.zip");
                    if (Directory.Exists(Path.Combine(extractFolder, tempFolderName))) Directory.Delete(Path.Combine(extractFolder, tempFolderName), true);
                }

                SongCore.Loader.Instance.RefreshSongs(false);
                SongCore.Loader.OnLevelPacksRefreshed += OnLevelsRefreshed;

                void OnLevelsRefreshed()
                {
                    SongCore.Data.SongData customLevelData = SongCore.Loader.Instance.LoadCustomLevelSongData(wipFolderPath);
                    CustomPreviewBeatmapLevel customPreviewLevel = SongCore.Loader.LoadSong(customLevelData.SaveData, wipFolderPath, out string hash);
                    SongCore.Loader.OnLevelPacksRefreshed -= OnLevelsRefreshed;
                    SegmentedControl control = categoryController.transform.Find("HorizontalIconSegmentedControl").GetComponent<IconSegmentedControl>();
                    control.SelectCellWithNumber(3);
                    categoryController.LevelFilterCategoryIconSegmentedControlDidSelectCell(control, 3);
                    searchController.ResetCurrentFilterParams();
                    filteringController.UpdateSecondChildControllerContent(SelectLevelCategoryViewController.LevelCategory.All);
                    navigationController.SelectLevel(customPreviewLevel);
                }

                ChatIntegration.SendChatMessage(Config.MessageDownloadSuccess);
            }
            catch (ThreadAbortException)
            {
                throw; // Rethrow so we can catch it in the thread loop
            }
            catch (Exception e)
            {
                if (e is WebException)
                    ChatIntegration.SendChatMessage(Config.ErrorMessageDownloadFailed);
                else if (e is ThreadAbortException)
                    ChatIntegration.SendChatMessage(Config.MessageDownloadCancelled);
                else
                    ChatIntegration.SendChatMessage(Config.ErrorMessageOther.Replace("%s", e.Message));
                Logger.Error(e);
            }
            finally
            {
                WipbotButtonController.UpdateButtonState(WipQueue.ToArray());
            }
        }

        private static string GetFolderName(InfoDat songDat, DateTimeOffset dateTime)
        {
            var sb = new StringBuilder();
            sb.Append("wipbot_(");
            if (!string.IsNullOrEmpty(songDat.SongName)) sb.Append(songDat.SongName).Append(" - ");
            if (!string.IsNullOrEmpty(songDat.SongSubName)) sb.Append(songDat.SongSubName).Append(" - ");
            if (!string.IsNullOrEmpty(songDat.SongAuthorName)) sb.Append(songDat.SongAuthorName).Append(" - ");
            if (!string.IsNullOrEmpty(songDat.LevelAuthorName)) sb.Append(songDat.LevelAuthorName).Append(" - ");
            sb.Remove(sb.Length - 3, 3).Append(")_").Append($"({dateTime:MMM dd, yyyy - HH:mm:ss})");
            var newFolderName = sb.ToString();
            return SanitizePath(newFolderName);
        }

        private static string SanitizePath(string path)
        {
            string invalidChars = new string(System.IO.Path.GetInvalidFileNameChars()) + new string(System.IO.Path.GetInvalidPathChars());
            foreach (char c in invalidChars)
            {
                path = path.Replace(c.ToString(), "_");
            }
            return path;
        }

        internal void RenameOldSongFolders()
        {
            try
            {
                int renamedFolders = 0;
                var wipDirectory = Path.Combine(Environment.CurrentDirectory, "Beat Saber_Data", "CustomWIPLevels");
                Directory.GetDirectories(wipDirectory).ToList().ForEach(wipFolder =>
                {
                    if (Path.GetFileName(wipFolder).StartsWith("wipbot_") && !Path.GetFileName(wipFolder).StartsWith("wipbot_("))
                    {
                        var infoDat = JsonConvert.DeserializeObject<InfoDat>(File.ReadAllText(Path.Combine(wipFolder, "info.dat")));
                        var newFolderPath = Path.Combine(wipDirectory, GetFolderName(infoDat, DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(Path.GetFileName(wipFolder).Remove(0, 7), 16))));
                        Directory.Move(wipFolder, newFolderPath);
                        renamedFolders++;
                        Logger.Info($"Renamed {Path.GetFileName(wipFolder)} to {Path.GetFileName(newFolderPath)}");
                    }
                });

                if (renamedFolders > 0)
                {
                    Logger.Info($"Renamed {renamedFolders} old wip song folders to new schema");

                    // Trigger a full song reload if legacy named maps are renamed
                    SongCore.Loader.OnLevelPacksRefreshed += RefreshSongs;
                    void RefreshSongs()
                    {
                        SongCore.Loader.OnLevelPacksRefreshed -= RefreshSongs;
                        SongCore.Loader.Instance.RefreshSongs(true);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }
    }
}
