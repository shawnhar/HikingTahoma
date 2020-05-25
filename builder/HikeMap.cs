using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;

namespace builder
{
    class HikeMap
    {
        readonly ImageProcessor imageProcessor;

        CanvasBitmap myMap;


        public HikeMap(ImageProcessor imageProcessor)
        {
            this.imageProcessor = imageProcessor;
        }


        public async Task Load(string sourceFolder)
        {
            myMap = await imageProcessor.LoadImage(sourceFolder, "map.png");
        }


        public async Task WriteThumbnail(string filename)
        {
            var scale = new Transform2DEffect
            {
                Source = imageProcessor.MasterMap,
                TransformMatrix = Matrix3x2.CreateScale(0.025f),
                InterpolationMode = CanvasImageInterpolation.HighQualityCubic,
            };

            var bounds = scale.GetBounds(imageProcessor.Device);

            using (var stream = File.OpenWrite(filename))
            {
                await CanvasImage.SaveAsync(scale, bounds, 96, imageProcessor.Device, stream.AsRandomAccessStream(), CanvasBitmapFileFormat.Jpeg);
            }
        }
    }
}
