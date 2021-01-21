using System.Collections.Generic;
using System.IO;

namespace builder
{
    class ImageMap
    {
        class Cell
        {
            public readonly Hike Hike;
            public readonly byte Coverage;

            public int Width = 1;
            public int Height = 1;


            public Cell(Hike hike, byte coverage)
            {
                Hike = hike;
                Coverage = coverage;
            }
        }


        const int subdiv = 64;

        readonly Cell[,] cells = new Cell[subdiv, subdiv];


        public ImageMap(IEnumerable<Hike> hikes)
        {
            foreach (var hike in hikes)
            {
                var hikeCoverage = hike.Map.GetImageMap(subdiv);

                for (int y = 0; y < subdiv; y++)
                {
                    for (int x = 0; x < subdiv; x++)
                    {
                        var coverage = hikeCoverage[x + y * subdiv];

                        if (IsBetterHikeChoice(hike, coverage, cells[x, y]))
                        {
                            cells[x, y] = new Cell(hike, coverage);
                        }
                    }
                }
            }

            MergeAdjacentCells();
        }


        static bool IsBetterHikeChoice(Hike hike, byte coverage, Cell previousHike)
        {
            if (coverage == 0)
                return false;

            if (previousHike == null)
                return true;

            if (coverage > previousHike.Coverage * 3 / 2)
                return true;

            if (previousHike.Coverage > coverage * 3 / 2)
                return false;

            return CombineDistanceAndElevation(hike) < CombineDistanceAndElevation(previousHike.Hike);
        }


        static float CombineDistanceAndElevation(Hike hike)
        {
            var distance = float.Parse(hike.Distance);
            var elevation = float.Parse(hike.ElevationGain);

            return distance * (elevation + 3000);
        }


        void MergeAdjacentCells()
        {
            using (new Profiler("ImageMap.MergeAdjacentCells"))
            {
                for (int y = 0; y < subdiv; y++)
                {
                    for (int x = 0; x < subdiv; x++)
                    {
                        if (cells[x, y] != null)
                        {
                            // Expand to the right.
                            int right = x + 1;

                            while (right < subdiv && IsSameHike(right, right + 1, y, cells[x, y].Hike))
                            {
                                cells[x, y].Width++;
                                ClearCells(right, right + 1, y);
                                right++;
                            }

                            // Expand downward.
                            int bottom = y + 1;

                            while (bottom < subdiv && IsSameHike(x, right, bottom, cells[x, y].Hike))
                            {
                                cells[x, y].Height++;
                                ClearCells(x, right, bottom);
                                bottom++;
                            }
                        }
                    }
                }
            }
        }


        bool IsSameHike(int x1, int x2, int y, Hike hike)
        {
            for (int x = x1; x < x2; x++)
            {
                if (cells[x, y]?.Hike != hike)
                    return false;
            }

            return true;
        }


        void ClearCells(int x1, int x2, int y)
        {
            for (int x = x1; x < x2; x++)
            {
                cells[x, y] = null;
            }
        }


        public void Write(StreamWriter writer)
        {
            for (int y = 0; y < subdiv; y++)
            {
                for (int x = 0; x < subdiv; x++)
                {
                    if (cells[x, y] != null)
                    {
                        writer.WriteLine("      <a href=\"{0}/\"><span class=\"imagemap\" style=\"left:{1}%; top:{2}%; right:{3}%; bottom:{4}%\" onMouseEnter=\"OnEnterImage(document, '{0}')\" onMouseLeave=\"OnLeaveImage(document, '{0}')\" /></a>",
                                         cells[x, y].Hike.FolderName,
                                         (float)x / subdiv * 100,
                                         (float)y / subdiv * 100,
                                         (float)(subdiv - x - cells[x, y].Width) / subdiv * 100,
                                         (float)(subdiv - y - cells[x, y].Height) / subdiv * 100);
                    }
                }
            }
        }
    }
}
