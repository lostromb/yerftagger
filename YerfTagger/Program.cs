using Durandal.API.Data;
using Durandal.Common.File;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.NLP.Classification;
using Durandal.Common.NLP.Classification.SharpEntropy;
using Durandal.Common.NLP.Feature;
using Durandal.Common.NLP.Indexing;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YerfTagger.Cache;
using YerfTagger.E621;
using YerfTagger.E621.Schemas;
using YerfTagger.Imaging;

namespace YerfTagger
{
    public class Program
    {
        private static readonly HashSet<string> ForbiddenTags = new HashSet<string>()
        {
            "anon", "animated", "big_breasts", "diaper", "five_nights_at_freddy's", "foot_fetish", "foot_focus", "gaping_mouth",
            "inflation", "koopa", "lingerie", "lola_bunny", "morbidly_obese", "muscular", "muscular_male", "nightmare_fuel",
            "obese", "overweight_male", "panties", "pinup", "pregnant", "profanity", "rubber", "skimpy", "source_filmmaker",
            "teenage_mutant_ninja_turtles", "transformation", "vore"
        };

        public static void Main(string[] args)
        {
            AsyncMain().Await();
        }

        private static async Task AsyncMain()
        {
            ILogger logger = new ConsoleLogger();
            IFileSystem fileSystem = new WindowsFileSystem(logger.Clone("FileSystem"), @"C:\Code\YerfTagger\runtime");
            BasicCompactIndex<string> stringIndex = BasicCompactIndex<string>.BuildStringIndex();
            Dictionary<string, BooruPostInformation> positivePosts = JsonConvert.DeserializeObject<Dictionary<string, BooruPostInformation>>(File.ReadAllText(@"C:\Code\YerfTagger\runtime\training-positive-posts.json", Encoding.UTF8));
            Dictionary<long, BooruPostInformation> negativePosts = JsonConvert.DeserializeObject<Dictionary<long, BooruPostInformation>>(File.ReadAllText(@"C:\Code\YerfTagger\runtime\training-negative-posts.json", Encoding.UTF8));
            MaxEntClassifier classifier = new MaxEntClassifier(stringIndex, logger.Clone("MaxEntClassifier"), fileSystem, "imageClassifier");
            PostFeatureExtractor featureExtractor = new PostFeatureExtractor();
            List<TrainingEvent> trainingEvents = new List<TrainingEvent>();

            logger.Log("Running feature extraction...");
            foreach (var positivePost in positivePosts.Values)
            {
                trainingEvents.Add(new TrainingEvent("1", featureExtractor.ExtractFeatures(positivePost)));
            }
            foreach (var negativePost in negativePosts.Values)
            {
                trainingEvents.Add(new TrainingEvent("0", featureExtractor.ExtractFeatures(negativePost)));
            }

            // Shuffle the training event list
            IRandom rand = new FastRandom();
            for (int c = 0; c < 10000; c++)
            {
                int src = rand.NextInt(0, trainingEvents.Count);
                int dst = rand.NextInt(0, trainingEvents.Count);
                if (src != dst)
                {
                    TrainingEvent swap = trainingEvents[src];
                    trainingEvents[src] = trainingEvents[dst];
                    trainingEvents[dst] = swap;
                }
            }

            logger.Log("Training classifier...");
            ITrainingEventReader dataReader = new BasicTrainingEventReader(trainingEvents);
            classifier.TrainFromData(dataReader, new VirtualPath("imageClassifierModel"), 5.0f);

            using (TcpClientSocketFactory socketFactory = new TcpClientSocketFactory(logger.Clone("SocketFactory")))
            {
                HttpSocketClientFactory httpClientFactory = new HttpSocketClientFactory(socketFactory, DefaultRealTimeProvider.Singleton);
                using (E621Api api = new E621Api(logger.Clone("E926API"), httpClientFactory))
                {
                    for (int page = 10; page < 350; page++)
                    {
                        logger.Log("Inspecting page " + page);
                        IList<BooruPostInformation> posts;
                        posts = await api.ListPosts(
                            logger,
                            pageNum: page,
                            perPage: 75);

                        if (posts != null)
                        {
                            foreach (BooruPostInformation post in posts)
                            {
                                try
                                {
                                    if (post != null && !string.Equals(post.File.Extension, "webm", StringComparison.OrdinalIgnoreCase))
                                    {
                                        List<Hypothesis<string>> classificationHyps = classifier.ClassifyAll(featureExtractor.ExtractFeatures(post));
                                        Hypothesis<string> positiveHyp = classificationHyps.Single((x) => string.Equals(x.Value, "1"));
                                        if (!ContainsForbiddenTags(post) && positiveHyp != null && positiveHyp.Conf > 0.4f)
                                        {
                                            Uri imageUri = new Uri(post.File.Url);
                                            using (IHttpClient httpClient = httpClientFactory.CreateHttpClient(imageUri, logger))
                                            {
                                                logger.Log(string.Format("I am {0:F2}% sure you will like this image: {1}", positiveHyp.Conf * 100, post.File.MD5));
                                                HttpResponse imageDownloadResponse = await httpClient.SendRequestAsync(HttpRequest.BuildFromUrlString(post.File.Url));
                                                if (imageDownloadResponse != null && imageDownloadResponse.ResponseCode == 200)
                                                {
                                                    string downloadedFileName = @"C:\Code\YerfTagger\runtime\" + post.File.MD5 + "." + post.File.Extension;
                                                    using (Stream downloadStream = imageDownloadResponse.GetPayloadAsStream())
                                                    using (FileStream fileStream = new FileStream(downloadedFileName, FileMode.Create, FileAccess.Write))
                                                    {
                                                        await downloadStream.CopyToAsync(fileStream);
                                                        logger.Log("Downloaded " + downloadedFileName);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    logger.Log(e);
                                }
                            }
                        }
                    }
                }
            }

            Console.WriteLine("Done");
        }

        private static bool ContainsForbiddenTags(BooruPostInformation post)
        {
            foreach (string tag in post.Tags.General)
            {
                if (ForbiddenTags.Contains(tag))
                {
                    return true;
                }
            }
            foreach (string tag in post.Tags.Artist)
            {
                if (ForbiddenTags.Contains(tag))
                {
                    return true;
                }
            }

            return false;
        }

        private static async Task BuildUpNegativeRankerTraining()
        {
            ILogger logger = new ConsoleLogger();
            using (TcpClientSocketFactory socketFactory = new TcpClientSocketFactory(logger.Clone("SocketFactory")))
            {
                HttpSocketClientFactory httpClientFactory = new HttpSocketClientFactory(socketFactory, DefaultRealTimeProvider.Singleton);
                using (E621Api api = new E621Api(logger.Clone("E926API"), httpClientFactory))
                {
                    HashSet<long> taggedPositivePostIds = new HashSet<long>();
                    string positiveRankerTrainingFile = @"C:\Code\YerfTagger\training-positive-posts.json";
                    if (File.Exists(positiveRankerTrainingFile))
                    {
                        Dictionary<string, BooruPostInformation> positivePosts = JsonConvert.DeserializeObject<Dictionary<string, BooruPostInformation>>(File.ReadAllText(positiveRankerTrainingFile, Encoding.UTF8));
                        foreach (var post in positivePosts.Values)
                        {
                            if (!taggedPositivePostIds.Contains(post.Id))
                            {
                                taggedPositivePostIds.Add(post.Id);
                            }
                        }
                    }

                    const int RESULTS_PER_PAGE = 75;
                    Dictionary<long, BooruPostInformation> negativePosts = new Dictionary<long, BooruPostInformation>();
                    string negativeRankerTrainingFileName = @"C:\Code\YerfTagger\training-negative-posts.json";
                    for (int page = 350; page < 550; page++)
                    {
                        logger.Log("Indexing page " + page);
                        IList<BooruPostInformation> posts;
                        posts = await api.ListPosts(
                            logger,
                            pageNum: page,
                            perPage: RESULTS_PER_PAGE);

                        if (posts != null)
                        {
                            foreach (BooruPostInformation post in posts)
                            {
                                if (post != null)
                                {
                                    if (!negativePosts.ContainsKey(post.Id) &&
                                        !taggedPositivePostIds.Contains(post.Id))
                                    {
                                        negativePosts.Add(post.Id, post);
                                    }
                                }
                            }

                            File.WriteAllText(negativeRankerTrainingFileName, JsonConvert.SerializeObject(negativePosts), Encoding.UTF8);
                        }
                    }
                }
            }
        }

        private static async Task BuildUpPositiveRankerTraining(ILogger logger, E621Api api)
        {
            DirectoryInfo rankerTrainingInput = new DirectoryInfo(@"C:\Code\YerfTagger\rankertraining-good");
            Dictionary<string, BooruPostInformation> posts;

            string rankerTrainingFileName = @"C:\Code\YerfTagger\training-positive-posts.json";
            if (File.Exists(rankerTrainingFileName))
            {
                posts = JsonConvert.DeserializeObject<Dictionary<string, BooruPostInformation>>(File.ReadAllText(rankerTrainingFileName, Encoding.UTF8));
            }
            else
            {
                posts = new Dictionary<string, BooruPostInformation>();
            }

            foreach (FileInfo rankerInputFile in rankerTrainingInput.EnumerateFiles())
            {
                string nameWithoutExtension = rankerInputFile.Name.Substring(0, rankerInputFile.Name.Length - rankerInputFile.Extension.Length);

                if (!posts.ContainsKey(nameWithoutExtension))
                {
                    ImageInformation imageInfo = ImageInspector.GetImageInformation(rankerInputFile);
                    if (imageInfo != null && imageInfo.Format != ImageFormat.Unknown)
                    {
                        BooruPostInformation fileInfo = await api.GetFileInformation(logger, rankerInputFile, imageInfo);
                        if (fileInfo != null)
                        {
                            posts.Add(nameWithoutExtension, fileInfo);
                            File.WriteAllText(rankerTrainingFileName, JsonConvert.SerializeObject(posts), Encoding.UTF8);
                        }
                    }
                }
            }
        }

        private static async Task TagAllArtInDirectory(string[] args)
        {
            ILogger logger = new ConsoleLogger();
            DirectoryInfo sourceDir = new DirectoryInfo(".");
            if (args.Length > 0)
            {
                sourceDir = new DirectoryInfo(args[0]);
            }

            if (!sourceDir.Exists)
            {
                Console.WriteLine("ERROR: Source directory " + sourceDir.FullName + " does not exist!");
                Environment.Exit(-1);
            }

            WindowsFileSystem localFileSystem = new WindowsFileSystem(logger.Clone("FileSystem"));

            // Load configuration

            int readCount = 0;
            int skippedCount = 0;
            int notFoundCount = 0;
            int successCount = 0;

            logger.Log("Initializing cache and API client...");
            using (TcpClientSocketFactory socketFactory = new TcpClientSocketFactory(logger.Clone("SocketFactory")))
            {
                HttpSocketClientFactory httpClientFactory = new HttpSocketClientFactory(socketFactory, DefaultRealTimeProvider.Singleton);
                using (E621Api api = new E621Api(logger.Clone("E926API"), httpClientFactory))
                using (TagCache tagCache = await TagCache.Create(api, localFileSystem, new VirtualPath("tags.cache"), logger.Clone("TagCache")))
                {
                    logger.Log("Tagging all artwork files in " + sourceDir.FullName);

                    foreach (FileInfo file in sourceDir.EnumerateFiles())
                    {
                        if (file.Extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                            file.Extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                            file.Extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                            file.Extension.Equals(".gif", StringComparison.OrdinalIgnoreCase))
                        {
                            readCount++;
                            if (FileIsGoodCandidateForConversion(file.Name))
                            {
                                BooruPostInformation postInfo = await api.GetFileInformation(logger, file, null);
                                if (postInfo != null && postInfo.Tags != null && postInfo.Tags.General != null && postInfo.Tags.General.Count > 0)
                                {
                                    string newFileName = await CreateNewFileName(postInfo, tagCache, file);
                                    logger.Log("Renaming " + file.Name + " to " + newFileName);
                                    RenameFileInPlace(file, newFileName);
                                    successCount++;
                                }
                                else
                                {
                                    logger.Log("File " + file.Name + " not found in db");
                                    notFoundCount++;
                                }
                            }
                            else
                            {
                                logger.Log("Skipping " + file.Name + " as it does not seem to be a good candidate");
                                skippedCount++;
                            }
                        }
                    }
                }
            }

            logger.Log("Finished!");
            logger.Log(string.Format("Files READ {0}\tSKIPPED {1}\tNOTFOUND {2}\tCONVERTED {3}", readCount, skippedCount, notFoundCount, successCount));
        }
        
        public static bool FileIsGoodCandidateForConversion(string filename)
        {
            if (ParseHashFromFilename(filename) != null)
            {
                return true;
            }

            if (filename.Length < 15)
            {
                return true;
            }

            return false;
        }

        public static string ParseHashFromFilename(string filename)
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
        
        public static async Task<string> CreateNewFileName(BooruPostInformation post, IAsyncReadThroughCache<string, BooruTag> tagCache, FileInfo originalFile)
        {
            // Build a dictionary of all tags in the post
            Dictionary<string, BooruTag> relevantTags = new Dictionary<string, BooruTag>();
            foreach (string tag in post.Tags.General)
            {
                relevantTags[tag] = await tagCache.GetCache(tag);
            }
            foreach (string tag in post.Tags.Species)
            {
                relevantTags[tag] = await tagCache.GetCache(tag);
            }

            StringBuilder builder = new StringBuilder();
            // Separate tags into mandatory and optional
            List<string> mandatoryTags = new List<string>(post.Tags.Artist);
            List<string> optionalTags = new List<string>();
            optionalTags.AddRange(
                post.Tags.Species.Where((t) => relevantTags.ContainsKey(t) && !IsTagInCommonSet(t))
                .OrderBy((t) => relevantTags[t].PostCount)
                .Select((t) => t));
            optionalTags.AddRange(
                post.Tags.General.Where((t) => relevantTags.ContainsKey(t) && relevantTags[t].PostCount > 50 && !IsTagInCommonSet(t))
                .OrderBy((t) => relevantTags[t].PostCount)
                .Select((t) => t));
            optionalTags.AddRange(
                post.Tags.General.Where((t) => relevantTags.ContainsKey(t) && IsTagInCommonSet(t))
                .OrderBy((t) => relevantTags[t].PostCount)
                .Select((t) => t));
            builder.Append(string.Join(" ", mandatoryTags));
            foreach (string optionalTag in optionalTags)
            {
                if (builder.Length > 100)
                {
                    break;
                }

                builder.Append(" " + optionalTag);
            }

            builder.Append(originalFile.Extension);

            string rawFileName = builder.ToString().Trim();
            char[] invalidFileChars = Path.GetInvalidFileNameChars();
            foreach (char invalidChar in invalidFileChars)
            {
                if (rawFileName.Contains(invalidChar))
                {
                    rawFileName = rawFileName.Replace(invalidChar, '_');
                }
            }

            return rawFileName;
        }

        private static readonly HashSet<string> CommonTags = new HashSet<string>()
        {
            "mammal", "scalie", "avian", "female", "male", "ambiguous_gender", "solo", "duo", "anthro", "clothing", "fur", "feathers", "breasts", "hair"
        };

        public static bool IsTagInCommonSet(string tagName)
        {
            return CommonTags.Contains(tagName.ToLowerInvariant());
        }

        public static void RenameFileInPlace(FileInfo file, string newName)
        {
            string destinationFile = file.DirectoryName + Path.DirectorySeparatorChar + newName;
            while (File.Exists(destinationFile))
            {
                newName = "DUPLICATE " + newName;
                destinationFile = file.DirectoryName + Path.DirectorySeparatorChar + newName;
            }

            file.MoveTo(destinationFile);
        }
    }
}
