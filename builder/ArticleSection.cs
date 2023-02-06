using System.Collections.Generic;

namespace builder
{
    class ArticleSection
    {
        public string Title { get; private set; }
        public string LongTitle { get; private set; }

        public string Url { get; private set; }

        public bool IsSeparatePage;

        public readonly List<Photo> Photos = new List<Photo>();
        public readonly List<string> Descriptions = new List<string>();


        public void SetTitle(string sectionTitle, string articleName)
        {
            // Possible parameter forms:
            //  "<short title>|<long title>"
            //  "<short title>" (in which case the long title is <articleName> <shorttitle>)

            var substring = sectionTitle.IndexOf('|');

            if (substring > 0)
            {
                Title = sectionTitle.Substring(0, substring);
                LongTitle = sectionTitle.Substring(substring + 1);
            }
            else
            {
                Title = sectionTitle;
                LongTitle = articleName + " " + sectionTitle;
            }

            // If title is in the form "Day 1: Sunrise to Mystic", the URL is just "Day1".
            substring = Title.IndexOf(':');

            if (substring < 0)
            {
                substring = Title.Length;
            }

            Url = Title.Substring(0, substring).Replace(" ", string.Empty)
                                               .Replace("?", string.Empty) + ".html";
        }
    }
}
