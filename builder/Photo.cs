using System.IO;

namespace builder
{
    class Photo
    {
        public readonly string Filename;
        public readonly string Description;

        public string Thumbnail => Path.GetFileName(Filename) + "-small.jpg";


        public Photo(string filename, string description)
        {
            Filename = filename;
            Description = description;
        }
    }
}
