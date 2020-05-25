using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace builder
{
    class Hike
    {
        public string FolderName => Path.GetFileName(sourcePath);
        public string MapName => FolderName + "-map.jpg";

        string sourcePath;
        string hikeName;
        string distance;
        string elevationGain;
        string maxElevation;

        List<Photo> photos;
        List<string> descriptions;

        HikeMap map;


        public Hike(string sourcePath, MapRenderer mapRenderer)
        {
            this.sourcePath = sourcePath;

            map = new HikeMap(mapRenderer);
        }


        public async Task Process(string outPath)
        {
            ParseReport();
            ValidatePhotos();

            await map.Load(sourcePath);
            
            await WriteOutput(outPath);
        }


        void ParseReport()
        {
            var lines = File.ReadAllLines(Path.Combine(sourcePath, "report.txt"));

            // First line is the hike name.
            hikeName = lines[0];

            // Second line is distance elevationGain maxElevation
            var split = lines[1].Split(' ');

            distance = split[0];
            elevationGain = split[1];
            maxElevation = split[2];

            var remainder = lines.Skip(2)
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

            foreach (var photo in photos)
            {
                File.Copy(Path.Combine(sourcePath, photo.Filename), Path.Combine(outPath, photo.Filename));
            }

            WriteHtml(outPath);

            await map.WriteThumbnail(Path.Combine(outPath, MapName));
        }


        void WriteHtml(string outPath)
        {
            using (var file = File.OpenWrite(Path.Combine(outPath, FolderName + ".html")))
            using (var writer = new StreamWriter(file))
            {
                writer.WriteLine("<html>");

                writer.WriteLine("<title> Hiking Tahoma: {0}</title>", this.hikeName);

                writer.WriteLine("<head>");
                writer.WriteLine("  <link rel=\"stylesheet\" href=\"../style.css\">");
                writer.WriteLine("</head>");

                writer.WriteLine("<body>");

                writer.WriteLine("<div class=\"title\">");

                writer.WriteLine("  <div class=\"about\">");
                writer.WriteLine("    <a href = \".. /AboutRainier.html\">about Mount Rainier</a><br>");
                writer.WriteLine("    <a href = \".. /AboutThisSite.html\">about this site</a><br>");
                writer.WriteLine("    <a href = \".. /FuturePlans.html\">future plans</a>");
                writer.WriteLine("  </div>");
                writer.WriteLine("  <div class=\"backlink\"><a href = \"..\">Hiking Tahoma</a></div>");
                writer.WriteLine("</div>");

                writer.WriteLine("<table>");
                writer.WriteLine("  <tr>");
                writer.WriteLine("    <td>");
                writer.WriteLine("      <img src=\"{0}\"/>", this.MapName);
                writer.WriteLine("    </td>");
                writer.WriteLine("    <td class=\"stats\">");
                writer.WriteLine("      <p class=\"hikename\">{0}</p>", this.hikeName);
                writer.WriteLine("      <p>{0} miles</p>", this.distance);
                writer.WriteLine("      <p>Elevation gain: {0}'</p>", this.elevationGain);
                writer.WriteLine("      <p>Max elevation: {0}'</p>", this.maxElevation);
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
                    writer.WriteLine("        <img src=\"{0}\"/>", photo.Filename);
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
    }
}
