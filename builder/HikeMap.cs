﻿using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI;

namespace builder
{
    enum MapEdge
    {
        None,
        Top,
        Bottom,
        Left,
    }


    class HikeMap
    {
        readonly ImageProcessor imageProcessor;
        readonly Dictionary<string, CanvasBitmap> myMaps = new Dictionary<string, CanvasBitmap>();
        CanvasBitmap combinedMap;
        CanvasBitmap overlayMap;
        Rect usedBounds;
        Rect overlayBounds;
        MapEdge isOffEdge;

        public IEnumerable<string> YearsHiked => myMaps.Keys;
        public CanvasBitmap CombinedOverlay => combinedMap;


        public HikeMap(ImageProcessor imageProcessor, MapEdge isOffEdge)
        {
            this.imageProcessor = imageProcessor;
            this.isOffEdge = isOffEdge;
        }


        public async Task Load(string sourceFolder, string firstHiked)
        {
            using (new Profiler("HikeMap.Load"))
            {
                myMaps[firstHiked] = await imageProcessor.LoadImage(sourceFolder, "map.png");

                // Some hikes have additional map overlays, if pieces of them were first hiked in a different year from the main trail.
                var layerFilenames = Directory.GetFiles(sourceFolder, "map-*.png").Select(Path.GetFileName);

                foreach (var layerFilename in layerFilenames)
                {
                    var year = Path.GetFileNameWithoutExtension(layerFilename).Substring(4);
                    var layerImage = await imageProcessor.LoadImage(sourceFolder, layerFilename);

                    myMaps.Add(year, layerImage);
                }

                // If there are multiple layers, flatten them to create a combined map.
                if (myMaps.Count == 1)
                {
                    combinedMap = myMaps[firstHiked];
                }
                else
                {
                    var size = myMaps[firstHiked].SizeInPixels;
                    var combined = new CanvasRenderTarget(imageProcessor.Device, size.Width, size.Height, 96);

                    using (var ds = combined.CreateDrawingSession())
                    {
                        ds.Clear(Colors.Transparent);

                        foreach (var layer in myMaps.Values)
                        {
                            ds.DrawImage(layer);
                        }
                    }

                    combinedMap = combined;
                }

                usedBounds = GetUsedBounds(combinedMap);

                // Does this hike have a custom overlay image, due to being outside the normal map bounds?
                if (File.Exists(Path.Combine(sourceFolder, "overlay.png")))
                {
                    overlayMap = await imageProcessor.LoadImage(sourceFolder, "overlay.png");
                    overlayBounds = GetUsedBounds(overlayMap);
                }
            }
        }


        public async Task WriteTrailMaps(string folder, string mapName, string mapThumbnail, bool isNever)
        {
            using (new Profiler("HikeMap.WriteTrailMaps"))
            {
                const int mapSize = 1024;
                const int thumbnailSize = 512;

                const int mapDilation = 4;
                const int thumbnailDilation = 5;

                const int minBoundsSize = 256;

                float padding = 0.25f;

                // Make it square.
                var bounds = usedBounds;

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

                // Expand super-tiny maps.
                if (bounds.Width < minBoundsSize)
                {
                    padding = Math.Max((float)((minBoundsSize - bounds.Width) / (bounds.Width * 2)), padding);
                }

                // Add some padding.
                bounds.X -= bounds.Width * padding;
                bounds.Y -= bounds.Height * padding;

                bounds.Width *= 1 + padding * 2;
                bounds.Height *= 1 + padding * 2;

                // Don't exceed the total map size.
                if (bounds.Width > combinedMap.Size.Width || bounds.Height > combinedMap.Size.Height)
                {
                    var shrink = Math.Min(combinedMap.Size.Width / bounds.Width, combinedMap.Size.Height / bounds.Height);

                    bounds.X += bounds.Width * (1 - shrink) / 2;
                    bounds.Y += bounds.Height * (1 - shrink) / 2;

                    bounds.Width *= shrink;
                    bounds.Height *= shrink;
                }

                // Clamp to within the image.
                if (bounds.X < 0)
                    bounds.X = 0;

                if (bounds.Y < 0)
                    bounds.Y = 0;

                if (bounds.Right > combinedMap.Size.Width)
                    bounds.X = combinedMap.Size.Width - bounds.Width;

                if (bounds.Bottom > combinedMap.Size.Height)
                    bounds.Y = combinedMap.Size.Height - bounds.Height;

                // Write two sizes of map.
                await WriteTrailMap(bounds, mapSize, mapDilation, false, folder, mapName, isNever);
                await WriteTrailMap(bounds, thumbnailSize, thumbnailDilation, true, folder, mapThumbnail, isNever);
            }
        }


        async Task WriteTrailMap(Rect bounds, int outputSize, int dilation, bool vignette, string folder, string filename, bool isNever)
        {
            const float vignetteAmount = 0.333f;
            const float vignetteCurve = 1f;

            // Transform chosen image region to output coordinates.
            var transform = Matrix3x2.CreateTranslation(-(float)bounds.X,
                                                        -(float)bounds.Y);

            transform *= Matrix3x2.CreateScale(outputSize / (float)bounds.Width,
                                               outputSize / (float)bounds.Height);

            // Crop, scale, and optionally vignette the background image.
            var backgroundSource = imageProcessor.MasterMap;
            var backgroundTransform = transform;
            var backgroundBounds = bounds;

            switch (isOffEdge)
            {
                case MapEdge.Top:
                    backgroundSource = imageProcessor.CombineMaps(imageProcessor.MapTop, imageProcessor.MasterMap, false);
                    break;

                case MapEdge.Bottom:
                    backgroundSource = imageProcessor.CombineMaps(imageProcessor.MasterMap, imageProcessor.MapBottom, false);
                    var offset = (float)(combinedMap.Size.Height - backgroundSource.Size.Height);
                    backgroundTransform = Matrix3x2.CreateTranslation(0, offset) * backgroundTransform;
                    backgroundBounds.Y -= offset;
                    break;

                case MapEdge.Left:
                    backgroundSource = imageProcessor.CombineMaps(imageProcessor.MapLeft, imageProcessor.MasterMap, true);
                    break;
            }

            ICanvasEffect background = new Transform2DEffect
            {
                TransformMatrix = backgroundTransform,
                InterpolationMode = CanvasImageInterpolation.HighQualityCubic,

                Source = new CropEffect
                {
                    SourceRectangle = backgroundBounds,
                    Source = backgroundSource,
                },
            };

            if (vignette)
            {
                background = new VignetteEffect
                {
                    Amount = vignetteAmount,
                    Curve = vignetteCurve,
                    Color = Color.FromArgb(0xFF, 0xD0, 0xD0, 0xD0),

                    Source = background,
                };
            }

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

                    RedOffset = isNever ? 1 : 0,
                    GreenOffset = 0,
                    BlueOffset = isNever ? 0 : 1,

                    Source = new MorphologyEffect
                    {
                        Mode = MorphologyEffectMode.Dilate,
                        Width = (int)Math.Max(dilation * bounds.Width / outputSize, 1),
                        Height = (int)Math.Max(dilation * bounds.Height / outputSize, 1),

                        Source = new CropEffect
                        {
                            SourceRectangle = bounds,
                            Source = combinedMap,
                        },
                    },
                },
            };

            // Adjust relevant region of master map to desired output size and overlay the hike route.
            using (var result = new CanvasRenderTarget(imageProcessor.Device, outputSize, outputSize, 96))
            {
                using (var drawingSession = result.CreateDrawingSession())
                {
                    drawingSession.DrawImage(background);
                    drawingSession.DrawImage(route);
                }

                await imageProcessor.SaveImage(result, folder, filename);
            }
        }


        public async Task WriteTrailOverlay(string folder, string filename, bool isNever)
        {
            using (new Profiler("HikeMap.WriteTrailOverlay"))
            {
                const int overlayDilation = 40;

                var color = isNever ? Colors.Red : Colors.Blue;

                var trailOverlay = GetTrailOverlay(color, overlayDilation);

                using (var result = new CanvasRenderTarget(imageProcessor.Device, imageProcessor.MapWidth, imageProcessor.MapHeight, 96))
                {
                    using (var drawingSession = result.CreateDrawingSession())
                    {
                        drawingSession.DrawImage(trailOverlay);
                    }

                    await imageProcessor.SaveImage(result, folder, filename);
                }
            }
        }


        public ICanvasImage GetTrailOverlay(Color color, int dilation, string year = null)
        {
            var source = string.IsNullOrEmpty(year) ? combinedMap : myMaps[year];
            var bounds = usedBounds;
            var offset = GetOverlayOffset();

            if (overlayMap != null)
            {
                source = overlayMap;
                bounds = overlayBounds;
                offset = Vector2.Zero;
            }

            var scale = (isOffEdge == MapEdge.Left) ? (float)imageProcessor.MapHeight / (float)combinedMap.SizeInPixels.Height :
                                                      (float)imageProcessor.MapWidth / (float)combinedMap.SizeInPixels.Width;

            var transform = Matrix3x2.CreateTranslation(offset) *
                            Matrix3x2.CreateScale(scale);

            return GetTrailOverlay(source, bounds, transform, color, dilation);        
        }


        public static ICanvasImage GetTrailOverlay(ICanvasImage source, Rect bounds, Matrix3x2 transform, Color color, int dilation)
        {
            using (new Profiler("HikeMap.GetTrailOverlay"))
            {
                return new Transform2DEffect
                {
                    TransformMatrix = transform,
                    InterpolationMode = CanvasImageInterpolation.HighQualityCubic,

                    Source = new LinearTransferEffect
                    {
                        RedSlope = 0,
                        GreenSlope = 0,
                        BlueSlope = 0,

                        RedOffset = color.R / 255.0f,
                        GreenOffset = color.G / 255.0f,
                        BlueOffset = color.B / 255.0f,

                        Source = new MorphologyEffect
                        {
                            Mode = MorphologyEffectMode.Dilate,
                            Width = dilation,
                            Height = dilation,

                            Source = new CropEffect
                            {
                                Source = source,
                                SourceRectangle = bounds,
                            },
                        },
                    },
                };
            }
        }


        public Vector2 GetOverlayOffset()
        {
            switch (isOffEdge)
            {
                case MapEdge.Top:
                    return new Vector2(0, (float)(-imageProcessor.MapTop.Size.Height));
                
                case MapEdge.Bottom:
                    return new Vector2(0, (float)(imageProcessor.MasterMap.Size.Height + imageProcessor.MapBottom.Size.Height - combinedMap.Size.Height));
                
                case MapEdge.Left:
                    return new Vector2((float)(-imageProcessor.MapLeft.Size.Width), 0);
                
                default:
                    return Vector2.Zero;
            }
        }


        static Rect GetUsedBounds(CanvasBitmap bitmap)
        {
            using (new Profiler("HikeMap.GetUsedBounds"))
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


        public byte[] GetImageMap(int subdiv)
        {
            using (new Profiler("HikeMap.GetImageMap"))
            {
                var map = overlayMap ?? combinedMap;
                var offset = overlayMap != null ? Vector2.Zero : GetOverlayOffset();
                var scale = new Vector2(subdiv) / imageProcessor.MasterMap.Size.ToVector2();

                using (var result = new CanvasRenderTarget(imageProcessor.Device, subdiv, subdiv, 96))
                {
                    var effect = new Transform2DEffect
                    {
                        TransformMatrix = Matrix3x2.CreateTranslation(offset) * Matrix3x2.CreateScale(scale),
                        InterpolationMode = CanvasImageInterpolation.HighQualityCubic,

                        Source = new MorphologyEffect
                        {
                            Mode = MorphologyEffectMode.Dilate,
                            Width = 100,
                            Height = 100,

                            Source = map,
                        },
                    };

                    using (var drawingSession = result.CreateDrawingSession())
                    {
                        drawingSession.Clear(Colors.Transparent);
                        drawingSession.DrawImage(effect);
                    }

                    var colors = result.GetPixelColors();

                    return colors.Select(color => color.A).ToArray();
                }
            }
        }
    }
}
