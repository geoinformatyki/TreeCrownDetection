using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TreeCrownDetection
{
    public class TreeCrownDetector
    {

        public TreeCrownDetector(IReadOnlyList<double> rawImage, int width, int height, int cutOffValue, int findLocalMaxWindowSize, int findMaxAreaWindowSize)
        {
            this.rawImage = rawImage;
            this.width = width;
            this.height = height;
            this.cutOffValue = cutOffValue;
            this.findLocalMaxWindowSize = findLocalMaxWindowSize;
            this.findMaxAreaWindowSize = findMaxAreaWindowSize;
        }

        private IReadOnlyList<double> rawImage;
        private int width;
        private int height;
        private int cutOffValue;
        private int findLocalMaxWindowSize;
        private int findMaxAreaWindowSize;

        private HashSet<Tuple<int, int>>[,] pointToMaximumAreaTable;

        public int[,] GetLabeledTrees()
        {
            pointToMaximumAreaTable = new HashSet<Tuple<int, int>>[height, width];
            var res = new int[height, width];
            Dictionary<Tuple<int, int>, int> maximums = new Dictionary<Tuple<int, int>, int>();
            int objectId = 1;

            for (var i = 1; i < height - 1; i++)
            {
                for (var j = 1; j < width - 1; j++)
                {
                    var partRes = FindLocalMax(i, j);

                    if (partRes == null)
                    {
                        continue;
                    }

                    if (!maximums.ContainsKey(partRes))
                    {
                        foreach (var max in FindMaximumArea(partRes.Item1, partRes.Item2))
                        {
                            maximums.Add(max, objectId);
                        }

                        objectId++;
                    }

                    res[i, j] = maximums[partRes];
                }

            }
            System.Console.WriteLine("Labeled objects: ");
            System.Console.WriteLine(objectId);

            return res;
        }

        private Tuple<int, int> FindLocalMax(int i, int j)
        {
            if (rawImage[i * width + j] < cutOffValue)
            {
                return null;
            }

            double currentMax;
            Tuple<int, int> newIndex;

            while (i > 0 && i < height - 1 && j > 0 && j < width - 1)
            {
                currentMax = rawImage[i * width + j];
                newIndex = null;

                for (int y = -findLocalMaxWindowSize; y < findLocalMaxWindowSize + 1; ++y)
                {
                    if (i + y > 0 && i + y < height - 1)
                    {
                        for (int x = -findLocalMaxWindowSize; x < findLocalMaxWindowSize + 1; ++x)
                        {
                            if (j + x > 0 && j + x < width - 1)
                            {
                                if (currentMax < rawImage[(i + y) * width + j + x])
                                {
                                    currentMax = rawImage[(i + y) * width + j + x];
                                    newIndex = new Tuple<int, int>(i + y, j + x);
                                }
                            }

                        }
                    }
                }

                if (newIndex == null)
                {
                    foreach (var it in FindMaximumArea(i, j))
                    {
                        return it;
                    }
                }

                i = newIndex.Item1;
                j = newIndex.Item2;
            }

            return null;
        }

        private HashSet<Tuple<int, int>> FindMaximumArea(int i, int j)
        {
            if (pointToMaximumAreaTable[i, j] != null)
            {
                return pointToMaximumAreaTable[i, j];
            }

            var res = new HashSet<Tuple<int, int>>();
            var addedCurrent = new HashSet<Tuple<int, int>>();
            var addedNew = new HashSet<Tuple<int, int>>();

            res.Add(new Tuple<int, int>(i, j));
            addedCurrent.Add(new Tuple<int, int>(i, j));

            var maxVal = rawImage[i * width + j];
            double tmpVal;


            while (addedCurrent.Count != 0)
            {
                foreach (var item in addedCurrent)
                {
                    i = item.Item1;
                    j = item.Item2;

                    for (int y = -findMaxAreaWindowSize; y < findMaxAreaWindowSize + 1; ++y)
                    {
                        if (i + y > 0 && i + y < height - 1)
                        {
                            for (int x = -findMaxAreaWindowSize; x < findMaxAreaWindowSize + 1; ++x)
                            {
                                if (j + x > 0 && j + x < width - 1)
                                {
                                    tmpVal = rawImage[(i + y) * width + j + x];

                                    if (maxVal == tmpVal)
                                    {
                                        var partRes = new Tuple<int, int>(i + y, j + x);
                                        if (!res.Contains(partRes) && !addedCurrent.Contains(partRes) && !addedNew.Contains(partRes))
                                        {
                                            res.Add(partRes);
                                            addedNew.Add(partRes);
                                        }
                                    }

                                    if (maxVal < tmpVal)
                                    {
                                        var maxRes = FindMaximumArea(i + y, j + x);

                                        foreach (var point in res)
                                        {
                                            pointToMaximumAreaTable[point.Item1, point.Item2] = maxRes;
                                        }

                                        return maxRes;
                                    }
                                }
                            }
                        }
                    }
                }
                addedCurrent = addedNew;
                addedNew = new HashSet<Tuple<int, int>>();
            }

            return res;
        }
    }
}
