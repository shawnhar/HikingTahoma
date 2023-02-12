using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;

namespace builder
{
    class WebsiteBuilder
    {
        string[] whatToBuild = { };


        bool WantToBuild(string name)
        {
            return whatToBuild.Contains(name) || !whatToBuild.Any();
        }


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

            // Are there instructions indicating to only build a subset of the trails?
            var buildTxt = Path.Combine(cacheFolder.Path, "build.txt");
            
            if (File.Exists(buildTxt))
            {
                var separators = new char[] { ' ', '\r', '\n' };

                whatToBuild = File.ReadAllText(buildTxt).Split(separators, StringSplitOptions.RemoveEmptyEntries);
            }

            using (new Profiler("WebsiteBuilder.Build"))
            {
                // Load the master map. 
                var imageProcessor = new ImageProcessor();

                await imageProcessor.Initialize(sourceFolder.Path);

                // Process all the hikes.
                var hikes = (await sourceFolder.GetFoldersAsync())
                            .Where(folder => Path.GetFileName(folder.Path) != "Animals" &&
                                             Path.GetFileName(folder.Path) != "Overlays" &&
                                             Path.GetFileName(folder.Path) != "Photos")
                            .Select(folder => new Hike(folder.Path, imageProcessor))
                            .ToList();

                foreach (var hike in hikes)
                {
                    bool loadMap = WantToBuild(hike.FolderName) || WantToBuild("map");

                    await hike.Load(loadMap);
                }

                if (WantToBuild("map"))
                {
                    var doneHikes = hikes.Where(hike => !hike.IsFuture && !hike.IsNever).ToList();

                    var progress = await imageProcessor.MeasureProgressTowardGoal(doneHikes, sourceFolder.Path, outFolder.Path);

                    // Generate the index page.
                    WriteIndex(outFolder.Path, hikes, progress.DistanceHiked, progress.CompletionRatio, imageProcessor);

                    await imageProcessor.WriteMasterMap(outFolder.Path, doneHikes);
                }

                // Write the individual hike pages and photos.
                foreach (var hike in hikes.Where(hike => !hike.IsHidden && WantToBuild(hike.FolderName)))
                {
                    await hike.WriteOutput(hikes, outFolder.Path);
                }

                // Write additional photos and whatnot.
                await ProcessArticle(sourceFolder.Path, outFolder.Path, hikes, "AboutRainier");
                await ProcessArticle(sourceFolder.Path, outFolder.Path, hikes, "AboutThisSite");
                await ProcessArticle(sourceFolder.Path, outFolder.Path, hikes, "FuturePlans");
                await ProcessArticle(sourceFolder.Path, outFolder.Path, hikes, "WhereToStart");

                ProcessStandaloneHtml(sourceFolder.Path, outFolder.Path,
                    "Planner.html",
                    "Wonderland Itinerary Planner",
                    "Shows distance and elevation change between your choice of Mount Rainier trailheads and campsites.");

                if (WantToBuild("photos"))
                {
                    await ProcessAnimals(sourceFolder.Path, outFolder.Path, imageProcessor);
                    await ProcessExtraPhotos(sourceFolder.Path, outFolder.Path, imageProcessor);
                }

                if (WantToBuild("overlays"))
                {
                    await ProcessMapOverlays(sourceFolder.Path, outFolder.Path, imageProcessor);
                }

                // Copy root files.
                CopyFile(sourceFolder.Path, outFolder.Path, "style.css");
                CopyFile(sourceFolder.Path, outFolder.Path, "siteicon.png");
                CopyFile(sourceFolder.Path, outFolder.Path, "siteicon-192x192.png");
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

                imageProcessor.ThrowIfBadAspectRatios();
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

                var doneHikes = sortedHikes.Where(hike => !hike.IsNever);

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

                    foreach (var hike in sortedHikes)
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
                    writer.WriteLine("        <option value=\"first-hiked\">By Year Hiked</option>");
                    writer.WriteLine("      </select>");
                    writer.WriteLine("    </div>");

                    // Trail names.
                    writer.WriteLine("    <div class=\"hikelist\">");
                    writer.WriteLine("      <div class=\"multicolumn\">");

                    var difficulties = new Dictionary<string, string>();
                    var regions = new Dictionary<string, string>();
                    var years = new Dictionary<string, string>();

                    foreach (var hike in sortedHikes)
                    {
                        var difficulty = hike.DifficultyCategory;
                        var region = hike.Region;
                        var firstHiked = hike.FirstHiked;

                        var firstHikedKey = firstHiked.Replace("older", "0lder")
                                                      .Replace("never", "znever");

                        difficulties[difficulty.Item1] = difficulty.Item2;
                        regions[region] = region;
                        years[firstHikedKey] = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(firstHiked);

                        var oneWay = hike.IsOneWay ? " data-one-way=\"true\"" : "";

                        var eventHandler = string.Format(" onMouseEnter=\"OnEnterLink(document, '{0}')\" onMouseLeave=\"OnLeaveLink(document, '{0}')\"", hike.FolderName);

                        writer.WriteLine("        <div id=\"link-{0}\" data-region=\"{2}\" data-difficulty=\"{3}\" data-length=\"{4}\" data-elevation-gain=\"{5}\" data-max-elevation=\"{6}\" data-first-hiked=\"{7}\"{8}{9}><a href=\"{0}/\">{1}</a></div>", hike.FolderName, hike.HikeName, region, difficulty.Item1, hike.Distance, float.Parse(hike.ElevationGain), hike.MaxElevation, firstHikedKey, oneWay, eventHandler);
                    }

                    // Category headings.
                    foreach (var difficulty in difficulties.OrderBy(pair => pair.Key))
                    {
                        writer.WriteLine("        <div class=\"listhead\" data-difficulty=\"{0}\">{1}</div>", difficulty.Key, difficulty.Value);
                    }

                    foreach (var region in regions.OrderBy(pair => pair.Key))
                    {
                        writer.WriteLine("        <div class=\"listhead\" data-region=\"{0}\">{1}</div>", region.Key, region.Value);
                    }

                    foreach (var year in years.OrderBy(pair => pair.Key))
                    {
                        writer.WriteLine("        <div class=\"listhead\" data-first-hiked=\"{0}\">{1}</div>", year.Key, year.Value);
                    }

                    writer.WriteLine("      </div>");
                    writer.WriteLine("      <table>");
                    writer.WriteLine("      </table>");
                    writer.WriteLine("    </div>");

                    var label = (completionRatio < 0.999) ? "Trails hiked so far" : "Trails hiked";

                    writer.WriteLine("    <span class=\"progress\" onMouseEnter=\"OnEnterLink(document, 'todo')\" onMouseLeave=\"OnLeaveLink(document, 'todo')\">{0}: {1:0.#}% ({2:0.#} miles)</span>", label, completionRatio * 100, distanceHiked);

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


        async Task ProcessArticle(string sourcePath, string outPath, List<Hike> hikes, string filename)
        {
            var article = new Article(sourcePath + "/", filename + ".txt", filename + ".html");

            await article.WriteOutput(hikes, outPath);
        }


        void ProcessStandaloneHtml(string sourcePath, string outPath, string filename, string title, string description)
        {
            using (var file = File.OpenWrite(Path.Combine(outPath, filename)))
            using (var writer = new StreamWriter(file))
            {
                WebsiteBuilder.WriteHtmlHeader(writer, title, "./", description);

                foreach (var line in File.ReadAllLines(Path.Combine(sourcePath, filename)))
                {
                    writer.WriteLine("    {0}", line);
                }

                writer.WriteLine("  </body>");
                writer.WriteLine("</html>");
            }
        }


        async Task ProcessAnimals(string sourcePath, string outPath, ImageProcessor imageProcessor)
        {
            using (new Profiler("WebsiteBuilder.ProcessAnimals"))
            {
                sourcePath = Path.Combine(sourcePath, "Animals");
                outPath = Path.Combine(outPath, "Animals");

                Directory.CreateDirectory(outPath);

                foreach (var animalInfo in Directory.GetFiles(sourcePath, "*.txt").Select(Path.GetFileName))
                {
                    // Parse the animal description.
                    var lines = File.ReadAllLines(Path.Combine(sourcePath, animalInfo));

                    var animalName = lines[0];

                    var photos = lines.Where(line => line.StartsWith("["))
                                      .Select(line => line.TrimStart('[').TrimEnd(']'))
                                      .ToList();

                    var remainder = lines.Skip(1)
                                         .SkipWhile(string.IsNullOrEmpty)
                                         .Where(line => !line.StartsWith("["));

                    var animalDescription = new List<string>();

                    while (remainder.Any())
                    {
                        var descs = remainder.TakeWhile(line => !string.IsNullOrEmpty(line));

                        animalDescription.Add(string.Join(' ', descs.Select(s => s.Trim())));

                        remainder = remainder.Skip(descs.Count())
                                             .SkipWhile(string.IsNullOrEmpty);
                    }

                    // Convert the animal photo(s).
                    photos.Insert(0, Path.GetFileNameWithoutExtension(animalInfo) + ".jpg");

                    for (int i = 0; i < photos.Count; i++)
                    {
                        if (!photos[i].StartsWith(".."))
                        {
                            var photo = new Photo(photos[i], string.Empty);

                            await imageProcessor.WritePhoto(photo, sourcePath, outPath, generateThumbnail: i == 0);
                        }
                    }

                    // Write the HTML info page.
                    using (var file = File.OpenWrite(Path.Combine(outPath, Path.GetFileNameWithoutExtension(animalInfo) + ".html")))
                    using (var writer = new StreamWriter(file))
                    {
                        WebsiteBuilder.WriteHtmlHeader(writer, animalName, "../");

                        writer.WriteLine("    <div class=\"fixedwidth\">");
                        writer.WriteLine("      <p class=\"heading\">{0}</p>", animalName);

                        writer.WriteLine("      <div class=\"description\">");

                        foreach (var line in animalDescription)
                        {
                            writer.WriteLine("        <p>{0}</p>", line);
                        }

                        writer.WriteLine("        <div class=\"animalphotos\">");

                        foreach (var photoFilename in photos)
                        {
                            writer.WriteLine("          <img src=\"{0}\" />", photoFilename);
                        }

                        writer.WriteLine("        </div>");

                        writer.WriteLine("      </div>");
                        writer.WriteLine("    </div>");
                        writer.WriteLine("  </body>");
                        writer.WriteLine("</html>");
                    }
                }
            }
        }


        async Task ProcessExtraPhotos(string sourcePath, string outPath, ImageProcessor imageProcessor)
        {
            using (new Profiler("WebsiteBuilder.ProcessExtraPhotos"))
            {
                sourcePath = Path.Combine(sourcePath, "Photos");
                outPath = Path.Combine(outPath, "Photos");

                Directory.CreateDirectory(outPath);

                foreach (var filename in Directory.GetFiles(sourcePath, "*.jpg").Select(Path.GetFileName))
                {
                    var photo = new Photo(filename, string.Empty);

                    bool thumbnailOnly = filename.Contains("-.jpg");

                    await imageProcessor.WritePhoto(photo, sourcePath, outPath, true, !thumbnailOnly);
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
