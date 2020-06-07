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
        public string FirstHiked    { get; private set; }

        readonly List<HikeSection> Sections = new List<HikeSection>();

        public string FolderName => Path.GetFileName(sourcePath);
        public string MapName => FolderName + "-map.jpg";
        public string MapThumbnail => FolderName + "-map-small.jpg";
        public string OverlayName => FolderName + "-overlay.png";

        public readonly HikeMap Map;

        string sourcePath;

        ImageProcessor imageProcessor;


        public Hike(string sourcePath, ImageProcessor imageProcessor)
        {
            this.sourcePath = sourcePath;
            this.imageProcessor = imageProcessor;

            Map = new HikeMap(imageProcessor);
        }


        public async Task Load()
        {
            ParseReport();
            ValidatePhotos();

            await Map.Load(sourcePath);
        }


        void ParseReport()
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

            // Fourth line is when I first hiked it.
            FirstHiked = lines[3];

            var remainder = lines.Skip(4)
                                 .SkipWhile(string.IsNullOrEmpty);

            while (remainder.Any())
            {
                var currentSection = new HikeSection();

                Sections.Add(currentSection);

                // Optional section title.
                if (remainder.First().StartsWith("# "))
                {
                    currentSection.Title = remainder.First().Substring(2).Trim();

                    remainder = remainder.Skip(1)
                                         .SkipWhile(string.IsNullOrEmpty);
                }

                // Set of photos in the form: [filename.jpg] description.
                currentSection.Photos.AddRange(remainder.TakeWhile(line => line.StartsWith('['))
                                                        .Select(ParsePhoto));

                remainder = remainder.Skip(currentSection.Photos.Count)
                                     .SkipWhile(string.IsNullOrEmpty);

                // Rest of the file is a text description;
                while (remainder.Any() && !remainder.First().StartsWith("# "))
                {
                    var descs = remainder.TakeWhile(line => !string.IsNullOrEmpty(line));

                    currentSection.Descriptions.Add(string.Join(' ', descs.Select(s => s.Trim())));

                    remainder = remainder.Skip(descs.Count())
                                         .SkipWhile(string.IsNullOrEmpty);
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
            

        public async Task WriteOutput(IEnumerable<Hike> hikes, string dir)
        {
            var outPath = Path.Combine(dir, FolderName);

            Directory.CreateDirectory(outPath);

            foreach (var photo in Sections.SelectMany(section => section.Photos))
            {
                await WritePhoto(photo, outPath);
            }

            WriteHtml(hikes, outPath);

            await Map.WriteTrailMaps(outPath, MapName, MapThumbnail);

            await Map.WriteTrailOverlay(outPath, OverlayName);
        }


        void WriteHtml(IEnumerable<Hike> hikes, string outPath)
        {
            using (var file = File.OpenWrite(Path.Combine(outPath, FolderName + ".html")))
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
                writer.WriteLine("            <p class=\"detail\">First hiked by me: {0}</p>", FirstHiked);
                writer.WriteLine("          </td>");
                writer.WriteLine("        </tr>");
                writer.WriteLine("      </table>");

                foreach (var section in Sections)
                {
                    writer.WriteLine("      <div class=\"description\">");

                    // Section title?
                    if (!string.IsNullOrEmpty(section.Title))
                    {
                        writer.WriteLine("        <p class=\"heading\">{0}</p>", section.Title);
                    }

                    // Section text.
                    foreach (var line in section.Descriptions)
                    {
                        var expandedLinks = ExpandLinks(line, hikes);

                        writer.WriteLine("        <p>{0}</p>", expandedLinks);
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

                            writer.WriteLine("          <td>");
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

                writer.WriteLine("    </div>");
                writer.WriteLine("  </body>");
                writer.WriteLine("</html>");
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

            return string.Format("<a href=\"../{0}/{0}.html\">{1}</a>", target.FolderName, target.HikeName);
        }


        async Task WritePhoto(Photo photo, string outPath)
        {
            const int maxPhotoSize = 2048;
            const int thumbnailHeight = 380;

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
                using (var thumbnail = imageProcessor.ResizeImage(bitmap, int.MaxValue, thumbnailHeight))
                {
                    await imageProcessor.SaveImage(thumbnail, outPath, photo.Thumbnail);

                    photo.ThumbnailSize = thumbnail.SizeInPixels;
                }
            }
        }
    }
}
