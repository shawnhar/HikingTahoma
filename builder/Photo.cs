namespace builder
{
    class Photo
    {
        public readonly string Filename;
        public readonly string Description;


        public Photo(string filename, string description)
        {
            Filename = filename;
            Description = description;
        }
    }
}
