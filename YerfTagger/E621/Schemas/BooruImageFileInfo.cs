using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YerfTagger.E621.Schemas
{
    public class BooruImageFileInfo
    {
        [JsonProperty("width")]
        public int Width { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }

        [JsonProperty("ext")]
        public string Extension { get; set; }

        [JsonProperty("size")]
        public int FileSizeBytes { get; set; }

        [JsonProperty("md5")]
        public string MD5 { get; set; }

        [JsonProperty("has")]
        public bool Has { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }
    }
}
