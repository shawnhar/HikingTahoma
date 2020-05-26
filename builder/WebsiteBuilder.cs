using System;
using System.Collections;
using System.Collections.Generic;
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

            // Generate the index page.
            WriteIndex(outFolder.Path, hikes);

            await imageProcessor.WriteMasterMap(outFolder.Path, hikes);

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


        void WriteIndex(string outPath, List<Hike> hikes)
        {
            using (var file = File.OpenWrite(Path.Combine(outPath, "index.html")))
            using (var writer = new StreamWriter(file))
            {
                WebsiteBuilder.WriteHtmlHeader(writer, "Index", "./");

                writer.WriteLine("<div class=\"map\">");
                writer.WriteLine("  <img src=\"map.jpg\" />");

                foreach (var hike in hikes)
                {
                    writer.WriteLine("  <img class=\"maplayer\" id=\"hike-{0}\" src=\"{0}/{1}\" />", hike.FolderName, hike.OverlayName);
                }

                writer.WriteLine("</div>");

                writer.WriteLine("<ul class=\"hikelist\">");

                foreach (var hike in hikes)
                {
                    writer.WriteLine("  <li onMouseOver=\" document.getElementById('hike-{0}').style.visibility = 'visible'\" onMouseOut=\"document.getElementById('hike-{0}').style.visibility = 'hidden'\"><a href=\"{0}/{0}.html\">{1}</a></li>", hike.FolderName, hike.HikeName);
                }

                writer.WriteLine("</ul>");

                writer.WriteLine("</body>");
                writer.WriteLine("</html>");
            }
        }


        public static void WriteHtmlHeader(StreamWriter writer, string title, string rootPrefix)
        {
            writer.WriteLine("<html>");

            writer.WriteLine("<title> Hiking Tahoma: {0}</title>", title);

            writer.WriteLine("<head>");
            writer.WriteLine("  <link rel=\"stylesheet\" href=\"" + rootPrefix + "style.css\">");
            writer.WriteLine("</head>");

            writer.WriteLine("<body>");

            writer.WriteLine("<div class=\"title\">");

            writer.WriteLine("  <div class=\"about\">");
            writer.WriteLine("    <a href = \"" + rootPrefix + "AboutRainier.html\">about Mount Rainier</a><br>");
            writer.WriteLine("    <a href = \"" + rootPrefix + "AboutThisSite.html\">about this site</a><br>");
            writer.WriteLine("    <a href = \"" + rootPrefix + "FuturePlans.html\">future plans</a>");
            writer.WriteLine("  </div>");
            writer.WriteLine("  <div class=\"backlink\"><a href = \"" + rootPrefix + "index.html\">Hiking Tahoma</a></div>");
            writer.WriteLine("</div>");
        }
    }
}
