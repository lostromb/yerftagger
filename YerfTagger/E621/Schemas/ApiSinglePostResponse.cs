using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YerfTagger.E621.Schemas
{
    /// <summary>
    /// Response schema for calls to /posts.json
    /// </summary>
    public class ApiSinglePostResponse
    {
        [JsonProperty("post")]
        public BooruPostInformation Post { get; set; }
    }
}
