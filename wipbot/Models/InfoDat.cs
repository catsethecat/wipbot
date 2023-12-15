using Newtonsoft.Json;

namespace wipbot.Models
{
    internal struct InfoDat
    {
        [JsonProperty("_songName")]
        public string SongName;

        [JsonProperty("_songSubName")]
        public string SongSubName;

        [JsonProperty("_songAuthorName")]
        public string SongAuthorName;

        [JsonProperty("_levelAuthorName")]
        public string LevelAuthorName;
    }
}