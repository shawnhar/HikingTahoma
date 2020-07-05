using System;
using System.Collections.Generic;
using System.Diagnostics;
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
                await hike.Load();
            }

            foreach (var hike in hikes)
            {
                await hike.WriteOutput(hikes, outFolder.Path);
            }

            var progress = await imageProcessor.MeasureProgressTowardGoal(hikes, sourceFolder.Path, outFolder.Path);

            // Generate the index page.
            WriteIndex(outFolder.Path, hikes, imageProcessor.MapWidth / 2, imageProcessor.MapHeight / 2, progress.DistanceHiked, progress.CompletionRatio);

            await imageProcessor.WriteMasterMap(outFolder.Path, hikes);

            // Copy root files.
            CopyFile(sourceFolder.Path, outFolder.Path, "style.css");
            CopyFile(sourceFolder.Path, outFolder.Path, "AboutRainier.html");
            CopyFile(sourceFolder.Path, outFolder.Path, "AboutThisSite.html");
            CopyFile(sourceFolder.Path, outFolder.Path, "FuturePlans.html");
            CopyFile(sourceFolder.Path, outFolder.Path, "me.png");

            // Debug .csv output can be pasted into Excel for length/difficulty analysis.
            PrintHikeLengthsAndDifficulties(hikes);

            // Combine all the text for all the hikes, so a spell check can easily be run over the whole thing.
            LogHikeTextForSpellCheck(outFolder.Path, hikes);

            return outFolder.Path;
        }


        void CopyFile(string sourcePath, string outPath, string name)
        {
            File.Copy(Path.Combine(sourcePath, name),
                      Path.Combine(outPath, name));
        }


        void WriteIndex(string outPath, List<Hike> hikes, int mapW, int mapH, float distanceHiked, float completionRatio)
        {
            var sortedHikes = hikes.OrderBy(hike => hike.HikeName);

            using (var file = File.OpenWrite(Path.Combine(outPath, "index.html")))
            using (var writer = new StreamWriter(file))
            {
                WebsiteBuilder.WriteHtmlHeader(writer, "Documenting my Rainier obsession", "./");

                // Trails map.
                writer.WriteLine("    <div class=\"map\">");
                writer.WriteLine("      <img class=\"mapbase\" src=\"map.png\" width=\"{0}\" height=\"{1}\" />", mapW, mapH);

                foreach (var hike in sortedHikes)
                {
                    writer.WriteLine("      <img class=\"maplayer\" id=\"hike-{0}\" src=\"{0}/{1}\" width=\"{2}\" height=\"{3}\" />", hike.FolderName, hike.OverlayName, mapW, mapH);
                }

                writer.WriteLine("      <img class=\"maplayer\" id=\"todo\" src=\"todo.png\" width=\"600\" height=\"506\" />");
                writer.WriteLine("    </div>");

                // Trail names.
                writer.WriteLine("    <ul class=\"hikelist\">");

                foreach (var hike in sortedHikes)
                {
                    writer.WriteLine("      <li onMouseOver=\"document.getElementById('hike-{0}').style.visibility = 'visible'\" onMouseOut=\"document.getElementById('hike-{0}').style.visibility = 'hidden'\"><a href=\"{0}/{0}.html\">{1}</a></li>", hike.FolderName, hike.HikeName);
                }

                writer.WriteLine("    </ul>");

                writer.WriteLine("    <p class=\"progress\" onMouseOver=\"document.getElementById('todo').style.visibility = 'visible'\" onMouseOut=\"document.getElementById('todo').style.visibility = 'hidden'\">Trails hiked so far: {0:0.0}% ({1:0.0} miles)</p>", completionRatio * 100, distanceHiked);

                writer.WriteLine("  </body>");
                writer.WriteLine("</html>");
            }
        }


        public static void WriteHtmlHeader(StreamWriter writer, string title, string rootPrefix)
        {
            writer.WriteLine("<html>");

            writer.WriteLine("  <head>");
            writer.WriteLine("    <title>Hiking Tahoma: {0}</title>", title);
            writer.WriteLine("    <link rel=\"stylesheet\" href=\"" + rootPrefix + "style.css\">");
            writer.WriteLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
            writer.WriteLine("  </head>");

            writer.WriteLine("  <body>");

            writer.WriteLine("    <div class=\"fixedwidth\">");
            writer.WriteLine("      <div class=\"title\">");
            writer.WriteLine("        <div class=\"about\">");
            writer.WriteLine("          <a href = \"" + rootPrefix + "AboutRainier.html\">about Mount Rainier</a><br/>");
            writer.WriteLine("          <a href = \"" + rootPrefix + "AboutThisSite.html\">about this site</a><br/>");
            writer.WriteLine("          <a href = \"" + rootPrefix + "FuturePlans.html\">future plans</a>");
            writer.WriteLine("        </div>");
            writer.WriteLine("        <div class=\"backlink\"><a href = \"" + rootPrefix + "index.html\">Hiking Tahoma</a></div>");
            writer.WriteLine("        <div class=\"subtitle\">Documenting my Rainier obsession</div>");
            writer.WriteLine("      </div>");
            writer.WriteLine("    </div>");
        }


        void PrintHikeLengthsAndDifficulties(IEnumerable<Hike> hikes)
        {
#if false
            foreach (var hike in hikes.OrderBy(hike => hike.HikeName))
            {
                Debug.WriteLine("{0},{1},{2},{3}", hike.HikeName, hike.Distance, hike.ElevationGain, hike.Difficulty);
            }
#endif
        }


        void LogHikeTextForSpellCheck(string outPath, IEnumerable<Hike> hikes)
        {
#if false
            var allText = hikes.SelectMany(hike => hike.Descriptions.Concat(hike.Photos.Select(photo => photo.Description)));

            File.WriteAllLines(Path.Combine(outPath, "spell.txt"), allText);
#endif
        }
    }
}
