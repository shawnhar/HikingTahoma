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
        readonly MapRenderer renderer;

        CanvasBitmap myMap;


        public HikeMap(MapRenderer renderer)
        {
            this.renderer = renderer;
        }


        public async Task Load(string sourceFolder)
        {
            myMap = await CanvasBitmap.LoadAsync(renderer.Device, Path.Combine(sourceFolder, "map.png"));
        }


        public async Task WriteThumbnail(string filename)
        {
            var scale = new Transform2DEffect
            {
                Source = renderer.MasterMap,
                TransformMatrix = Matrix3x2.CreateScale(0.01f),
                InterpolationMode = CanvasImageInterpolation.HighQualityCubic,
            };

            var bounds = scale.GetBounds(renderer.Device);

            using (var stream = File.OpenWrite(filename))
            {
                await CanvasImage.SaveAsync(scale, bounds, 96, renderer.Device, stream.AsRandomAccessStream(), CanvasBitmapFileFormat.Jpeg);
            }
        }
    }
}
