using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;

namespace builder
{
    class WebsiteBuilder
    {
        public async Task<string> Build()
        {
            // Get paths.
            var cacheFolder = ApplicationData.Current.LocalCacheFolder;

            StorageFolder sourceFolder;

            try
            {
                sourceFolder = await cacheFolder.GetFolderAsync("source");
            }
            catch
            {
                throw new Exception("'source' folder not found in " + cacheFolder.Path);
            }

            StorageFolder outFolder = await cacheFolder.CreateFolderAsync("out", CreationCollisionOption.ReplaceExisting);

            // Load the master map. 
            var imageProcessor = new ImageProcessor();

            await imageProcessor.Initialize(sourceFolder.Path);

            // Process all the hikes.
            var hikes = (await sourceFolder.GetFoldersAsync())
                        .Select(folder => new Hike(folder.Path, imageProcessor))
                        .ToList();

            foreach (var hike in hikes)
            {
                await hike.Process(outFolder.Path);
            }

            // Copy root files.
            CopyFile(sourceFolder.Path, outFolder.Path, "style.css");
            CopyFile(sourceFolder.Path, outFolder.Path, "AboutRainier.html");
            CopyFile(sourceFolder.Path, outFolder.Path, "AboutThisSite.html");
            CopyFile(sourceFolder.Path, outFolder.Path, "FuturePlans.html");

            return outFolder.Path;
        }


        void CopyFile(string sourcePath, string outPath, string name)
        {
            File.Copy(Path.Combine(sourcePath, name), 
                      Path.Combine(outPath, name));
        }
    }
}
