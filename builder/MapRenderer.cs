using Microsoft.Graphics.Canvas;
using System;
using System.IO;
using System.Threading.Tasks;

namespace builder
{
    class MapRenderer
    {
        public readonly CanvasDevice Device;

        public CanvasBitmap MasterMap { get; private set; }


        public MapRenderer()
        {
            Device = new CanvasDevice();
        }


        public async Task LoadMap(string sourceFolder)
        {
            MasterMap = await CanvasBitmap.LoadAsync(Device, Path.Combine(sourceFolder, "map.png"));
        }
    }
}
