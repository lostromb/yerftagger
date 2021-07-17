using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YerfTagger.E621.Schemas
{
    public class BooruTag
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("post_count")]
        public long PostCount { get; set; }

        [JsonProperty("category")]
        public BooruTagCategory Category { get; set; }

        [JsonProperty("is_locked")]
        public bool IsLocked { get; set; }
    }
}
