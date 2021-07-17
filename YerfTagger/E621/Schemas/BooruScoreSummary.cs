using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YerfTagger.E621.Schemas
{
    public class BooruScoreSummary
    {
        /// <summary>
        /// Number of upvotes, where each vote is 1
        /// </summary>
        [JsonProperty("up")]
        public int UpVotes { get; set; }

        /// <summary>
        /// Number of downvotes, where each downvote is -1
        /// </summary>
        [JsonProperty("down")]
        public int DownVotes { get; set; }

        /// <summary>
        /// Total score, as upvotes + downvotes
        /// </summary>
        [JsonProperty("total")]
        public int TotalScore { get; set; }
    }
}
