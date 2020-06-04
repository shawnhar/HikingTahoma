using System.Collections.Generic;

namespace builder
{
    class HikeSection
    {
        public string Title;
        public readonly List<Photo> Photos = new List<Photo>();
        public readonly List<string> Descriptions = new List<string>();
    }
}
