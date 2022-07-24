using System.IO;
using Windows.Graphics.Imaging;

namespace builder
{
    class Photo
    {
        public readonly string Filename;
        public readonly string Description;

        public readonly bool IsPanorama;
        public readonly bool IsFeatured;

        public string Thumbnail => Path.GetFileNameWithoutExtension(Filename) + "-small.jpg";

        public BitmapSize ThumbnailSize;


        public Photo(string filename, string description)
        {
            if (filename.StartsWith('-'))
            {
                IsPanorama = true;
                filename = filename.Substring(1);
            }
            else if (filename.StartsWith('!'))
            {
                IsFeatured = true;
                filename = filename.Substring(1);
            }

            Filename = filename;
            Description = description;
        }
    }
}
