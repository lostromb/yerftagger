using Durandal.Common.Cache;
using Durandal.Common.File;
using Durandal.Common.Logger;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YerfTagger.E621.Schemas;

namespace YerfTagger.E621
{
    public class TagCache : FileBasedReadThroughCache<string, BooruTag>
    {
        private readonly E621Api _e621Api;
        private readonly JsonSerializer _serializer;
        private readonly ILogger _logger;

        private TagCache(E621Api e621Api, IFileSystem fileSystem, VirtualPath cacheFileName, ILogger logger)
            : base(fileSystem, cacheFileName, logger)
        {
            _e621Api = e621Api;
            _logger = logger;
            _serializer = new JsonSerializer()
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.Indented
            };
        }

        /// <summary>
        /// Async constructor pattern.
        /// </summary>
        /// <param name="e621Api">An api to fetch booru tags from</param>
        /// <param name="fileSystem">The local filesystem for the cache file</param>
        /// <param name="cacheFileName">The name of the actual cache file</param>
        /// <param name="logger">A logger</param>
        /// <returns></returns>
        public static async Task<TagCache> Create(E621Api e621Api, IFileSystem fileSystem, VirtualPath cacheFileName, ILogger logger)
        {
            TagCache returnVal = new TagCache(e621Api, fileSystem, cacheFileName, logger);
            await returnVal.InitializeCacheFile();
            return returnVal;
        }

        protected override async Task<RetrieveResult<BooruTag>> CacheMiss(string key)
        {
            BooruTag tag = await _e621Api.GetTagInformation(_logger, key);
            if (tag != null)
            {
                return new RetrieveResult<BooruTag>(tag);
            }

            return new RetrieveResult<BooruTag>();
        }

        protected override Task DeserializeCacheFile(Stream cacheFileStream, IDictionary<string, BooruTag> targetDictionary)
        {
            using (StreamReader reader = new StreamReader(cacheFileStream, Encoding.UTF8))
            using (JsonTextReader jsonReader = new JsonTextReader(reader))
            {
                // OPT: double storage of memory here
                Dictionary<string, BooruTag> tags = _serializer.Deserialize<Dictionary<string, BooruTag>>(jsonReader);
                foreach (var kvp in tags)
                {
                    targetDictionary.Add(kvp);
                }

                return DurandalTaskExtensions.NoOpTask;
            }
        }

        protected override Task SerializeCacheFile(IDictionary<string, BooruTag> cachedItems, Stream cacheFileOutStream)
        {
            using (StreamWriter writer = new StreamWriter(cacheFileOutStream, Encoding.UTF8))
            using (JsonTextWriter jsonWriter = new JsonTextWriter(writer))
            {
                _serializer.Serialize(jsonWriter, cachedItems);
            }

            return DurandalTaskExtensions.NoOpTask;
        }
    }
}
