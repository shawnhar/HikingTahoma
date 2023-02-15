using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace builder
{
    class Article
    {
        protected string ArticleName { get; set; }

        public readonly List<ArticleSection> Sections = new List<ArticleSection>();

        public string FolderName => Path.GetFileName(sourcePath);

        readonly protected string sourcePath;

        readonly string descriptionFilename;
        readonly string htmlFilename;

        string cssClass = "description";

        protected virtual string RootPrefix => "./";


        public Article(string sourcePath, string descriptionFilename, string htmlFilename)
        {
            this.sourcePath = sourcePath;
            this.descriptionFilename = descriptionFilename;
            this.htmlFilename = htmlFilename;

            ParseDescription();
        }


        void ParseDescription()
        {
            using (new Profiler("Article.ParseDescription"))
            {
                var lines = File.ReadAllLines(Path.Combine(sourcePath, descriptionFilename));

                int headerLineCount = ParseDescriptionHeader(lines);

                var remainder = lines.Skip(headerLineCount)
                                     .SkipWhile(string.IsNullOrEmpty);

                var titleRegex = new Regex("^#+ ");

                while (remainder.Any())
                {
                    var currentSection = new ArticleSection();

                    Sections.Add(currentSection);

                    // Optional section title:
                    // "# Title" embeds the section within the current page.
                    // "## Title" outputs the section as a separate page.
                    var match = titleRegex.Match(remainder.First());

                    if (match.Success)
                    {
                        currentSection.SetTitle(remainder.First().Substring(match.Length).Trim(), ArticleName);
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


        protected virtual int ParseDescriptionHeader(string[] lines)
        {
            ArticleName = lines[0];

            if (string.IsNullOrEmpty(lines[1]))
            {
                return 1;
            }
            else
            {
                cssClass += " " + lines[1];
                return 2;
            }
        }


        static Photo ParsePhoto(string line)
        {
            var i = line.IndexOf(']');

            string filename = line.Substring(1, i - 1);
            string description = line.Substring(i + 1).Trim();

            return new Photo(filename, description);
        }


        public async Task WriteOutput(IEnumerable<Hike> allHikes, string dir)
        {
            using (new Profiler("Article.WriteOutput"))
            {
                var outPath = Path.Combine(dir, FolderName);

                Directory.CreateDirectory(outPath);

                await WritePhotos(outPath);

                WriteHtml(allHikes, outPath);
            }
        }


        protected virtual Task WritePhotos(string outPath)
        {
            return Task.CompletedTask;
        }


        void WriteHtml(IEnumerable<Hike> allHikes, string outPath)
        {
            using (new Profiler("Article.WriteHtml"))
            {
                using (var file = File.OpenWrite(Path.Combine(outPath, htmlFilename)))
                using (var writer = new StreamWriter(file))
                {
                    WebsiteBuilder.WriteHtmlHeader(writer, ArticleName, RootPrefix);

                    writer.WriteLine("    <div class=\"fixedwidth\">");

                    WritePageTitle(writer);

                    IEnumerable<ArticleSection> todo = Sections;

                    ArticleSection prevSeparatePage = null;

                    while (todo.Any())
                    {
                        var currentSection = todo.First();
                        var separatePages = todo.Skip(1).TakeWhile(page => page.IsSeparatePage);

                        WriteSection(allHikes, writer, currentSection, separatePages);

                        foreach (var separatePage in separatePages)
                        {
                            var nextSeparatePage = todo.SkipWhile(page => page != separatePage)
                                                       .Skip(1)
                                                       .FirstOrDefault(page => page.IsSeparatePage);

                            WriteSeparatePage(allHikes, outPath, separatePage, prevSeparatePage, nextSeparatePage);

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


        protected virtual void WritePageTitle(StreamWriter writer)
        {
            writer.WriteLine("      <p class=\"heading\">{0}</p>", ArticleName);
        }


        void WriteSeparatePage(IEnumerable<Hike> allHikes, string outPath, ArticleSection page, ArticleSection prev, ArticleSection next)
        {
            using (var file = File.OpenWrite(Path.Combine(outPath, page.Url)))
            using (var writer = new StreamWriter(file))
            {
                WebsiteBuilder.WriteHtmlHeader(writer, page.LongTitle, RootPrefix);

                writer.WriteLine("    <div class=\"fixedwidth\">");

                writer.WriteLine("      <p class=\"heading\">{0}</p>", page.LongTitle.Replace(": ", ":<br/>"));

                WriteSection(allHikes, writer, page);

                writer.WriteLine("      <div class=\"pagefooter\">");

                if (prev != null)
                {
                    writer.WriteLine("        <a href=\"{0}\">prev</a> -", prev.Url);
                }

                writer.WriteLine("        <a href=\"{0}\">up</a>", htmlFilename == "index.html" ? "./" : htmlFilename);

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


        void WriteSection(IEnumerable<Hike> allHikes, StreamWriter writer, ArticleSection section, IEnumerable<ArticleSection> separatePages = null)
        {
            writer.WriteLine("      <div class=\"{0}\">", cssClass);

            // Section title?
            if (!string.IsNullOrEmpty(section.Title) && !section.IsSeparatePage)
            {
                writer.WriteLine("        <p class=\"heading\">{0}</p>", section.Title);
            }

            // Section text.
            foreach (var line in section.Descriptions)
            {
                var expandedLinks = ExpandLinks(line, allHikes);

                if (line.StartsWith("<p") ||
                    line.StartsWith("<ul") ||
                    line.StartsWith("<ol") ||
                    line.StartsWith("<br") ||
                    line.StartsWith("<hr") ||
                    line.StartsWith("<img") ||
                    line.StartsWith("<table"))
                {
                    writer.WriteLine("        {0}", expandedLinks);
                }
                else
                {
                    writer.WriteLine("        <p>{0}</p>", expandedLinks);
                }
            }

            // Links to separate pages?
            if (separatePages != null && separatePages.Any())
            {
                bool inList = false;
                bool inTable = false;
                int rowCount = 0;

                foreach (var page in separatePages)
                {
                    var featuredPhoto = page.Photos.FirstOrDefault(photo => photo.IsFeatured);

                    if (featuredPhoto != null)
                    {
                        if (!inTable)
                        {
                            Close();
                            writer.WriteLine("        <table class=\"photos showlinks\">");
                            writer.WriteLine("          <tr>");
                            inTable = true;
                            rowCount = 0;
                        }
                        else if (++rowCount >= 2)
                        {
                            writer.WriteLine("          </tr>");
                            writer.WriteLine("          <tr>");
                            rowCount = 0;
                        }

                        writer.WriteLine("            <td><a href=\"{0}\"><img src=\"{1}\" width=\"253\" height=\"190\" /><p>{2}</p></a></td>", page.Url, featuredPhoto.Thumbnail, page.Title);
                    }
                    else
                    {
                        if (!inList)
                        {
                            Close();
                            writer.WriteLine("        <ul>");
                            inList = true;
                        }

                        writer.WriteLine("          <li><a href=\"{0}\">{1}</a></li>", page.Url, page.Title);
                    }
                }

                Close();

                void Close()
                {
                    if (inList)
                    {
                        writer.WriteLine("        </ul>");
                        inList = false;
                    }
                    else if (inTable)
                    {
                        writer.WriteLine("          </tr>");
                        writer.WriteLine("        </table>");
                        inTable = false;
                    }
                }
            }

            writer.WriteLine("      </div>");

            // Photos.
            var photos = section.Photos.Where(photo => !string.IsNullOrEmpty(photo.Description) || !photo.IsFeatured);

            if (photos.Any())
            {
                writer.WriteLine("      <table class=\"photos\">");

                int photoCount = 0;

                foreach (var photo in photos)
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

                    var thumbnailWidth = photo.ThumbnailSize.Width / 2;
                    var thumbnailHeight = photo.ThumbnailSize.Height / 2;

                    if (thumbnailWidth == 0)
                    {
                        if (!photo.IsReference)
                        {
                            throw new Exception("Unknown size thumbnail only allowed when referencing photos from elsewhere: " + photo.Filename);
                        }

                        thumbnailWidth = 253;
                        thumbnailHeight = 190;
                    }

                    writer.WriteLine("            <a href=\"{0}\">", photo.Filename);
                    writer.WriteLine("              <img src=\"{0}\" width=\"{1}\" height=\"{2}\" />", photo.Thumbnail, thumbnailWidth, thumbnailHeight);
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


        string ExpandLinks(string line, IEnumerable<Hike> allHikes)
        {
            line = ExpandLinks(line, hikeLinkRegex, match => ExpandHikeLink(match.Groups[1].Value, allHikes));
            line = ExpandLinks(line, photoLinkRegex, ExpandPhotoLink);

            return line;
        }


        static string ExpandLinks(string line, Regex regex, Func<Match, string> expandFunction)
        {
            for (var match = regex.Match(line); match.Success; match = regex.Match(line))
            {
                line = line.Substring(0, match.Index) +
                       expandFunction(match) +
                       line.Substring(match.Index + match.Length);
            }

            return line;
        }


        static Regex hikeLinkRegex = new Regex(@"\[(\w+)\]");


        string ExpandHikeLink(string name, IEnumerable<Hike> allHikes)
        {
            var target = allHikes.FirstOrDefault(hike => hike.FolderName == name);

            if (target == null)
            {
                throw new Exception("Unknown link target " + name);
            }

            return string.Format("<a href=\"{0}{1}/\">{2}</a>", RootPrefix, target.FolderName, target.HikeName);
        }


        static Regex photoLinkRegex = new Regex(@"\[([\w\.\-/]+\.(jpg|html))(\|([^|\]]+(?=\|)))?(\|([^\]]+))?\]");


        static string ExpandPhotoLink(Match match)
        {
            var linkTarget = match.Groups[1].Value;
            var linkExtension = match.Groups[2].Value;
            var explicitTarget = match.Groups[4].Value;
            var linkLabel = match.Groups[6].Value;

            var smallPhoto = linkTarget.Replace("." + linkExtension, "-small.jpg");

            if (explicitTarget == ".")
            {
                linkTarget = Path.GetDirectoryName(linkTarget).Replace('\\', '/') + "/";
            }
            else if (!string.IsNullOrEmpty(explicitTarget))
            {
                linkTarget = explicitTarget;
            }

            if (!string.IsNullOrEmpty(linkLabel))
            {
                linkLabel = "<p>" + linkLabel + "</p>";
            }

            return string.Format("<a href=\"{0}\"><img src=\"{1}\" width=\"253\" height=\"190\" />{2}</a>", linkTarget, smallPhoto, linkLabel);
        }
    }
}
