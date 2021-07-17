using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YerfTagger.E621.Schemas
{
    public class BooruTagGroup
    {
        [JsonProperty("general")]
        public IList<string> General { get; set; }

        [JsonProperty("character")]
        public IList<string> Character { get; set; }

        [JsonProperty("species")]
        public IList<string> Species { get; set; }

        [JsonProperty("artist")]
        public IList<string> Artist { get; set; }

        [JsonProperty("copyright")]
        public IList<string> Copyright { get; set; }

        [JsonProperty("invalid")]
        public IList<string> Invalid { get; set; }

        [JsonProperty("lore")]
        public IList<string> Lore { get; set; }

        [JsonProperty("meta")]
        public IList<string> Meta { get; set; }
    }
}
