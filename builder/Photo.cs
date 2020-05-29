using System.IO;
using Windows.Graphics.Imaging;

namespace builder
{
    class Photo
    {
        public readonly string Filename;
        public readonly string Description;

        public string Thumbnail => Path.GetFileName(Filename) + "-small.jpg";

        public BitmapSize ThumbnailSize;


        public Photo(string filename, string description)
        {
            Filename = filename;
            Description = description;
        }
    }
}
