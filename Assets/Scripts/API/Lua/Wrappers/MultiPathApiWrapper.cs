using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    [LuaDocsDescription("Multiple disconnected path segments")]
    [MoonSharpUserData]
    public class MultiPathApiWrapper : IPathApiWrapper
    {

        [MoonSharpHidden]
        public ScriptCoordSpace _Space { get; set; }

        [MoonSharpHidden]
        public List<TrTransform> AsSingleTrList() => _MultiPath.SelectMany(p => p).ToList();

        [MoonSharpHidden]
        public List<List<TrTransform>> AsMultiTrList() => _MultiPath;

        [MoonSharpHidden]
        public List<List<TrTransform>> _MultiPath;

        [MoonSharpHidden]
        public List<Color> _Colors;

        public MultiPathApiWrapper()
        {
            _MultiPath = new List<List<TrTransform>>();
        }

        public MultiPathApiWrapper(IEnumerable<PathApiWrapper> multipath, List<Color> colors = null)
        {
            _MultiPath = multipath.Select(p => p._Path).ToList();
            _Colors = colors;
        }

        public MultiPathApiWrapper(IEnumerable<IEnumerable<TrTransform>> nestedTrs, List<Color> colors = null)
        {
            _MultiPath = nestedTrs.Select(trList => trList.ToList()).ToList();
            _Colors = colors;
        }

        public MultiPathApiWrapper(PathApiWrapper path)
        {
            _MultiPath = new List<List<TrTransform>> { path._Path };
        }

        public static MultiPathApiWrapper New() => new MultiPathApiWrapper();
        public static MultiPathApiWrapper New(List<PathApiWrapper> pathList) => new MultiPathApiWrapper(pathList);

        [LuaDocsDescription("Draws this path as a brush stroke using current settings")]
        public void Draw() => LuaApiMethods.DrawPath(this);

        [LuaDocsDescription("Creates a new MultiPath that draws the shape of the given text. Use App:SetFont to set the letter shapes")]
        public static MultiPathApiWrapper FromText(string text)
        {
            var builder = new TextToStrokes(ApiManager.Instance.TextFont);
            return new MultiPathApiWrapper(builder.Build(text));
        }


        [LuaDocsDescription("Returns number of paths contained in the multipath")]
        public int count => _MultiPath?.Count ?? 0;

        [LuaDocsDescription("Returns the number of points in all paths in the multipath")]
        public int pointCount => _MultiPath?.Sum(l => l.Count) ?? 0;

        public override string ToString()
        {
            return _MultiPath == null ? "Empty Path" : $"MultiPath with {count} paths and {pointCount} points";
        }

        [LuaDocsDescription("Inserts the given path at the end of the multipath")]
        public void Insert(PathApiWrapper path) => _MultiPath.Add(path._Path);

        [LuaDocsDescription("Inserts a new path at the given index")]
        public void Insert(PathApiWrapper path, int index) => _MultiPath.Insert(index, path._Path);

        [LuaDocsDescription("Inserts a point at the end of the last path in the multipath")]
        public void InsertPoint(TrTransform transform) => _MultiPath[^1].Add(transform);

        [LuaDocsDescription("Inserts a point at the given index of the given path")]
        public void InsertPoint(TrTransform transform, int pathIndex, int pointIndex) => _MultiPath[pathIndex].Insert(pointIndex, transform);

        [LuaDocsDescription("Transforms all paths in the multipath")]
        public void TransformBy(TrTransform transform)
        {
            for (var i = 0; i < _MultiPath.Count; i++)
            {
                var path = _MultiPath[i];
                for (int j = 0; j < path.Count; j++)
                {
                    path[j] = transform * path[j];
                }
            }
        }

        [LuaDocsDescription("Translates all paths by a given offset")]
        public void TranslateBy(Vector3 amount) => TransformBy(TrTransform.T(amount));

        [LuaDocsDescription("Rotates all paths by a specified amount around the origin")]
        public void RotateBy(Quaternion amount) => TransformBy(TrTransform.R(amount));

        [LuaDocsDescription("Scales all paths by a specified amount towards or away from the origin")]
        public void ScaleBy(Vector3 scale)
        {
            for (var i = 0; i < _MultiPath.Count; i++)
            {
                var path = _MultiPath[i];
                // Supports non-uniform scaling
                for (var j = 0; j < path.Count; j++)
                {
                    path[j].translation.Scale(scale);
                }
            }
        }

        [LuaDocsDescription("Offsets all points on the path so that their common center is at the origin")]
        public void Center()
        {
            var allPoints = _MultiPath.SelectMany(p => p).ToList();
            (Vector3 center, float _) = PathApiWrapper._CalculateCenterAndScale(allPoints);

            // Apply the scale factor to each Vector3 in the input list
            for (var i = 0; i < _MultiPath.Count; i++)
            {
                var path = _MultiPath[i];
                for (var j = 0; j < path.Count; j++)
                {
                    var tr = path[j];
                    tr.translation -= center;
                    path[j] = tr;
                }
            }
        }

        [LuaDocsDescription("Scales all paths to fit inside a 1x1x1 cube at the origin")]
        public void Normalize(float scale = 1)
        {
            if (_MultiPath == null) return;
            var allPoints = _MultiPath.SelectMany(p => p).ToList();
            (Vector3 center, float unitScale) = PathApiWrapper._CalculateCenterAndScale(allPoints);
            scale *= unitScale;

            // Apply the scale factor to each Vector3 in the input list
            for (var i = 0; i < _MultiPath.Count; i++)
            {
                var path = _MultiPath[i];
                for (var j = 0; j < _MultiPath[i].Count; j++)
                {
                    var tr = path[j];
                    tr.translation = (tr.translation - center) * scale;
                    path[j] = tr;
                }
            }
        }

        [LuaDocsDescription("Resamples the multipath at a specified spacing")]
        public void Resample(float spacing)
        {
            if (_MultiPath == null || spacing <= 0) return;
            for (var i = 0; i < _MultiPath.Count; i++)
            {
                _MultiPath[i] = PathApiWrapper.Resample(_MultiPath[i], spacing);
            }
        }

        [LuaDocsDescription("Joins all the paths in the multipath and returns a single Path")]
        public PathApiWrapper Join()
        {
            return new PathApiWrapper(_MultiPath.SelectMany(p => p).ToList());
        }

        [LuaDocsDescription("Returns the longest path in the multipath")]
        public PathApiWrapper Longest()
        {
            var empty = new List<TrTransform>();
            var longest = _MultiPath.Aggregate(empty, (max, cur) => max.Count > cur.Count ? max : cur);
            return new PathApiWrapper(longest);
        }
    }
}
