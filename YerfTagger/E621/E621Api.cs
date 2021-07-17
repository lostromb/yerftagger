using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Net.Http;
using Durandal.Common.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using YerfTagger.Cache;
using YerfTagger.E621.Schemas;
using YerfTagger.Imaging;

namespace YerfTagger.E621
{
    public class E621Api : IDisposable
    {
        private const double MAX_BACKOFF_SECONDS = 60;
        private const double IDEAL_THROTTLE_SECONDS_PER_REQUEST = 1;
        private static readonly string SAFE_HOST_URL = "e926.net";
        private static readonly string EXPLICIT_HOST_URL = "e621.net";
        private static readonly string USER_AGENT = "Yerf Tagger (by user Durandal)";
        private readonly IHttpClient _webClient;
        private readonly JsonSerializerSettings _serializer;
        private readonly MovingAverage _rateLimiterDelayAverage;
        private int _disposed = 0;

        public E621Api(ILogger logger, IHttpClientFactory httpClientFactory, bool enableExplicitImages = false)
        {
            _webClient = httpClientFactory.CreateHttpClient(enableExplicitImages ? EXPLICIT_HOST_URL : SAFE_HOST_URL, 443, true, logger.Clone("HttpClient"));
            _rateLimiterDelayAverage = new MovingAverage(20, IDEAL_THROTTLE_SECONDS_PER_REQUEST);

            _serializer = new JsonSerializerSettings()
            {
                NullValueHandling = NullValueHandling.Ignore,
                DateParseHandling = DateParseHandling.DateTimeOffset,
                DateTimeZoneHandling = DateTimeZoneHandling.RoundtripKind
            };
        }

        ~E621Api()
        {
            Dispose(false);
        }

        public async Task<BooruPostInformation> GetFileInformation(ILogger logger, FileInfo file, ImageInformation imageMetadata = null)
        {
            // Is the file name already a hash?
            BooruPostInformation postInfo = null;
            string fileNameHash = ParseHashFromFilename(file.Name);
            if (fileNameHash != null)
            {
                postInfo = await GetPostByMD5(logger, fileNameHash);
            }

            // Look up file by hash code
            if (postInfo == null)
            {
                postInfo = await GetPostByMD5(logger, CalculateMD5HashOfFile(file));
            } 

            // Look up file by keyword search
            if (postInfo == null)
            {
                IList<string> tagsFromFilename = ExtractTagsFromFileName(file.Name);
                if (tagsFromFilename != null && tagsFromFilename.Count > 0)
                {
                    IList<BooruPostInformation> matchedPosts = await ListPosts(
                        logger,
                        before_id: null,
                        pageNum: 0,
                        perPage: 10,
                        tagSearch: tagsFromFilename);

                    // Either one post matched, or there is a post with the same image dimensions as our input
                    if (matchedPosts.Count == 1)
                    {
                        postInfo = matchedPosts[0];
                    }
                    else if (imageMetadata != null && matchedPosts.Count > 1)
                    {
                        foreach (BooruPostInformation matchedPost in matchedPosts)
                        {
                            if (matchedPost.File.Width == imageMetadata.Width &&
                                matchedPost.File.Height == imageMetadata.Height)
                            {
                                postInfo = matchedPost;
                                break;
                            }
                        }
                    }
                }
            }

            return postInfo;
        }

        public async Task<IList<BooruPostInformation>> ListPosts(ILogger logger, long? before_id = null, int pageNum = 0, int perPage = 50, IEnumerable<string> tagSearch = null)
        {
            if (pageNum < 0)
            {
                throw new ArgumentOutOfRangeException("Page number cannot be negative");
            }
            if (pageNum > 750)
            {
                throw new ArgumentOutOfRangeException("Cannot iterate beyond page 750");
            }
            if (perPage < 1)
            {
                throw new ArgumentOutOfRangeException("Cannot list zero results per page");
            }
            if (perPage > 320)
            {
                throw new ArgumentOutOfRangeException("Can not return more than 320 posts per page");
            }

            HttpRequest request = HttpRequest.BuildFromUrlString("/posts.json", "GET");
            request.GetParameters["page"] = pageNum.ToString();
            request.GetParameters["limit"] = perPage.ToString();
            if (before_id.HasValue)
            {
                request.GetParameters["before_id"] = before_id.Value.ToString();
            }

            if (tagSearch != null)
            {
                StringBuilder tagSearchBuilder = new StringBuilder();
                foreach (string tag in tagSearch)
                {
                    if (tagSearchBuilder.Length != 0)
                    {
                        tagSearchBuilder.Append(" ");
                    }

                    if (!string.IsNullOrWhiteSpace(tag))
                    {
                        tagSearchBuilder.Append(tag);
                    }
                }

                if (tagSearchBuilder.Length != 0)
                {
                    request.GetParameters["tags"] = tagSearchBuilder.ToString();
                }
            }

            request.RequestHeaders["User-Agent"] = USER_AGENT;
            HttpResponse webResponse = await SendHttpRequestWithRetries(request, logger);
            if (webResponse == null)
            {
                logger.Log("Null web response when fetching post listing", LogLevel.Err);
                return null;
            }
            else if (webResponse.ResponseCode == 404)
            {
                logger.Log("Post listing not found!", LogLevel.Err);
                return null;
            }
            else if (webResponse.ResponseCode != 200)
            {
                logger.Log("Non-success web response " + webResponse.ResponseCode + " when listing posts", LogLevel.Err);
                logger.Log(webResponse.GetPayloadAsString(), LogLevel.Err);
                return null;
            }
            else if (webResponse.ResponseHeaders.ContainsKey("Content-Type") &&
               webResponse.ResponseHeaders["Content-Type"].Contains("application/json"))
            {
                string data = webResponse.GetPayloadAsString();
                if (!string.IsNullOrEmpty(data))
                {
                    ApiMultiplePostsResponse parsedResponse = JsonConvert.DeserializeObject<ApiMultiplePostsResponse>(data, _serializer);
                    if (parsedResponse == null)
                    {
                        logger.Log("Posts response failed to parse");
                        return null;
                    }

                    return parsedResponse.Posts;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                logger.Log("Unexpected content type on web response, expected application/json", LogLevel.Err);
                return null;
            }
        }

        public async Task<BooruTag> GetTagInformation(ILogger logger, string tag)
        {
            HttpRequest request = HttpRequest.BuildFromUrlString("/tags.json?search%5Bhide_empty%5D=yes&search%5Bname_matches%5D=" + WebUtility.UrlEncode(tag), "GET");
            request.RequestHeaders["User-Agent"] = USER_AGENT;
            HttpResponse webResponse = await SendHttpRequestWithRetries(request, logger);
            if (webResponse == null)
            {
                logger.Log("Null web response when fetching tag \"" + tag + "\"", LogLevel.Err);
                return null;
            }
            else if (webResponse.ResponseCode == 404)
            {
                logger.Log("Tag " + tag + " not found!", LogLevel.Err);
                return null;
            }
            else if (webResponse.ResponseCode != 200)
            {
                logger.Log("Non-success web response " + webResponse.ResponseCode + " when fetching tag \"" + tag + "\"", LogLevel.Err);
                logger.Log(webResponse.GetPayloadAsString(), LogLevel.Err);
                return null;
            }
            else if (!webResponse.ResponseHeaders.ContainsKey("Content-Type") ||
               !webResponse.ResponseHeaders["Content-Type"].Contains("application/json"))
            {
                // An HTML response means "not found", for whatever reason
                logger.Log("Tag " + tag + " not found!", LogLevel.Err);
                return null;
            }
            else if (webResponse.ResponseHeaders.ContainsKey("Content-Type") &&
               webResponse.ResponseHeaders["Content-Type"].Contains("application/json"))
            {
                string data = webResponse.GetPayloadAsString();
                if (data.StartsWith("{"))
                {
                    logger.Log("Tag " + tag + " does not exist", LogLevel.Err);
                    return null;
                }
                if (!string.IsNullOrEmpty(data))
                {
                    List<BooruTag> parsedResponse = JsonConvert.DeserializeObject<List<BooruTag>>(data, _serializer);
                    if (parsedResponse == null || parsedResponse.Count == 0)
                    {
                        logger.Log("Tag response failed to parse when fetching tag \"" + tag + "\"");
                        return null;
                    }

                    logger.Log("Got tag info for \"" + tag + "\"");
                    return parsedResponse[0];
                }
                else
                {
                    return null;
                }
            }
            else
            {
                logger.Log("Unexpected content type on web response, expected application/json", LogLevel.Err);
                return null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 1)
            {
                return;
            }

            if (disposing)
            {
                _webClient?.Dispose();
            }
        }

        private static readonly Regex LikelyTagNameRegex = new Regex("^[a-zA-Z0-9_\\-]{1,20}$");

        private static IList<string> ExtractTagsFromFileName(string fileName)
        {
            if (fileName.Contains("."))
            {
                // trim file extension
                fileName = fileName.Substring(0, fileName.LastIndexOf('.'));
            }

            string[] fileNameParts = fileName.Split(' ');
            IList<string> returnVal = new List<string>();
            foreach (string fileNamePart in fileNameParts)
            {
                if (LikelyTagNameRegex.IsMatch(fileNamePart))
                {
                    returnVal.Add(fileNamePart);
                }
            }

            return returnVal;
        }

        private static string CalculateMD5HashOfFile(FileInfo file)
        {
            MD5 hasher = new MD5Cng();
            hasher.Initialize();
            StringBuilder returnVal = new StringBuilder();
            using (FileStream stream = new FileStream(file.FullName, FileMode.Open))
            {
                byte[] hash = hasher.ComputeHash(stream);
                stream.Close();
                BinaryHelpers.ToHexString(hash, 0, 16, returnVal);
            }

            return returnVal.ToString().ToLowerInvariant();
        }

        private static string ParseHashFromFilename(string filename)
        {
            if (filename.Contains('.'))
            {
                string fileNameWithoutExtension = filename.Substring(0, filename.LastIndexOf('.'));
                Guid hash;
                if (fileNameWithoutExtension.Length == 32 && Guid.TryParse(fileNameWithoutExtension, out hash))
                {
                    return hash.ToString("N").ToLowerInvariant();
                }
            }

            return null;
        }

        private async Task<BooruPostInformation> GetPostByMD5(ILogger logger, string md5)
        {
            logger.Log("Fetching art information by hash " + md5);
            HttpRequest request = HttpRequest.BuildFromUrlString("/posts.json", "GET");
            request.GetParameters["md5"] = md5;
            request.RequestHeaders["User-Agent"] = USER_AGENT;
            HttpResponse webResponse = await SendHttpRequestWithRetries(request, logger);
            if (webResponse == null)
            {
                logger.Log("Null web response", LogLevel.Err);
                return null;
            }
            else if (webResponse.ResponseCode == 404)
            {
                logger.Log("Image hash " + md5 + " not found!", LogLevel.Err);
                return null;
            }
            else if (webResponse.ResponseCode != 200)
            {
                logger.Log("Non-success web response " + webResponse.ResponseCode, LogLevel.Err);
                logger.Log(webResponse.GetPayloadAsString(), LogLevel.Err);
                return null;
            }
            else if (!webResponse.ResponseHeaders.ContainsKey("Content-Type") ||
               !webResponse.ResponseHeaders["Content-Type"].Contains("application/json"))
            {
                // An HTML response means "not found", for whatever reason
                logger.Log("Image hash " + md5 + " not found!", LogLevel.Err);
                return null;
            }
            else if (webResponse.ResponseHeaders.ContainsKey("Content-Type") &&
               webResponse.ResponseHeaders["Content-Type"].Contains("application/json"))
            {
                string data = webResponse.GetPayloadAsString();
                if (!string.IsNullOrEmpty(data))
                {
                    ApiSinglePostResponse searchResponse = JsonConvert.DeserializeObject<ApiSinglePostResponse>(data, _serializer);
                    if (searchResponse == null)
                    {
                        return null;
                    }

                    return searchResponse.Post;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                logger.Log("Unexpected content type on web response, expected application/json", LogLevel.Err);
                return null;
            }
        }

        private async Task<HttpResponse> SendHttpRequestWithRetries(HttpRequest request, ILogger logger, int maxRetries = 10)
        {
            HttpResponse response = null;
            int retryCount = 0;

            while (retryCount++ < maxRetries)
            {
                await Task.Delay(TimeSpan.FromSeconds(_rateLimiterDelayAverage.Average));

                response = await _webClient.SendRequestAsync(request);
                if (response == null)
                {
                    return null;
                }
                else if (response.ResponseCode == 419)
                {
                    // Backoff. Tell the rate limiter to start moving towards the upper backoff range
                    _rateLimiterDelayAverage.Add(MAX_BACKOFF_SECONDS);
                    logger.Log("Request " + retryCount + " throttled. Will delay for " + _rateLimiterDelayAverage.Average + " seconds", LogLevel.Wrn);
                }
                else
                {
                    _rateLimiterDelayAverage.Add(IDEAL_THROTTLE_SECONDS_PER_REQUEST);
                    return response;
                }
            }

            return response;
        }
    }
}
