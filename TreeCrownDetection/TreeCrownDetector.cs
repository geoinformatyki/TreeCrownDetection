using System;
using System.Collections.Generic;

namespace TreeCrownDetection
{
    public class TreeCrownDetector
    {
        private readonly int _cutOffValue;
        private readonly int _findLocalMaxWindowSize;
        private readonly int _findMaxAreaWindowSize;
        private readonly int _height;

        private HashSet<Tuple<int, int>>[,] _pointToMaximumAreaTable;

        private readonly IReadOnlyList<double> _rawImage;
        private readonly int _width;
        private const double Epsilon = 0.00001;

        public TreeCrownDetector(IReadOnlyList<double> rawImage, int width, int height, int cutOffValue,
            int findLocalMaxWindowSize, int findMaxAreaWindowSize)
        {
            this._rawImage = rawImage;
            this._width = width;
            this._height = height;
            this._cutOffValue = cutOffValue;
            this._findLocalMaxWindowSize = findLocalMaxWindowSize;
            this._findMaxAreaWindowSize = findMaxAreaWindowSize;
        }

        public int[,] GetLabeledTrees()
        {
            _pointToMaximumAreaTable = new HashSet<Tuple<int, int>>[_height, _width];
            var res = new int[_height, _width];
            var maximums = new Dictionary<Tuple<int, int>, int>();
            var objectId = 1;

            for (var i = 1; i < _height - 1; i++)
                for (var j = 1; j < _width - 1; j++)
                {
                    var partRes = FindLocalMax(i, j);

                    if (partRes == null) continue;

                    if (!maximums.ContainsKey(partRes))
                    {
                        foreach (var max in FindMaximumArea(partRes.Item1, partRes.Item2)) maximums.Add(max, objectId);

                        objectId++;
                    }

                    res[i, j] = maximums[partRes];
                }

            Console.WriteLine("Labeled objects: ");
            Console.WriteLine(objectId);

            return res;
        }

        private Tuple<int, int> FindLocalMax(int i, int j)
        {
            if (_rawImage[i * _width + j] < _cutOffValue) return null;

            while (i > 0 && i < _height - 1 && j > 0 && j < _width - 1)
            {
                var currentMax = _rawImage[i * _width + j];
                Tuple<int, int> newIndex = null;

                for (var y = -_findLocalMaxWindowSize; y < _findLocalMaxWindowSize + 1; ++y)
                    if (i + y > 0 && i + y < _height - 1)
                        for (var x = -_findLocalMaxWindowSize; x < _findLocalMaxWindowSize + 1; ++x)
                            if (j + x > 0 && j + x < _width - 1)
                                if (currentMax < _rawImage[(i + y) * _width + j + x])
                                {
                                    currentMax = _rawImage[(i + y) * _width + j + x];
                                    newIndex = new Tuple<int, int>(i + y, j + x);
                                }

                if (newIndex == null)
                    foreach (var it in FindMaximumArea(i, j))
                        return it;

                if (newIndex == null) continue;
                i = newIndex.Item1;
                j = newIndex.Item2;
            }

            return null;
        }

        private HashSet<Tuple<int, int>> FindMaximumArea(int i, int j)
        {
            if (_pointToMaximumAreaTable[i, j] != null) return _pointToMaximumAreaTable[i, j];

            var res = new HashSet<Tuple<int, int>>();
            var addedCurrent = new HashSet<Tuple<int, int>>();
            var addedNew = new HashSet<Tuple<int, int>>();

            res.Add(new Tuple<int, int>(i, j));
            addedCurrent.Add(new Tuple<int, int>(i, j));

            var maxVal = _rawImage[i * _width + j];

            while (addedCurrent.Count != 0)
            {
                foreach (var item in addedCurrent)
                {
                    i = item.Item1;
                    j = item.Item2;

                    for (var y = -_findMaxAreaWindowSize; y < _findMaxAreaWindowSize + 1; ++y)
                        if (i + y > 0 && i + y < _height - 1)
                            for (var x = -_findMaxAreaWindowSize; x < _findMaxAreaWindowSize + 1; ++x)
                                if (j + x > 0 && j + x < _width - 1)
                                {
                                    var tmpVal = _rawImage[(i + y) * _width + j + x];

                                    if (Math.Abs(maxVal - tmpVal) < Epsilon)
                                    {
                                        var partRes = new Tuple<int, int>(i + y, j + x);
                                        if (!res.Contains(partRes) && !addedCurrent.Contains(partRes) &&
                                            !addedNew.Contains(partRes))
                                        {
                                            res.Add(partRes);
                                            addedNew.Add(partRes);
                                        }
                                    }

                                    if (!(maxVal < tmpVal)) continue;
                                    var maxRes = FindMaximumArea(i + y, j + x);

                                    foreach (var point in res)
                                        _pointToMaximumAreaTable[point.Item1, point.Item2] = maxRes;

                                    return maxRes;
                                }
                }

                addedCurrent = addedNew;
                addedNew = new HashSet<Tuple<int, int>>();
            }

            return res;
        }
    }
}