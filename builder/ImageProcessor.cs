using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Windows.UI;

namespace builder
{
    class ImageProcessor
    {
        public CanvasDevice Device { get; private set; }
        public CanvasBitmap MasterMap { get; private set; }

        public int MapWidth => 1200;
        public int MapHeight => (int)(MasterMap.SizeInPixels.Height * MapWidth / MasterMap.SizeInPixels.Width);


        public async Task Initialize(string sourceFolder)
        {
            Device = new CanvasDevice();

            MasterMap = await LoadImage(sourceFolder, "map.png");
        }


        public async Task<CanvasBitmap> LoadImage(string folder, string filename)
        {
            using (new Profiler("ImageProcessor.LoadImage"))
            {
                return await CanvasBitmap.LoadAsync(Device, Path.Combine(folder, filename));
            }
        }


        public async Task SaveImage(CanvasBitmap bitmap, string folder, string filename)
        {
            using (new Profiler("ImageProcessor.SaveImage"))
            {
                await bitmap.SaveAsync(Path.Combine(folder, filename));
            }
        }


        public CanvasBitmap ResizeImage(CanvasBitmap bitmap, int maxW, int maxH)
        {
            using (new Profiler("ImageProcessor.ResizeImage"))
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


        public async Task<(float DistanceHiked, float CompletionRatio)> MeasureProgressTowardGoal(List<Hike> hikes, string sourceFolder, string outPath)
        {
            // Manually measured using CalTopo. Does not double count any overlaps or out-and-back.
            const float totalLengthOfAllTrails = 286;

            using (new Profiler("ImageProcessor.MeasureProgressTowardGoal"))
            {
                const int todoDilation = 24;

                using (var allTrails = await LoadImage(sourceFolder, "alltrails.png"))
                using (var trailsHiked = new CanvasRenderTarget(allTrails, allTrails.Size))
                using (var trailsTodo = new CanvasRenderTarget(allTrails, allTrails.Size))
                {
                    // Combine the individual maps of all hikes done so far.
                    using (var drawingSession = trailsHiked.CreateDrawingSession())
                    {
                        foreach (var hike in hikes.OrderBy(hike => hike.HikeName))
                        {
                            drawingSession.DrawImage(hike.Map.RawOverlay);
                        }
                    }

                    // Subtract out trails already hiked from the allTrails map, leaving only the sections that are still to be hiked.
                    using (var drawingSession = trailsTodo.CreateDrawingSession())
                    {
                        drawingSession.DrawImage(allTrails);

                        // Expand slightly to avoid partial alpha artifacts along edges.
                        var dilate = new MorphologyEffect
                        {
                            Source = trailsHiked,
                            Mode = MorphologyEffectMode.Dilate,
                            Width = 6,
                            Height = 6,
                        };

                        drawingSession.DrawImage(dilate, trailsTodo.Bounds, trailsTodo.Bounds, 1, CanvasImageInterpolation.Linear, CanvasComposite.DestinationOut);
                    }

                    // Write out an overlay showing trails that are still to be hiked.
                    var scale = (float)MapWidth / (float)trailsTodo.SizeInPixels.Width;

                    using (var todo = new CanvasRenderTarget(Device, (float)trailsTodo.Size.Width * scale, (float)trailsTodo.Size.Height * scale, 96))
                    {
                        var todoOverlay = HikeMap.GetTrailOverlay(trailsTodo, trailsTodo.Bounds, scale, Colors.Red, todoDilation);

                        using (var drawingSession = todo.CreateDrawingSession())
                        {
                            drawingSession.DrawImage(todoOverlay);
                        }

                        await SaveImage(todo, outPath, "todo.png");
                    }

                    // What portion of the total set of trails has been hiked so far?
                    var pixelsHiked = CountPixels(trailsHiked);
                    var todoPixels = CountPixels(trailsTodo);

                    var completionRatio = pixelsHiked / (pixelsHiked + todoPixels);

                    var distanceHiked = totalLengthOfAllTrails * completionRatio;

                    return (distanceHiked, completionRatio);
                }
            }
        }


        static float CountPixels(CanvasBitmap bitmap)
        {
            using (new Profiler("ImageProcessor.CountPixels"))
            {
                var bins = CanvasImage.ComputeHistogram(bitmap, bitmap.Bounds, bitmap.Device, EffectChannelSelect.Alpha, 2);

                return bins[1];
            }
        }


        public async Task WriteMasterMap(string outPath, List<Hike> hikes)
        {
            using (new Profiler("ImageProcessor.WriteMasterMap"))
            {
                const int overlayDilation = 24;

                var yearColors = new Dictionary<string, Color>
                {
                    { "older", Color.FromArgb(0xFF, 0x00, 0x80, 0x00) },
                    { "2019",  Color.FromArgb(0xFF, 0x00, 0xD0, 0x00) },
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
                                          from year in hike.Map.YearsHiked
                                          group hike by year into years
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
                                var overlay = hike.Map.GetTrailOverlay(color, overlayDilation, year.Key);

                                drawingSession.DrawImage(overlay);
                            }
                        }

                        // Draw a text key.
                        var textFormat = new CanvasTextFormat
                        {
                            FontFamily = "Tahoma",
                            FontSize = 32,
                            HorizontalAlignment = CanvasHorizontalAlignment.Right,
                            VerticalAlignment = CanvasVerticalAlignment.Top,
                        };

                        var textRect = result.Bounds;
                        var borderRect = textRect;

                        textRect.Width -= 16;
                        textRect.Y += 16;

                        borderRect.X = borderRect.Right - 104;
                        borderRect.Height = 26 + yearColors.Count * 46;

                        drawingSession.FillRectangle(borderRect, Color.FromArgb(0x80, 0xB0, 0xB0, 0xB0));
                        drawingSession.DrawRectangle(borderRect, Color.FromArgb(0xFF, 0x95, 0xC0, 0xE0));

                        foreach (var year in hikesByYear)
                        {
                            drawingSession.DrawText(year.Key, textRect, yearColors[year.Key], textFormat);

                            textRect.Y += 46;
                        }
                    }

                    await SaveImage(result, outPath, "map.jpg");
                }
            }
        }
    }
}
