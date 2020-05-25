using Microsoft.Graphics.Canvas;
using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;

namespace builder
{
    class ImageProcessor
    {
        public CanvasDevice Device { get; private set; }
        public CanvasBitmap MasterMap { get; private set; }


        public async Task Initialize(string sourceFolder)
        {
            Device = new CanvasDevice();

            MasterMap = await LoadImage(sourceFolder, "map.png");
        }


        public async Task<CanvasBitmap> LoadImage(string folder, string filename)
        {
            return await CanvasBitmap.LoadAsync(Device, Path.Combine(folder, filename));
        }


        public async Task SaveImage(CanvasBitmap bitmap, string folder, string filename)
        {
            await bitmap.SaveAsync(Path.Combine(folder, filename), CanvasBitmapFileFormat.Jpeg);
        }


        public CanvasBitmap ResizeImage(CanvasBitmap bitmap, int maxW, int maxH)
        {
            var size = bitmap.Size.ToVector2();

            var sizeX = (size.X > maxW) ? size / (size.X / maxW) : size;
            var sizeY = (size.Y > maxH) ? size / (size.Y / maxH) : size;

            var newSize = Vector2.Min(sizeX, sizeY);

            var result = new CanvasRenderTarget(Device, newSize.X, newSize.Y, 96);

            using (var drawingSession = result.CreateDrawingSession())
            {
                drawingSession.DrawImage(bitmap, result.Bounds, bitmap.Bounds, 1, CanvasImageInterpolation.HighQualityCubic);
            }

            return result;
        }
    }
}
