using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace builder
{
    class Hike
    {
        public string HikeName      { get; private set; }
        public string Difficulty    { get; private set; }
        public string Distance      { get; private set; }
        public string ElevationGain { get; private set; }
        public string MaxElevation  { get; private set; }
        public string Region        { get; private set; }
        public string CampSites     { get; private set; }
        public string FirstHiked    { get; private set; }

        public readonly List<HikeSection> Sections = new List<HikeSection>();

        public string FolderName => Path.GetFileName(sourcePath);
        public string MapName => FolderName + "-map.jpg";
        public string MapThumbnail => FolderName + "-map-small.jpg";
        public string OverlayName => FolderName + "-overlay.png";
        public bool IsHidden => HikeName == "misc";
        public bool IsFuture => FirstHiked == "not yet!";
        public bool IsNever => FirstHiked == "never";
        public bool IsNotHiked => IsFuture || IsNever;
        public bool IsOffTop => FolderName == "WestForkWhiteRiver";
        public bool IsOffBottom => FolderName == "BackboneRidge";

        public readonly HikeMap Map;

        string sourcePath;

        ImageProcessor imageProcessor;


        public Tuple<string, string> DifficultyCategory
        {
            get
            {
                if (Difficulty.Contains("overnighter") || Difficulty.Contains("days"))
                {
                    return Tuple.Create("x", "Backpacking");
                }
                else if (Difficulty.Contains("easy"))
                {
                    return Tuple.Create("easy", "Easy");
                }
                else if (Difficulty.Contains("moderate"))
                {
                    return Tuple.Create("moderate", "Moderate");
                }
                else if (Difficulty.Contains("strenuous") || Difficulty.Contains("wtf"))
                {
                    return Tuple.Create("strenuous", "Strenuous");
                }
                else
                {
                    throw new Exception("Unknown difficulty category: " + Difficulty);
                }
            }
        }


        public Hike(string sourcePath, ImageProcessor imageProcessor)
        {
            this.sourcePath = sourcePath;
            this.imageProcessor = imageProcessor;

            Map = new HikeMap(imageProcessor, IsOffTop, IsOffBottom);
        }


        public async Task Load()
        {
            ParseReport();
            ValidatePhotos();

            await Map.Load(sourcePath, FirstHiked);
        }


        void ParseReport()
        {
            using (new Profiler("Hike.ParseReport"))
            {
                var lines = File.ReadAllLines(Path.Combine(sourcePath, "report.txt"));

                // First two lines are the hike name and difficulty.
                HikeName = lines[0];
                Difficulty = lines[1];

                // Third line is 'distance elevationGain maxElevation'
                var split = lines[2].Split(' ');

                Distance = split[0];
                ElevationGain = split[1];
                MaxElevation = split[2];

                // Fourth line is the region.
                Region = lines[3];

                // Fifth line is campsites along this trail.
                CampSites = lines[4];

                // Sixth line is when I first hiked it.
                FirstHiked = lines[5];

                var remainder = lines.Skip(6)
                                     .SkipWhile(string.IsNullOrEmpty);

                var titleRegex = new Regex("^#+ ");

                while (remainder.Any())
                {
                    var currentSection = new HikeSection();

                    Sections.Add(currentSection);

                    // Optional section title:
                    // "# Title" embeds the section within the current page.
                    // "## Title" outputs the section as a separate page.
                    var match = titleRegex.Match(remainder.First());

                    if (match.Success)
                    {
                        currentSection.SetTitle(remainder.First().Substring(match.Length).Trim(), HikeName);
                        currentSection.IsSeparatePage = match.Length > 2;

                        remainder = remainder.Skip(1)
                                             .SkipWhile(string.IsNullOrEmpty);
                    }

                    // Set of photos in the form: [filename.jpg] description.
                    currentSection.Photos.AddRange(remainder.TakeWhile(line => line.StartsWith('['))
                                                            .Select(ParsePhoto));

                    remainder = remainder.Skip(currentSection.Photos.Count)
                                         .SkipWhile(string.IsNullOrEmpty);

                    // Rest of the section is a text description;
                    while (remainder.Any() && !titleRegex.IsMatch(remainder.First()))
                    {
                        var descs = remainder.TakeWhile(line => !string.IsNullOrEmpty(line));

                        currentSection.Descriptions.Add(string.Join(' ', descs.Select(s => s.Trim())));

                        remainder = remainder.Skip(descs.Count())
                                             .SkipWhile(string.IsNullOrEmpty);
                    }
                }
            }
        }


        static Photo ParsePhoto(string line)
        {
            var i = line.IndexOf(']');

            string filename = line.Substring(1, i - 1);
            string description = line.Substring(i + 1).Trim();

            return new Photo(filename, description);
        }


        void ValidatePhotos()
        {
            using (new Profiler("Hike.ValidatePhotos"))
            {
                var photoNames = from section in Sections
                                 from photo in section.Photos
                                 select photo.Filename;

                var jpegs = Directory.GetFiles(sourcePath, "*.jpg").Select(Path.GetFileName);

                var missing = photoNames.Except(jpegs);

                if (missing.Any())
                {
                    throw new Exception("Missing photos in " + FolderName + ": " + string.Join(", ", missing));
                }

                var unreferenced = jpegs.Except(photoNames);

                if (unreferenced.Any())
                {
                    throw new Exception("Unreferenced photos in " + FolderName + ": " + string.Join(", ", unreferenced));
                }
            }
        }


        public async Task WriteOutput(IEnumerable<Hike> hikes, string dir)
        {
            using (new Profiler("Hike.WriteOutput"))
            {
                var outPath = Path.Combine(dir, FolderName);

                Directory.CreateDirectory(outPath);

                foreach (var photo in Sections.SelectMany(section => section.Photos))
                {
                    await WritePhoto(photo, outPath);
                }

                WriteHtml(hikes, outPath);

                await Map.WriteTrailMaps(outPath, MapName, MapThumbnail, IsNever);

                if (!IsFuture)
                {
                    await Map.WriteTrailOverlay(outPath, OverlayName, IsNever);
                }
            }
        }


        void WriteHtml(IEnumerable<Hike> hikes, string outPath)
        {
            using (new Profiler("Hike.WriteHtml"))
            {
                using (var file = File.OpenWrite(Path.Combine(outPath, "index.html")))
                using (var writer = new StreamWriter(file))
                {
                    WebsiteBuilder.WriteHtmlHeader(writer, HikeName, "../");

                    writer.WriteLine("    <div class=\"fixedwidth\">");

                    writer.WriteLine("      <table>");
                    writer.WriteLine("        <tr>");
                    writer.WriteLine("          <td>");
                    writer.WriteLine("            <a href=\"{0}\">", MapName);
                    writer.WriteLine("              <img class=\"hikemap\" src=\"{0}\" width=\"256\" height=\"256\" />", MapThumbnail);
                    writer.WriteLine("            </a>");
                    writer.WriteLine("          </td>");
                    writer.WriteLine("          <td class=\"stats\">");
                    writer.WriteLine("            <p class=\"hikename\">{0}</p>", HikeName);
                    writer.WriteLine("            <p class=\"detail\">Difficulty: {0}</p>", Difficulty);
                    writer.WriteLine("            <p class=\"detail\">{0} miles</p>", Distance);
                    writer.WriteLine("            <p class=\"detail\">Elevation gain: {0}'</p>", ElevationGain);
                    writer.WriteLine("            <p class=\"detail\">Max elevation: {0}'</p>", MaxElevation);
                    writer.WriteLine("            <p class=\"detail\">Camps: {0}</p>", CampSites);
                    writer.WriteLine("            <p class=\"detail\">First hiked by me: {0}</p>", FirstHiked);
                    writer.WriteLine("          </td>");
                    writer.WriteLine("        </tr>");
                    writer.WriteLine("      </table>");

                    IEnumerable<HikeSection> todo = Sections;

                    HikeSection prevSeparatePage = null;

                    while (todo.Any())
                    {
                        var currentSection = todo.First();
                        var separatePages = todo.Skip(1).TakeWhile(page => page.IsSeparatePage);

                        WriteSection(hikes, writer, currentSection, separatePages);

                        foreach (var separatePage in separatePages)
                        {
                            var nextSeparatePage = todo.SkipWhile(page => page != separatePage)
                                                       .Skip(1)
                                                       .FirstOrDefault(page => page.IsSeparatePage);

                            WriteSeparatePage(hikes, outPath, separatePage, prevSeparatePage, nextSeparatePage);

                            prevSeparatePage = separatePage;
                        }

                        todo = todo.Skip(1 + separatePages.Count());
                    }

                    writer.WriteLine("    </div>");
                    writer.WriteLine("  </body>");
                    writer.WriteLine("</html>");
                }
            }
        }


        static void WriteSeparatePage(IEnumerable<Hike> hikes, string outPath, HikeSection page, HikeSection prev, HikeSection next)
        {
            using (var file = File.OpenWrite(Path.Combine(outPath, page.Url)))
            using (var writer = new StreamWriter(file))
            {
                WebsiteBuilder.WriteHtmlHeader(writer, page.LongTitle, "../");

                writer.WriteLine("    <div class=\"fixedwidth\">");

                writer.WriteLine("      <p class=\"heading\">{0}</p>", page.LongTitle.Replace(": ", ":<br/>"));

                WriteSection(hikes, writer, page);

                writer.WriteLine("      <div class=\"pagefooter\">");

                if (prev != null)
                {
                    writer.WriteLine("        <a href=\"{0}\">prev</a> -", prev.Url);
                }

                writer.WriteLine("        <a href=\"./\">up</a>");

                if (next != null)
                {
                    writer.WriteLine("        - <a href=\"{0}\">next</a>", next.Url);
                }

                writer.WriteLine("      </div>");
                writer.WriteLine("    </div>");
                writer.WriteLine("  </body>");
                writer.WriteLine("</html>");
            }
        }


        static void WriteSection(IEnumerable<Hike> hikes, StreamWriter writer, HikeSection section, IEnumerable<HikeSection> separatePages = null)
        {
            writer.WriteLine("      <div class=\"description\">");

            // Section title?
            if (!string.IsNullOrEmpty(section.Title) && !section.IsSeparatePage)
            {
                writer.WriteLine("        <p class=\"heading\">{0}</p>", section.Title);
            }

            // Section text.
            foreach (var line in section.Descriptions)
            {
                var expandedLinks = ExpandLinks(line, hikes);

                writer.WriteLine("        <p>{0}</p>", expandedLinks);
            }

            // Links to separate pages?
            if (separatePages != null && separatePages.Any())
            {
                writer.WriteLine("        <ul>");

                foreach (var page in separatePages)
                {
                    writer.WriteLine("          <li><a href=\"{0}\">{1}</a></li>", page.Url, page.Title);
                }

                writer.WriteLine("        </ul>");
            }

            writer.WriteLine("      </div>");

            // Photos.
            if (section.Photos.Any())
            {
                writer.WriteLine("      <table class=\"photos\">");

                int photoCount = 0;

                foreach (var photo in section.Photos)
                {
                    if ((photoCount & 1) == 0)
                    {
                        if (photoCount > 0)
                        {
                            writer.WriteLine("        </tr>");
                        }

                        writer.WriteLine("        <tr>");
                    }

                    if (photo.IsPanorama)
                    {
                        writer.WriteLine("          <td class=\"panorama\" colspan=\"2\">");
                        photoCount++;
                    }
                    else
                    {
                        writer.WriteLine("          <td>");
                    }

                    writer.WriteLine("            <a href=\"{0}\">", photo.Filename);
                    writer.WriteLine("              <img src=\"{0}\" width=\"{1}\" height=\"{2}\" />", photo.Thumbnail, photo.ThumbnailSize.Width / 2, photo.ThumbnailSize.Height / 2);
                    writer.WriteLine("              <p>{0}</p>", photo.Description);
                    writer.WriteLine("            </a>");
                    writer.WriteLine("          </td>");

                    photoCount++;
                }

                if (photoCount > 0)
                {
                    writer.WriteLine("        </tr>");
                }

                writer.WriteLine("      </table>");
            }
        }


        static Regex linkRegex = new Regex(@"\[(\w+)\]");


        static string ExpandLinks(string line, IEnumerable<Hike> hikes)
        {
            for (var match = linkRegex.Match(line); match.Success; match = linkRegex.Match(line))
            {
                line = line.Substring(0, match.Index) +
                       ExpandLink(match.Groups[1].Value, hikes) +
                       line.Substring(match.Index + match.Length);
            }

            return line;
        }


        static string ExpandLink(string name, IEnumerable<Hike> hikes)
        {
            var target = hikes.FirstOrDefault(hike => hike.FolderName == name);

            if (target == null)
            {
                throw new Exception("Unknown link target " + name);
            }

            return string.Format("<a href=\"../{0}/\">{1}</a>", target.FolderName, target.HikeName);
        }


        async Task WritePhoto(Photo photo, string outPath)
        {
            using (new Profiler("Hike.WritePhoto"))
            {
                const int thumbnailWidth = 1200;
                const int thumbnailHeight = 380;

                int maxPhotoSize = photo.IsPanorama ? 4096 : 2048;

                using (var bitmap = await imageProcessor.LoadImage(sourcePath, photo.Filename))
                {
                    if (bitmap.Size.Width > maxPhotoSize || bitmap.Size.Height > maxPhotoSize)
                    {
                        // Resize if the source is excessively large.
                        using (var sensibleSize = imageProcessor.ResizeImage(bitmap, maxPhotoSize, maxPhotoSize))
                        {
                            await imageProcessor.SaveImage(sensibleSize, outPath, photo.Filename);
                        }
                    }
                    else
                    {
                        // If the size is ok, just copy it directly over.
                        File.Copy(Path.Combine(sourcePath, photo.Filename), Path.Combine(outPath, photo.Filename));
                    }

                    // Also create thumbnail versions.
                    using (var thumbnail = imageProcessor.ResizeImage(bitmap, thumbnailWidth, thumbnailHeight))
                    {
                        await imageProcessor.SaveImage(thumbnail, outPath, photo.Thumbnail);

                        photo.ThumbnailSize = thumbnail.SizeInPixels;
                    }
                }
            }
        }
    }
}
