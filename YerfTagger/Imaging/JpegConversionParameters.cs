using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YerfTagger.Imaging
{
    public class JpegConversionParameters : ImageConversionParameters
    {
        /// <summary>
        /// The target image format to convert to
        /// </summary>
        public override ImageFormat TargetFormat => ImageFormat.Jpeg;

        /// <summary>
        /// The target JPEG quality as an int from 0 to 100
        /// </summary>
        public int JpegQuality { get; set; }
    }
}
