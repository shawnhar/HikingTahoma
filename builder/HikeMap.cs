using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Networking.BackgroundTransfer;
using Windows.UI;

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


        public async Task WriteThumbnail(string folder, string filename)
        {
            const int thumbnailSize = 256;
            const float thumbnailPadding = 0.25f;
            
            const float vignetteAmount = 0.333f;
            const float vignetteCurve = 1f;

            const int routeDilation = 3;

            var bounds = GetUsedBounds(myMap);

            // Make it square.
            if (bounds.Width > bounds.Height)
            {
                bounds.Y -= (bounds.Width - bounds.Height) / 2;
                bounds.Height = bounds.Width;
            }
            else
            {
                bounds.X -= (bounds.Height - bounds.Width) / 2;
                bounds.Width = bounds.Height;
            }

            // Add some padding.
            bounds.X -= bounds.Width * thumbnailPadding;
            bounds.Y -= bounds.Height * thumbnailPadding;

            bounds.Width *= 1 + thumbnailPadding * 2;
            bounds.Height *= 1 + thumbnailPadding * 2;

            // Clamp to within the image.
            if (bounds.X < 0)
                bounds.X = 0;

            if (bounds.Y < 0)
                bounds.Y = 0;

            if (bounds.Right > myMap.Size.Width)
                bounds.X = myMap.Size.Width - bounds.Width;

            if (bounds.Bottom > myMap.Size.Height)
                bounds.Y = myMap.Size.Height - bounds.Height;

            // Transform chosen image region to output coordinates.
            var transform = Matrix3x2.CreateTranslation(-(float)bounds.X,
                                                        -(float)bounds.Y) *
                            Matrix3x2.CreateScale(thumbnailSize / (float)bounds.Width,
                                                  thumbnailSize / (float)bounds.Height);

            // Crop, scale, and vignette the background image.
            var background = new VignetteEffect
            {
                Amount = vignetteAmount,
                Curve = vignetteCurve,
                Color = Color.FromArgb(0xFF, 0xD0, 0xD0, 0xD0),

                Source = new Transform2DEffect
                {
                    TransformMatrix = transform,
                    InterpolationMode = CanvasImageInterpolation.HighQualityCubic,

                    Source = new CropEffect
                    {
                        SourceRectangle = bounds,
                        Source = imageProcessor.MasterMap,
                    },
                },
            };

            // Crop, scale, dilate, and tint the hike overlay.
            var route = new Transform2DEffect
            {
                TransformMatrix = transform,
                InterpolationMode = CanvasImageInterpolation.HighQualityCubic,

                Source = new LinearTransferEffect
                {
                    RedSlope = 0,
                    GreenSlope = 0,
                    BlueSlope = 0,

                    RedOffset = 1, 
                    GreenOffset = 0, 
                    BlueOffset = 0, 

                    Source = new MorphologyEffect
                    {
                        Mode = MorphologyEffectMode.Dilate,
                        Width = (int)(routeDilation * bounds.Width / thumbnailSize),
                        Height = (int)(routeDilation * bounds.Height / thumbnailSize),

                        Source = new CropEffect
                        {
                            SourceRectangle = bounds,
                            Source = myMap,
                        },
                    },
                },
            };

            // Adjust relevant region of master map to desired output size and overlay the hike route.
            using (var result = new CanvasRenderTarget(imageProcessor.Device, thumbnailSize, thumbnailSize, 96))
            {
                using (var drawingSession = result.CreateDrawingSession())
                {
                    drawingSession.DrawImage(background);
                    drawingSession.DrawImage(route);
                }

                await imageProcessor.SaveImage(result, folder, filename);
            }
        }


        static Rect GetUsedBounds(CanvasBitmap bitmap)
        {
            int bitmapW = (int)bitmap.SizeInPixels.Width;

            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minY = int.MaxValue;
            int maxY = int.MinValue;

            var colors = bitmap.GetPixelColors();

            for (int i = 0; i < colors.Length; i++)
            {
                if (colors[i].A > 0)
                {
                    int x = i % bitmapW;
                    int y = i / bitmapW;

                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
    }
}
