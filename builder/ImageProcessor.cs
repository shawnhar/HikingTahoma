using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Windows.UI;

namespace builder
{
    class ImageProcessor
    {
        // Manually measured using CalTopo. Does not double count any overlaps or out-and-back.
        const float totalLengthOfAllTrails = 353;

        public CanvasDevice Device { get; private set; }
        public CanvasBitmap MasterMap { get; private set; }
        public CanvasBitmap MapTop { get; private set; }
        public CanvasBitmap MapBottom { get; private set; }
        public CanvasBitmap MapLeft { get; private set; }

        public int MapWidth => 1200;
        public int MapHeight => (int)(MasterMap.SizeInPixels.Height * MapWidth / MasterMap.SizeInPixels.Width);

        readonly List<string> badAspectRatios = new List<string>();


        public async Task Initialize(string sourceFolder)
        {
            Device = new CanvasDevice();

            MasterMap = await LoadImage(sourceFolder, "map.png");
            MapTop = await LoadImage(sourceFolder, "maptop.png");
            MapBottom = await LoadImage(sourceFolder, "mapbottom.png");
            MapLeft = await LoadImage(sourceFolder, "mapleft.png");
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


        public CanvasBitmap CombineMaps(CanvasBitmap a, CanvasBitmap b, bool horizontal)
        {
            using (new Profiler("ImageProcessor.CombineMaps"))
            {
                var result = new CanvasRenderTarget(Device, 
                                                    horizontal ? (a.SizeInPixels.Width + b.SizeInPixels.Width) : a.SizeInPixels.Width,
                                                    horizontal ? a.SizeInPixels.Height : (a.SizeInPixels.Height + b.SizeInPixels.Height), 
                                                    96);

                using (var drawingSession = result.CreateDrawingSession())
                {
                    drawingSession.DrawImage(a);
                    drawingSession.DrawImage(b, horizontal ? a.SizeInPixels.Width : 0,
                                                horizontal ? 0 : a.SizeInPixels.Height);
                }

                return result;
            }
        }


        public async Task<(float DistanceHiked, float CompletionRatio)> MeasureProgressTowardGoal(List<Hike> hikes, string sourceFolder, string outPath)
        {
            using (new Profiler("ImageProcessor.MeasureProgressTowardGoal"))
            {
                const int todoDilation = 24;

                using (var allTrails = await LoadImage(sourceFolder, "alltrails.png"))
                {
                    var expandedSize = allTrails.Size;

                    expandedSize.Height += MapTop.Size.Height;
                    expandedSize.Height += MapBottom.Size.Height;

                    var expandedOffset = new Vector2(0, (float)MapTop.Size.Height);

                    using (var trailsHiked = new CanvasRenderTarget(allTrails, expandedSize))
                    using (var trailsTodo = new CanvasRenderTarget(allTrails, allTrails.Size))
                    {
                        // Combine the individual maps of all hikes done so far.
                        using (var drawingSession = trailsHiked.CreateDrawingSession())
                        {
                            foreach (var hike in hikes.OrderBy(hike => hike.HikeName))
                            {
                                drawingSession.DrawImage(hike.Map.CombinedOverlay, expandedOffset + hike.Map.GetOverlayOffset());
                            }
                        }

                        // Subtract out trails already hiked from the allTrails map, leaving only the sections that are still to be hiked.
                        using (var drawingSession = trailsTodo.CreateDrawingSession())
                        {
                            drawingSession.DrawImage(allTrails);

                            // Expand slightly to avoid partial alpha artifacts along edges.
                            var dilate = new MorphologyEffect
                            {
                                Mode = MorphologyEffectMode.Dilate,
                                Width = 6,
                                Height = 6,

                                Source = new Transform2DEffect
                                {
                                    Source = trailsHiked,
                                    TransformMatrix = Matrix3x2.CreateTranslation(-expandedOffset),
                                },
                            };

                            drawingSession.DrawImage(dilate, trailsTodo.Bounds, trailsTodo.Bounds, 1, CanvasImageInterpolation.Linear, CanvasComposite.DestinationOut);
                        }

                        // Write out an overlay showing trails that are still to be hiked.
                        using (var todo = new CanvasRenderTarget(Device, MapWidth, MapHeight, 96))
                        {
                            var transform = Matrix3x2.CreateScale((float)MapWidth / (float)trailsTodo.SizeInPixels.Width);

                            var todoOverlay = HikeMap.GetTrailOverlay(trailsTodo, trailsTodo.Bounds, transform, Colors.Red, todoDilation);

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
        }


        static float CountPixels(CanvasBitmap bitmap)
        {
            using (new Profiler("ImageProcessor.CountPixels"))
            {
                var bins = CanvasImage.ComputeHistogram(bitmap, bitmap.Bounds, bitmap.Device, EffectChannelSelect.Alpha, 2);

                return bins[1] * (float)(bitmap.Size.Width * bitmap.Size.Height);
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
                    { "2021",  Color.FromArgb(0xFF, 0xFF, 0xA0, 0x00) },
                    { "2022",  Color.FromArgb(0xFF, 0xFF, 0x60, 0xFF) },
                    { "newer", Color.FromArgb(0xFF, 0x00, 0xA0, 0xFF) },
                };

                using (var result = new CanvasRenderTarget(Device, MapWidth, MapHeight, 96))
                {
                    using (var drawingSession = result.CreateDrawingSession())
                    {
                        // Draw the main map.
                        drawingSession.DrawImage(MasterMap, result.Bounds, MasterMap.Bounds, 1, CanvasImageInterpolation.HighQualityCubic);

                        // Overlay trail routes, sorted by year.
                        Func<string, string> yearSortKey = year => char.IsDigit(year[0]) ? year : "0";

                        var hikesByYear = from hike in hikes
                                          from year in hike.Map.YearsHiked
                                          group hike by year into years
                                          orderby yearSortKey(years.Key) descending
                                          select years;

                        foreach (var year in hikesByYear)
                        {
                            Color color;

                            if (!yearColors.TryGetValue(Hike.MergeRecentYears(year.Key), out color))
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

                        borderRect.X = borderRect.Right - 116;
                        borderRect.Height = 26 + yearColors.Count * 46;

                        drawingSession.FillRectangle(borderRect, Color.FromArgb(0x80, 0xB0, 0xB0, 0xB0));
                        drawingSession.DrawRectangle(borderRect, Color.FromArgb(0xFF, 0x95, 0xC0, 0xE0));

                        foreach (var year in hikesByYear.Select(year => Hike.MergeRecentYears(year.Key)).Distinct())
                        {
                            drawingSession.DrawText(year, textRect, yearColors[year], textFormat);

                            textRect.Y += 46;
                        }
                    }

                    await SaveImage(result, outPath, "map.jpg");
                }
            }
        }


        public CanvasBitmap ProcessMapOverlay(CanvasBitmap bitmap)
        {
            const int dilation = 40;
            const int fringeDilation = 4;

            var scale = new Vector2(MapWidth, MapHeight) / bitmap.Size.ToVector2();

            var transformedMap = new Transform2DEffect
            {
                TransformMatrix = Matrix3x2.CreateScale(scale),
                InterpolationMode = CanvasImageInterpolation.HighQualityCubic,

                Source = new LinearTransferEffect
                {
                    RedSlope = 0,
                    GreenSlope = 0,
                    BlueSlope = 0,

                    RedOffset = 1,
                    GreenOffset = 1,
                    BlueOffset = 1,

                    Source = new MorphologyEffect
                    {
                        Mode = MorphologyEffectMode.Dilate,
                        Width = dilation,
                        Height = dilation,

                        Source = bitmap
                    }
                }
            };

            var mapWithFringe = new ArithmeticCompositeEffect
            {
                Source1 = transformedMap,

                Source2 = new MorphologyEffect
                {
                    Mode = MorphologyEffectMode.Dilate,
                    Width = fringeDilation,
                    Height = fringeDilation,

                    Source = transformedMap
                },

                Source1Amount = 127f / 128,
                Source2Amount = 1f / 128,

                MultiplyAmount = 0
            };

            var result = new CanvasRenderTarget(Device, MapWidth, MapHeight, 96);

            using (var drawingSession = result.CreateDrawingSession())
            {
                drawingSession.DrawImage(mapWithFringe);
            }

            return result;
        }


        public async Task WritePhoto(Photo photo, string sourcePath, string outPath, bool generateThumbnail = true, bool generateFullSize = true)
        {
            using (new Profiler("ImageProcessor.WritePhoto"))
            {
                const int thumbnailWidth = 1100;
                const int thumbnailHeight = 380;

                int maxPhotoSize = photo.IsPanorama ? 4096 : 2048;

                using (var bitmap = await LoadImage(sourcePath, photo.Filename))
                {
                    // Check the aspect ratio.
                    if (!photo.IsPanorama &&
                        (bitmap.Size.Width != bitmap.Size.Height * 4 / 3) &&
                        (bitmap.Size.Height != bitmap.Size.Width * 4 / 3))
                    {
                        badAspectRatios.Add(Path.Combine(Path.GetFileName(outPath), photo.Filename));
                    }

                    if (generateFullSize)
                    {
                        if (bitmap.Size.Width > maxPhotoSize || bitmap.Size.Height > maxPhotoSize)
                        {
                            // Resize if the source is excessively large.
                            using (var sensibleSize = ResizeImage(bitmap, maxPhotoSize, maxPhotoSize))
                            {
                                await SaveImage(sensibleSize, outPath, photo.Filename);
                            }
                        }
                        else
                        {
                            // If the size is ok, just copy it directly over.
                            File.Copy(Path.Combine(sourcePath, photo.Filename), Path.Combine(outPath, photo.Filename));
                        }
                    }

                    // Also create thumbnail versions.
                    if (generateThumbnail)
                    {
                        using (var thumbnail = ResizeImage(bitmap, thumbnailWidth, thumbnailHeight))
                        {
                            await SaveImage(thumbnail, outPath, photo.Thumbnail);

                            photo.ThumbnailSize = thumbnail.SizeInPixels;
                        }
                    }
                }
            }
        }


        public void ThrowIfBadAspectRatios()
        {
            if (badAspectRatios.Any())
            {
                foreach (var filename in badAspectRatios)
                {
                    Debug.WriteLine("Photo has bad aspect ratio: " + filename);
                }

                throw new Exception("Some photos have incorrect aspect ratios. See debugger output for details.");
            }
        }
    }
}
