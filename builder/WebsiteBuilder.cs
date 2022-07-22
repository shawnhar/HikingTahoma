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
        const bool processMap = true;
        readonly string[] processHikes = { };


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

            using (new Profiler("WebsiteBuilder.Build"))
            {
                // Load the master map. 
                var imageProcessor = new ImageProcessor();

                await imageProcessor.Initialize(sourceFolder.Path);

                // Process all the hikes.
                var hikes = (await sourceFolder.GetFoldersAsync())
                            .Where(folder => Path.GetFileName(folder.Path) != "Animals" && 
                                             Path.GetFileName(folder.Path) != "Tracy" && 
                                             Path.GetFileName(folder.Path) != "Overlays")
                            .Select(folder => new Hike(folder.Path, imageProcessor))
                            .ToList();

                foreach (var hike in hikes)
                {
                    await hike.Load();
                }

                if (processMap)
                {
                    var doneHikes = hikes.Where(hike => !hike.IsNotHiked).ToList();

                    var progress = await imageProcessor.MeasureProgressTowardGoal(doneHikes, sourceFolder.Path, outFolder.Path);

                    // Generate the index page.
                    WriteIndex(outFolder.Path, hikes, progress.DistanceHiked, progress.CompletionRatio, imageProcessor);

                    await imageProcessor.WriteMasterMap(outFolder.Path, doneHikes);
                }

                // Process the individual hikes and photos.
                foreach (var hike in hikes.Where(hike => !hike.IsHidden))
                {
                    if (processHikes.Length == 0 || processHikes.Contains(hike.FolderName))
                    {
                        await hike.WriteOutput(hikes, outFolder.Path);
                    }
                }

                if (processHikes.Length == 0)
                {
                    await ProcessExtraPhotos("Animals", sourceFolder.Path, outFolder.Path, imageProcessor);
                    await ProcessExtraPhotos("Tracy", sourceFolder.Path, outFolder.Path, imageProcessor);

                    // Generate map overlay images for the distances planning tool.
                    await ProcessMapOverlays(sourceFolder.Path, outFolder.Path, imageProcessor);
                }

                // Copy root files.
                CopyFile(sourceFolder.Path, outFolder.Path, "style.css");
                CopyFile(sourceFolder.Path, outFolder.Path, "siteicon.png");
                CopyFile(sourceFolder.Path, outFolder.Path, "siteicon-192x192.png");
                CopyFile(sourceFolder.Path, outFolder.Path, "AboutRainier.html");
                CopyFile(sourceFolder.Path, outFolder.Path, "AboutThisSite.html");
                CopyFile(sourceFolder.Path, outFolder.Path, "WhereToStart.html");
                CopyFile(sourceFolder.Path, outFolder.Path, "FuturePlans.html");
                CopyFile(sourceFolder.Path, outFolder.Path, "Planner.html");
                CopyFile(sourceFolder.Path, outFolder.Path, "Planner.js");
                CopyFile(sourceFolder.Path, outFolder.Path, "index.js");
                CopyFile(sourceFolder.Path, outFolder.Path, "mapbase.jpg");
                CopyFile(sourceFolder.Path, outFolder.Path, "me.png");
                CopyFile(sourceFolder.Path, outFolder.Path, "bucket.png");
                CopyFile(sourceFolder.Path, outFolder.Path, ".htaccess");

#if false
                // Debug .csv output can be pasted into Excel for length/difficulty analysis.
                PrintHikeLengthsAndDifficulties(hikes);

                // Combine all the text for all the hikes, so a spell check can easily be run over the whole thing.
                LogHikeTextForSpellCheck(outFolder.Path, hikes);
#endif
            }

            Profiler.OutputResults();

            return outFolder.Path;
        }


        void CopyFile(string sourcePath, string outPath, string name)
        {
            File.Copy(Path.Combine(sourcePath, name),
                      Path.Combine(outPath, name));
        }


        void WriteIndex(string outPath, List<Hike> hikes, float distanceHiked, float completionRatio, ImageProcessor imageProcessor)
        {
            using (new Profiler("WebsiteBuilder.WriteIndex"))
            {
                var sortedHikes = hikes.Where(hike => !hike.IsHidden)
                                       .OrderBy(hike => hike.HikeName);

                var doneHikes = sortedHikes.Where(hike => !hike.IsNotHiked);
                var notFutureHikes = sortedHikes.Where(hike => !hike.IsFuture);

                using (var file = File.OpenWrite(Path.Combine(outPath, "index.html")))
                using (var writer = new StreamWriter(file))
                {
                    const string title = "Documenting my Rainier obsession";
                    const string description = "Mount Rainier trail descriptions, photos, and Wonderland itinerary planner.";

                    WebsiteBuilder.WriteHtmlHeader(writer, title, "./", description);

                    // Trails map.
                    var imgSize = string.Format("width=\"{0}\" height=\"{1}\"", imageProcessor.MapWidth / 2, imageProcessor.MapHeight / 2);

                    writer.WriteLine("    <div class=\"map\">");
                    writer.WriteLine("      <img class=\"mapbase\" src=\"map.jpg\" {0} />", imgSize);

                    foreach (var hike in notFutureHikes)
                    {
                        writer.WriteLine("      <img class=\"maplayer\" id=\"hike-{0}\" src=\"{0}/{1}\" {2} />", hike.FolderName, hike.OverlayName, imgSize);
                    }

                    writer.WriteLine("      <img class=\"maplayer\" id=\"hike-todo\" src=\"todo.png\" {0} />", imgSize);

                    // Overlay clickable and focusable regions to create an image map.
                    var imageMap = new ImageMap(doneHikes);

                    imageMap.Write(writer);

                    writer.WriteLine("    </div>");

                    writer.WriteLine("    <div style=\"position:relative\">");
                    writer.WriteLine("      <select id=\"sortselector\" onChange=\"SortSelectorChanged(this.value)\">");
                    writer.WriteLine("        <option value=\"alphabetical\">Alphabetical</option>");
                    writer.WriteLine("        <option value=\"region\">By Region</option>");
                    writer.WriteLine("        <option value=\"difficulty\">By Difficulty</option>");
                    writer.WriteLine("        <option value=\"length\">By Length</option>");
                    writer.WriteLine("        <option value=\"elevation-gain\">By Elevation Gain</option>");
                    writer.WriteLine("        <option value=\"max-elevation\">By Max Elevation</option>");
                    writer.WriteLine("        <option value=\"steepness\">By Steepness</option>");
                    writer.WriteLine("      </select>");
                    writer.WriteLine("    </div>");

                    // Trail names.
                    writer.WriteLine("    <div class=\"hikelist\">");
                    writer.WriteLine("      <div class=\"multicolumn\">");

                    var difficulties = new Dictionary<string, string>();
                    var regions = new Dictionary<string, string>();

                    foreach (var hike in sortedHikes)
                    {
                        var difficulty = hike.DifficultyCategory;
                        var region = hike.Region;

                        difficulties[difficulty.Item1] = difficulty.Item2;
                        regions[region] = region;

                        var eventHandler = hike.IsFuture ? "" : string.Format(" onMouseEnter=\"OnEnterLink(document, '{0}')\" onMouseLeave=\"OnLeaveLink(document, '{0}')\"", hike.FolderName);

                        writer.WriteLine("        <div id=\"link-{0}\" data-region=\"{2}\" data-difficulty=\"{3}\" data-length=\"{4}\" data-elevation-gain=\"{5}\" data-max-elevation=\"{6}\"{7}><a href=\"{0}/\">{1}</a></div>", hike.FolderName, hike.HikeName, region, difficulty.Item1, hike.Distance, float.Parse(hike.ElevationGain), hike.MaxElevation, eventHandler);
                    }

                    // Category headings.
                    foreach (var difficulty in difficulties)
                    {
                        writer.WriteLine("        <div class=\"listhead\" data-difficulty=\"{0}\">{1}</div>", difficulty.Key, difficulty.Value);
                    }

                    foreach (var region in regions)
                    {
                        writer.WriteLine("        <div class=\"listhead\" data-region=\"{0}\">{1}</div>", region.Key, region.Value);
                    }

                    writer.WriteLine("      </div>");
                    writer.WriteLine("      <table>");
                    writer.WriteLine("      </table>");
                    writer.WriteLine("    </div>");

                    writer.WriteLine("    <span class=\"progress\" onMouseEnter=\"OnEnterLink(document, 'todo')\" onMouseLeave=\"OnLeaveLink(document, 'todo')\">Trails hiked so far: {0:0.0}% ({1:0.0} miles)</span>", completionRatio * 100, distanceHiked);

                    writer.WriteLine("    <script src=\"index.js\"></script>");

                    writer.WriteLine("  </body>");
                    writer.WriteLine("</html>");
                }
            }
        }


        public static void WriteHtmlHeader(StreamWriter writer, string title, string rootPrefix, string description = null)
        {
            writer.WriteLine("<html>");

            writer.WriteLine("  <head>");
            writer.WriteLine("    <title>Hiking Tahoma: {0}</title>", title);
            writer.WriteLine("    <link rel=\"stylesheet\" href=\"" + rootPrefix + "style.css\"/>");
            writer.WriteLine("    <link rel=\"icon\" type=\"image/png\" href=\"" + rootPrefix + "siteicon.png\"/>");
            writer.WriteLine("    <link rel=\"apple-touch-icon\" href=\"" + rootPrefix + "siteicon-192x192.png\"/>");
            writer.WriteLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"/>");

            if (!string.IsNullOrEmpty(description))
            {
                writer.WriteLine("    <meta name=\"description\" content=\"{0}\"/>", description);
            }

            writer.WriteLine("  </head>");

            writer.WriteLine("  <body>");

            writer.WriteLine("    <div class=\"fixedwidth\">");
            writer.WriteLine("      <table class=\"title\">");
            writer.WriteLine("        <tr>");
            writer.WriteLine("          <td>");
            writer.WriteLine("            <div class=\"backlink\"><a href=\"" + rootPrefix + "\">Hiking Tahoma</a></div>");
            writer.WriteLine("            <div class=\"subtitle\">Documenting my Rainier obsession</div>");
            writer.WriteLine("          </td>");
            writer.WriteLine("          <td>");
            writer.WriteLine("            <table class=\"about\">");
            writer.WriteLine("              <tr>");
            writer.WriteLine("                <td><a href=\"" + rootPrefix + "AboutRainier.html\">about Mount Rainier</a></td>");
            writer.WriteLine("                <td><a href=\"" + rootPrefix + "WhereToStart.html\">where to start</a></td>");
            writer.WriteLine("              </tr>");
            writer.WriteLine("              <tr>");
            writer.WriteLine("                <td><a href=\"" + rootPrefix + "AboutThisSite.html\">about this site</a></td>");
            writer.WriteLine("                <td><a href=\"" + rootPrefix + "FuturePlans.html\">future plans</a></td>");
            writer.WriteLine("              </tr>");
            writer.WriteLine("              <tr>");
            writer.WriteLine("                <td><a href=\"" + rootPrefix + "Planner.html\">itinerary planner</a></td>");
            writer.WriteLine("                <td><a href=\"" + rootPrefix + "Wonderland/How.html\">permits</a></td>");
            writer.WriteLine("              </tr>");
            writer.WriteLine("            </table>");
            writer.WriteLine("          </td>");
            writer.WriteLine("        </tr>");
            writer.WriteLine("      </table>");
            writer.WriteLine("    </div>");
        }


        async Task ProcessExtraPhotos(string folderName, string sourcePath, string outPath, ImageProcessor imageProcessor)
        {
            using (new Profiler("WebsiteBuilder.ProcessExtraPhotos"))
            {
                sourcePath = Path.Combine(sourcePath, folderName);
                outPath = Path.Combine(outPath, folderName);

                Directory.CreateDirectory(outPath);

                foreach (var filename in Directory.GetFiles(sourcePath, "*.jpg").Select(Path.GetFileName))
                {
                    var photo = new Photo(filename, string.Empty);

                    await imageProcessor.WritePhoto(photo, sourcePath, outPath);
                }
            }
        }


        async Task ProcessMapOverlays(string sourcePath, string outPath, ImageProcessor imageProcessor)
        {
            using (new Profiler("WebsiteBuilder.ProcessMapOverlays"))
            {
                sourcePath = Path.Combine(sourcePath, "Overlays");
                outPath = Path.Combine(outPath, "Overlays");

                Directory.CreateDirectory(outPath);

                foreach (var filename in Directory.GetFiles(sourcePath, "*.png").Select(Path.GetFileName))
                {
                    var bitmap = await imageProcessor.LoadImage(sourcePath, filename);

                    bitmap = imageProcessor.ProcessMapOverlay(bitmap);

                    await imageProcessor.SaveImage(bitmap, outPath, filename);
                }
            }
        }


        void PrintHikeLengthsAndDifficulties(IEnumerable<Hike> hikes)
        {
            foreach (var hike in hikes.OrderBy(hike => hike.HikeName))
            {
                Debug.WriteLine("{0},{1},{2},{3}", hike.HikeName, hike.Distance, hike.ElevationGain, hike.Difficulty);
            }
        }


        void LogHikeTextForSpellCheck(string outPath, IEnumerable<Hike> hikes)
        {
            var allText = hikes.SelectMany(hike => hike.Sections.SelectMany(section => section.Descriptions.Concat(section.Photos.Select(photo => photo.Description))));

            File.WriteAllLines(Path.Combine(outPath, "spell.txt"), allText);
        }
    }
}
