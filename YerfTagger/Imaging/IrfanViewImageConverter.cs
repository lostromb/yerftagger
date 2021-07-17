using Durandal.Common.File;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YerfTagger.Imaging
{
    public class IrfanViewImageConverter : IImageConverter
    {
        private readonly string IrfanViewExePath;

        public IrfanViewImageConverter(string iviewExePath)
        {
            IrfanViewExePath = iviewExePath; //@"C:\Program Files\IrfanView\i_view64.exe"
        }

        public FileInfo ConvertImageFile(FileInfo inputFile, ImageConversionParameters parameters)
        {
            if (parameters is JpegConversionParameters)
            {
                JpegConversionParameters convertedParams = parameters as JpegConversionParameters;
                string targetFileName = ConvertFileNameToJpg(inputFile);

                ProcessStartInfo irfanParams = new ProcessStartInfo(
                    IrfanViewExePath,
                    "\"" + inputFile.FullName + "\" /convert=\"" + targetFileName + "\" /jpgq=95 /silent");

                Process irfan = Process.Start(irfanParams);

                irfan.WaitForExit();

                if (irfan.ExitCode != 0)
                {
                    return null;
                }

                FileInfo outputFile = new FileInfo(targetFileName);
                CopyAttributes(inputFile, outputFile);
                return outputFile;
            }

            throw new NotImplementedException("Conversion to anything besides jpeg is not supported");
        }

        private static void CopyAttributes(FileInfo sourceFile, FileInfo targetFile)
        {
            sourceFile.Refresh();
            targetFile.CreationTime = sourceFile.CreationTime;
            targetFile.LastWriteTime = sourceFile.LastWriteTime;
            targetFile.LastAccessTime = sourceFile.LastAccessTime;
            targetFile.Refresh();
        }

        private static string ConvertFileNameToJpg(FileInfo file)
        {
            string rawFileName = file.Name.Substring(0, file.Name.LastIndexOf('.'));
            string returnVal = file.Directory.FullName + Path.DirectorySeparatorChar + rawFileName + ".jpg";
            int counter = 1;
            while (File.Exists(returnVal))
            {
                returnVal = file.Directory.FullName + Path.DirectorySeparatorChar + rawFileName + "_" + (counter++) + ".jpg";
            }

            return returnVal;
        }
    }
}
