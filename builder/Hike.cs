using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public string FolderName => Path.GetFileName(sourcePath);
        public string MapName => FolderName + "-map.jpg";
        public string MapThumbnail => FolderName + "-map-small.jpg";
        public string OverlayName => FolderName + "-overlay.png";

        string sourcePath;

        List<Photo> photos;
        List<string> descriptions;

        ImageProcessor imageProcessor;

        public readonly HikeMap Map;


        public Hike(string sourcePath, ImageProcessor imageProcessor)
        {
            this.sourcePath = sourcePath;
            this.imageProcessor = imageProcessor;

            Map = new HikeMap(imageProcessor);
        }


        public async Task Process(string outPath)
        {
            ParseReport();
            ValidatePhotos();

            await Map.Load(sourcePath);
            
            await WriteOutput(outPath);
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
                                 .SkipWhile(line => string.IsNullOrEmpty(line));

            // Set of photos in the form: [filename.jpg] description.
            photos = remainder.TakeWhile(line => line.StartsWith('['))
                              .Select(ParsePhoto)
                              .ToList();

            // Rest of the file is a text description;
            remainder = remainder.Skip(photos.Count)
                                 .SkipWhile(string.IsNullOrEmpty);

            descriptions = new List<string>();

            while (remainder.Any())
            {
                var descs = remainder.TakeWhile(line => !string.IsNullOrEmpty(line));

                descriptions.Add(string.Join(' ', descs.Select(s => s.Trim())));

                remainder = remainder.Skip(descs.Count())
                                     .SkipWhile(string.IsNullOrEmpty);
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
            var photoNames = photos.Select(photo => photo.Filename);

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


        async Task WriteOutput(string dir)
        {
            var outPath = Path.Combine(dir, FolderName);

            Directory.CreateDirectory(outPath);

            WriteHtml(outPath);

            await Map.WriteTrailMaps(outPath, MapName, MapThumbnail);

            await Map.WriteTrailOverlay(outPath, OverlayName);

            foreach (var photo in photos)
            {
                await WritePhoto(photo, outPath);
            }
        }


        void WriteHtml(string outPath)
        {
            using (var file = File.OpenWrite(Path.Combine(outPath, FolderName + ".html")))
            using (var writer = new StreamWriter(file))
            {
                WebsiteBuilder.WriteHtmlHeader(writer, this.HikeName, "../");

                writer.WriteLine("<table>");
                writer.WriteLine("  <tr>");
                writer.WriteLine("    <td>");
                writer.WriteLine("      <a href=\"{0}\"/>", this.MapName);
                writer.WriteLine("        <img src=\"{0}\"/>", this.MapThumbnail);
                writer.WriteLine("      </a>");
                writer.WriteLine("    </td>");
                writer.WriteLine("    <td class=\"stats\">");
                writer.WriteLine("      <p class=\"hikename\">{0}</p>", this.HikeName);
                writer.WriteLine("      <p class=\"detail\">Difficulty: {0}</p>", this.Difficulty);
                writer.WriteLine("      <p class=\"detail\">{0} miles</p>", this.Distance);
                writer.WriteLine("      <p class=\"detail\">Elevation gain: {0}'</p>", this.ElevationGain);
                writer.WriteLine("      <p class=\"detail\">Max elevation: {0}'</p>", this.MaxElevation);
                writer.WriteLine("      <p class=\"detail\">First hiked by me: {0}</p>", this.FirstHiked);
                writer.WriteLine("    </td>");
                writer.WriteLine("  </tr>");
                writer.WriteLine("</table>");

                writer.WriteLine("<div class=\"description\">");

                foreach (var line in this.descriptions)
                {
                    writer.WriteLine("  <p>{0}</p>", line);
                }

                writer.WriteLine("</div>");

                writer.WriteLine("<table class=\"photos\">");

                int photoCount = 0;

                foreach (var photo in this.photos)
                {
                    if ((photoCount & 1) == 0)
                    {
                        if (photoCount > 0)
                        {
                            writer.WriteLine("  </tr>");
                        }

                        writer.WriteLine("  <tr>");
                    }

                    writer.WriteLine("    <td>");
                    writer.WriteLine("      <a href=\"{0}\">", photo.Filename);
                    writer.WriteLine("        <img src=\"{0}\"/>", photo.Thumbnail);
                    writer.WriteLine("        <p>{0}</p>", photo.Description);
                    writer.WriteLine("      </a>");
                    writer.WriteLine("    </td>");

                    photoCount++;
                }

                if (photoCount > 0)
                {
                    writer.WriteLine("  </tr>");
                }

                writer.WriteLine("</table>");

                writer.WriteLine("</body>");
                writer.WriteLine("</html>");
            }
        }


        async Task WritePhoto(Photo photo, string outPath)
        {
            const int maxPhotoSize = 2048;
            const int thumbnailHeight = 190;

            using (var bitmap = await imageProcessor.LoadImage(sourcePath, photo.Filename))
            {
                if (bitmap.Size.Width > maxPhotoSize || bitmap.Size.Height > maxPhotoSize)
                {
                    // Resize if the source is excessively large.
                    using (var sensibleSize = imageProcessor.ResizeImage(bitmap, maxPhotoSize, maxPhotoSize))
                    {
                        await imageProcessor.SaveJpeg(sensibleSize, outPath, photo.Filename);
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
                    await imageProcessor.SaveJpeg(thumbnail, outPath, photo.Thumbnail, 1);
                }
            }
        }
    }
}
