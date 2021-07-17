using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YerfTagger.E621.Schemas
{
    public class BooruPostInformation
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("created_at")]
        public DateTimeOffset? CreatedAt { get; set; }

        [JsonProperty("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }

        [JsonProperty("file")]
        public BooruImageFileInfo File { get; set; }

        [JsonProperty("preview")]
        public BooruImageFileInfo Preview { get; set; }

        [JsonProperty("sample")]
        public BooruImageFileInfo Sample { get; set; }

        [JsonProperty("score")]
        public BooruScoreSummary Score { get; set; }

        [JsonProperty("tags")]
        public BooruTagGroup Tags { get; set; }

        [JsonProperty("locked_tags")]
        public HashSet<string> LockedTags { get; set; }

        [JsonProperty("rating")]
        public BooruPostRating Rating { get; set; }

        [JsonProperty("fav_count")]
        public int FavoriteCount { get; set; }

        [JsonProperty("sources")]
        public List<string> Sources { get; set; }

        // relationships
        // approver_id
        // uploader_id

        [JsonProperty("pools")]
        public List<long> Pools { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("comment_count")]
        public int CommentCount { get; set; }

        [JsonProperty("is_favorited")]
        public bool IsFavorited { get; set; }

        [JsonProperty("has_notes")]
        public bool HasNotes { get; set; }
    }
}
