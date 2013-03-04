using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ICSharpCode.SharpZipLib.Zip;

namespace Manson.AutoUpdater
{
    public static class DirectoryCompression
    {

        public static void CompressDirectory(this Stream target, string sourcePath,

                                             Func<string, bool> excludeFromCompression)
        {

            sourcePath = Path.GetFullPath(sourcePath);



            string parentDirectory = Path.GetDirectoryName(sourcePath);



            int trimOffset = (string.IsNullOrEmpty(parentDirectory)

                                  ? Path.GetPathRoot(sourcePath).Length

                                  : parentDirectory.Length);





            List<string> fileSystemEntries = new List<string>();



            fileSystemEntries

                .AddRange(Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories)

                                   .Select(d => d + "\\"));



            fileSystemEntries

                .AddRange(Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories));





            using (ZipOutputStream compressor = new ZipOutputStream(target))
            {

                compressor.SetLevel(9);



                foreach (string filePath in fileSystemEntries)
                {

                    if (excludeFromCompression(filePath))
                    {

                        continue;

                    }



                    compressor.PutNextEntry(new ZipEntry(filePath.Substring(trimOffset)));



                    if (filePath.EndsWith(@"\"))
                    {

                        continue;

                    }



                    byte[] data = new byte[2048];



                    using (FileStream input = File.OpenRead(filePath))
                    {

                        int bytesRead;



                        while ((bytesRead = input.Read(data, 0, data.Length)) > 0)
                        {

                            compressor.Write(data, 0, bytesRead);

                        }

                    }

                }



                compressor.Finish();

            }

        }





        public static void DecompressToDirectory(this Stream source, string targetPath, string pwd,

                                                 Func<string, bool> excludeFromDecompression)
        {

            targetPath = Path.GetFullPath(targetPath);



            using (ZipInputStream decompressor = new ZipInputStream(source))
            {

                if (!string.IsNullOrEmpty(pwd))
                {

                    decompressor.Password = pwd;

                }



                ZipEntry entry;



                while ((entry = decompressor.GetNextEntry()) != null)
                {

                    if (excludeFromDecompression(entry.Name))
                    {

                        continue;

                    }



                    string filePath = Path.Combine(targetPath, entry.Name);



                    string directoryPath = Path.GetDirectoryName(filePath);





                    if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                    {

                        Directory.CreateDirectory(directoryPath);

                    }



                    if (entry.IsDirectory)
                    {

                        continue;

                    }



                    byte[] data = new byte[2048];

                    using (FileStream streamWriter = File.Create(filePath))
                    {

                        int bytesRead;

                        while ((bytesRead = decompressor.Read(data, 0, data.Length)) > 0)
                        {

                            streamWriter.Write(data, 0, bytesRead);

                        }

                    }

                }

            }

        }

    }
}
