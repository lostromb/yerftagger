using Durandal.Common.NLP.Feature;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YerfTagger.E621.Schemas;

namespace YerfTagger
{
    public class PostFeatureExtractor
    {
        private static readonly int[] SCORE_THRESHOLDS = new int[]
        {
            5, 10, 15, 20, 25, 30, 40, 50, 60, 70, 80, 90, 100, 120, 140, 160, 180, 200, 250, 300, 350, 400, 450, 500
        };

        private static readonly int[] SIZE_THRESHOLDS = new int[]
        {
            1000, 1500, 2000, 2500
        };

        public string[] ExtractFeatures(BooruPostInformation post)
        {
            List<string> features = new List<string>();

            if (post.Rating == BooruPostRating.Safe)
            {
                features.Add("rtg:s");
            }
            else if (post.Rating == BooruPostRating.Questionable)
            {
                features.Add("rtg:q");
            }
            else if (post.Rating == BooruPostRating.Explicit)
            {
                features.Add("rtg:e");
            }

            foreach (var generalTag in post.Tags.General)
            {
                features.Add("tag:" + generalTag);
            }

            foreach (var artistTag in post.Tags.Artist)
            {
                features.Add("ast:" + artistTag);
            }

            foreach (var metaTag in post.Tags.Character)
            {
                features.Add("chr:" + metaTag);
            }

            foreach (var metaTag in post.Tags.Copyright)
            {
                features.Add("cpy:" + metaTag);
            }

            foreach (var metaTag in post.Tags.Species)
            {
                features.Add("spc:" + metaTag);
            }

            foreach (var metaTag in post.Tags.Meta)
            {
                features.Add("mta:" + metaTag);
            }

            foreach (int threshold in SCORE_THRESHOLDS)
            {
                if (post.Score.UpVotes > threshold)
                {
                    features.Add("upvote-above:" + threshold);
                }
                else
                {
                    features.Add("upvote-below:" + threshold);
                }

                if (post.Score.DownVotes < (0 - threshold))
                {
                    features.Add("downvote-below:" + threshold);
                }
                else
                {
                    features.Add("downvote-above:" + threshold);
                }

                if (post.FavoriteCount > threshold)
                {
                    features.Add("favorites-above:" + threshold);
                }
                else
                {
                    features.Add("favorites-below:" + threshold);
                }
            }

            foreach (int threshold in SIZE_THRESHOLDS)
            {
                if (post.File.Width >= threshold ||
                    post.File.Height >= threshold)
                {
                    features.Add("size-above:" + threshold);
                }
                else if (post.File.Width < threshold &&
                         post.File.Height < threshold)
                {
                    features.Add("size-below:" + threshold);
                }
            }

            // not yet in training data
            //if (post.Pools != null && post.Pools.Count > 0)
            //{
            //    features.Add("is-pool:yes");
            //}
            //else
            //{
            //    features.Add("is-pool:no");
            //}

            return features.ToArray();
        }
    }
}
