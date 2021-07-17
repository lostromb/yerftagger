using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;

namespace YerfTagger.E621.Schemas
{
    [JsonConverter(typeof(PostRatingJsonConverter))]
    public enum BooruPostRating
    {
        Safe,
        Questionable,
        Explicit
    }

    internal class PostRatingJsonConverter : JsonConverter<BooruPostRating>
    {
        public override BooruPostRating ReadJson(JsonReader reader, Type objectType, BooruPostRating existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String)
            {
                string strVal = reader.Value as string;
                if (string.Equals("e", strVal))
                {
                    return BooruPostRating.Explicit;
                }
                else if (string.Equals("q", strVal))
                {
                    return BooruPostRating.Questionable;
                }
                else
                {
                    return BooruPostRating.Safe;
                }
            }
            else if(reader.TokenType == JsonToken.Null)
            {
                // if for some reason it's null, null, default to safe
                return BooruPostRating.Safe;
            }
            else
            {
                throw new FormatException("Unexpected JSON token " + reader.Value + " at path " + reader.Path);
            }
        }

        public override void WriteJson(JsonWriter writer, BooruPostRating value, JsonSerializer serializer)
        {
            string strVal = "s";
            if (value == BooruPostRating.Explicit)
            {
                strVal = "e";
            }
            else if (value == BooruPostRating.Questionable)
            {
                strVal = "q";
            }

            writer.WriteValue(strVal);
        }
    }
}
