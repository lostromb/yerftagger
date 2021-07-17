using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YerfTagger.Imaging
{
    public abstract class ImageConversionParameters
    {
        /// <summary>
        /// The target image format to convert to
        /// </summary>
        public abstract ImageFormat TargetFormat { get; }

        /// <summary>
        /// If specified, scales the image proportionally until its width is this value in pixels
        /// </summary>
        public int? TargetWidth { get; }

        /// <summary>
        /// If specified, scales the image proportionally until its height is this value in pixels
        /// </summary>
        public int? TargetHeight { get; }

        /// <summary>
        /// If specified, scales the image proportionally until its longest dimension (either width or height) is this value in pixels
        /// </summary>
        public int? TargetLongestDimension { get; }
    }
}
