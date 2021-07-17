using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YerfTagger.Imaging
{
    public class ImageInformation
    {
        public ImageFormat Format { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool HasTransparency { get; set; }
        public float CompressedBitsPerPixel { get; set; }
    }
}
