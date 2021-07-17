using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YerfTagger.Imaging
{
    public static class ImageInspector
    {
        public static ImageInformation GetImageInformation(FileInfo inputFile)
        {
            // Check file extension
            ImageFormat expectedFormat = GuessFormatFromFileExtension(inputFile.Extension);
            if (expectedFormat == ImageFormat.Unknown)
            {
                return null;
            }

            using (Bitmap x = Image.FromFile(inputFile.FullName) as Bitmap)
            {
                try
                {
                    ImageInformation returnVal = new ImageInformation();
                    returnVal.Format = expectedFormat;
                    returnVal.Width = x.Width;
                    returnVal.Height = x.Height;
                    returnVal.HasTransparency = false;

                    // Sample the corners for transparency
                    if (x.GetPixel(0, 0).A < 255 ||
                        x.GetPixel(x.Width - 1, 0).A < 255 ||
                        x.GetPixel(0, x.Height - 1).A < 255 ||
                        x.GetPixel(x.Width - 1, x.Height - 1).A < 255)
                    {
                        returnVal.HasTransparency = true;
                    }

                    // Sample random pixels and see if any are transparent
                    Random rand = new Random();
                    for (int c = 0; c < 100; c++)
                    {
                        Color sample = x.GetPixel(rand.Next(0, x.Width - 1), rand.Next(0, x.Height - 1));
                        if (sample.A < 255)
                        {
                            returnVal.HasTransparency = true;
                            break;
                        }
                    }

                    // Check its compression level in bits per pixel
                    returnVal.CompressedBitsPerPixel = (float)inputFile.Length / ((float)x.Width * x.Height) / 8;
                    return returnVal;
                }
                finally
                {
                    x.Dispose();
                }
            }
        }

        private static ImageFormat GuessFormatFromFileExtension(string extension)
        {
            if (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase))
            {
                return ImageFormat.Jpeg;
            }
            else if (extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                return ImageFormat.Jpeg;
            }
            else if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                return ImageFormat.Png;
            }
            else if (extension.Equals(".gif", StringComparison.OrdinalIgnoreCase))
            {
                return ImageFormat.Gif;
            }
            else
            {
                return ImageFormat.Unknown;
            }
        }
    }
}
