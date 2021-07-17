using Durandal.Common.File;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YerfTagger.Imaging
{
    public interface IImageConverter
    {
        FileInfo ConvertImageFile(FileInfo inputFile, ImageConversionParameters parameters);
    }
}
