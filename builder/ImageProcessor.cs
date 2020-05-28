using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;

namespace builder
{
    class ImageProcessor
    {
        public CanvasDevice Device { get; private set; }
        public CanvasBitmap MasterMap { get; private set; }

        public const int MapWidth = 600;
        
        public int MapHeight => (int)(MasterMap.SizeInPixels.Height * MapWidth / MasterMap.SizeInPixels.Width);


        public async Task Initialize(string sourceFolder)
        {
            Device = new CanvasDevice();

            MasterMap = await LoadImage(sourceFolder, "map.png");
        }


        public async Task<CanvasBitmap> LoadImage(string folder, string filename)
        {
            return await CanvasBitmap.LoadAsync(Device, Path.Combine(folder, filename));
        }


        public async Task SaveJpeg(CanvasBitmap bitmap, string folder, string filename, float quality = 0.9f)
        {
            await bitmap.SaveAsync(Path.Combine(folder, filename), CanvasBitmapFileFormat.Jpeg, quality);
        }


        public async Task SavePng(CanvasBitmap bitmap, string folder, string filename)
        {
            await bitmap.SaveAsync(Path.Combine(folder, filename), CanvasBitmapFileFormat.Png);
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


        public async Task WriteMasterMap(string outPath, List<Hike> hikes)
        {
            const int overlayDilation = 24;

            var yearColors = new Dictionary<string, Color>
            {
                { "older", Color.FromArgb(0xFF, 0x00, 0x80, 0x00) },
                { "2019",  Color.FromArgb(0xFF, 0x00, 0xFF, 0x00) },
                { "2020",  Color.FromArgb(0xFF, 0xFF, 0xFF, 0x00) },
            };

            using (var result = new CanvasRenderTarget(Device, MapWidth, MapHeight, 96))
            {
                using (var drawingSession = result.CreateDrawingSession())
                {
                    // Draw the main map.
                    drawingSession.DrawImage(MasterMap, result.Bounds, MasterMap.Bounds, 1, CanvasImageInterpolation.HighQualityCubic);

                    // Overlay trail routes, sorted by year.
                    var hikesByYear = from hike in hikes
                                      group hike by hike.FirstHiked into years
                                      orderby char.IsDigit(years.Key[0]) ? years.Key : "0" descending
                                      select years;

                    foreach (var year in hikesByYear)
                    {
                        Color color;

                        if (!yearColors.TryGetValue(year.Key, out color))
                        {
                            throw new Exception("Unknown year " + year.Key);
                        }

                        foreach (var hike in year)
                        {
                            var overlay = hike.Map.GetTrailOverlay(color, overlayDilation);

                            drawingSession.DrawImage(overlay);
                        }
                    }

                    // Draw a text key.
                    var textFormat = new CanvasTextFormat
                    {
                        FontFamily = "Tahoma",
                        FontSize = 16,
                        HorizontalAlignment = CanvasHorizontalAlignment.Right,
                        VerticalAlignment = CanvasVerticalAlignment.Bottom,
                    };

                    var textRect = result.Bounds;

                    textRect.Width -= 12;
                    textRect.Height -= 12;

                    var borderRect = textRect;

                    borderRect.X = borderRect.Right - 48;
                    borderRect.Y = borderRect.Bottom - 28 - yearColors.Count * 23;

                    drawingSession.FillRectangle(borderRect, Color.FromArgb(0x80, 0xB0, 0xB0, 0xB0));
                    drawingSession.DrawRectangle(borderRect, Colors.Gray);

                    foreach (var year in hikesByYear)
                    {
                        drawingSession.DrawText(year.Key, textRect, yearColors[year.Key], textFormat);

                        textRect.Height -= 23;
                    }

                    textFormat.FontStyle = FontStyle.Italic;
                    drawingSession.DrawText("key", textRect, Colors.Gray, textFormat);
                }

                await SavePng(result, outPath, "map.png");
            }
        }
    }
}
