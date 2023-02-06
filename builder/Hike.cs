using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace builder
{
    class Hike : Article
    {
        public string Difficulty { get; private set; }
        public string Distance { get; private set; }
        public bool IsOneWay { get; private set; }
        public string ElevationGain { get; private set; }
        public string MaxElevation { get; private set; }
        public string Region { get; private set; }
        public string CampSites { get; private set; }
        public string FirstHiked { get; private set; }

        public string HikeName => ArticleName;
        public string MapName => FolderName + "-map.jpg";
        public string MapThumbnail => FolderName + "-map-small.jpg";
        public string OverlayName => FolderName + "-overlay.png";
        public bool IsHidden => HikeName == "misc";
        public bool IsFuture => FirstHiked == "not yet";
        public bool IsNever => FirstHiked == "never";

        public readonly HikeMap Map;

        ImageProcessor imageProcessor;

        override protected string RootPrefix => "../";


        public Tuple<string, string> DifficultyCategory
        {
            get
            {
                if (Difficulty.Contains("overnighter") || Difficulty.Contains("days"))
                {
                    return Tuple.Create("x", "Backpacking");
                }

                if (Difficulty.Contains("unofficial") || Difficulty.Contains("unmaintained") || Difficulty.Contains("off-trail"))
                {
                    return Tuple.Create("unmaintained", "Unmaintained, off-trail");
                }

                var epos = (uint)Difficulty.IndexOf("easy");
                var mpos = (uint)Difficulty.IndexOf("moderate");
                var spos = (uint)Difficulty.IndexOf("strenuous");

                if (epos < mpos && epos < spos)
                {
                    return Tuple.Create("easy", "Easy");
                }
                else if (mpos < spos)
                {
                    return Tuple.Create("moderate", "Moderate");
                }
                else if (spos < uint.MaxValue || Difficulty.Contains("wtf"))
                {
                    return Tuple.Create("strenuous", "Strenuous");
                }
                else
                {
                    throw new Exception("Unknown difficulty category: " + Difficulty);
                }
            }
        }


        MapEdge IsOffEdge()
        {
            switch (FolderName)
            {
                case "WestForkWhiteRiver":
                case "HuckleberryCreek":
                case "HuckleberryGrandParkLoop":
                    return MapEdge.Top;

                case "BackboneRidge":
                    return MapEdge.Bottom;

                case "GlacierViewWilderness":
                    return MapEdge.Left;

                default:
                    return MapEdge.None;
            }
        }


        public Hike(string sourcePath, ImageProcessor imageProcessor)
            : base(sourcePath, "report.txt", "index.html")
        {
            this.imageProcessor = imageProcessor;

            Map = new HikeMap(imageProcessor, IsOffEdge());
        }


        public async Task Load(bool loadMap)
        {
            ValidatePhotos();

            if (loadMap)
            {
                await Map.Load(sourcePath, FirstHiked);
            }
        }


        override protected int ParseDescriptionHeader(string[] lines)
        {
            // First two lines are the hike name and difficulty.
            ArticleName = lines[0];
            Difficulty = lines[1];

            // Third line is 'distance elevationGain maxElevation'
            var split = lines[2].Split(' ');

            Distance = split[0];
            ElevationGain = split[1];
            MaxElevation = split[2];

            if (Distance.EndsWith(".oneway"))
            {
                Distance = Distance.Substring(0, Distance.Length - ".oneway".Length);
                IsOneWay = true;
            }

            // Fourth line is the region.
            Region = lines[3];

            // Fifth line is campsites along this trail.
            CampSites = lines[4];

            // Sixth line is when I first hiked it.
            FirstHiked = lines[5];

            return 6;
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


        override protected async Task WritePhotos(string outPath)
        {
            foreach (var photo in Sections.SelectMany(section => section.Photos))
            {
                await imageProcessor.WritePhoto(photo, sourcePath, outPath);
            }

            await Map.WriteTrailMaps(outPath, MapName, MapThumbnail, IsNever);
            await Map.WriteTrailOverlay(outPath, OverlayName, IsNever);
        }


        override protected void WritePageTitle(StreamWriter writer)
        {
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
            writer.WriteLine("            <p class=\"detail\">{0} miles{1}</p>", Distance, IsOneWay ? " one way" : string.Empty);
            writer.WriteLine("            <p class=\"detail\">Elevation gain: {0}'</p>", ElevationGain);
            writer.WriteLine("            <p class=\"detail\">Max elevation: {0}'</p>", MaxElevation);
            writer.WriteLine("            <p class=\"detail\">Camps: {0}</p>", CampSites);
            writer.WriteLine("            <p class=\"detail\">First hiked by me: {0}</p>", FirstHiked);
            writer.WriteLine("          </td>");
            writer.WriteLine("        </tr>");
            writer.WriteLine("      </table>");
        }
    }
}
