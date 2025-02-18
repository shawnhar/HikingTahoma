﻿using System.IO;
using Windows.Graphics.Imaging;

namespace builder
{
    class Photo
    {
        public readonly string Filename;
        public readonly string Description;

        public readonly bool IsPanorama;
        public readonly bool IsFeatured;
        public readonly bool IsReference;

        public string Thumbnail => Path.Combine(Path.GetDirectoryName(Filename),
                                                Path.GetFileNameWithoutExtension(Filename).TrimEnd('-') + "-small.jpg")
                                       .Replace('\\', '/');

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
            else if (filename.StartsWith('+'))
            {
                IsReference = true;
                filename = filename.Substring(1);
            }

            if (filename.Contains("/"))
            {
                IsReference = true;
            }

            Filename = filename;
            Description = description;
        }
    }
}
