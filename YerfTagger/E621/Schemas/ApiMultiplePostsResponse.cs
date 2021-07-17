using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YerfTagger.E621.Schemas
{
    /// <summary>
    /// Response schema for calls to /posts.json when a page of posts is requested
    /// </summary>
    public class ApiMultiplePostsResponse
    {
        [JsonProperty("posts")]
        public IList<BooruPostInformation> Posts { get; set; }
    }
}
