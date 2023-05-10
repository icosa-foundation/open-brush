using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    [MoonSharpUserData]
    public class MultiPathApiWrapper : IPathApiWrapper
    {

        [MoonSharpHidden]
        public ScriptCoordSpace Space { get; set; }

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
            _MultiPath = new List<List<TrTransform>>{path._Path};
        }

        public static MultiPathApiWrapper New() => new MultiPathApiWrapper();
        public static MultiPathApiWrapper New(List<PathApiWrapper> pathList) => new MultiPathApiWrapper(pathList);

        public void Draw() => LuaApiMethods.DrawPath(this);
        public static MultiPathApiWrapper FromText(string text)
        {
            var builder = new TextToStrokes(ApiManager.Instance.TextFont);
            return new MultiPathApiWrapper(builder.Build(text));
        }

        public int count => _MultiPath?.Count ?? 0;
        public int pointCount => _MultiPath?.Sum(l => l.Count) ?? 0;

        public override string ToString()
        {
            return _MultiPath == null ? "Empty Path" : $"MultiPath with {count} paths and {pointCount} points";
        }

        public void Insert(PathApiWrapper path) => _MultiPath.Add(path._Path);
        public void InsertPoint(TrTransform transform) => _MultiPath[^1].Add(transform);

        public void Transform(TrTransform transform)
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

        public void Translate(Vector3 amount) => Transform(TrTransform.T(amount));
        public void Rotate(Quaternion amount) => Transform(TrTransform.R(amount));
        public void Scale(Vector3 scale)
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

        public void Resample(float spacing)
        {
            if (_MultiPath == null || spacing <= 0) return;
            for (var i = 0; i < _MultiPath.Count; i++)
            {
                _MultiPath[i] = PathApiWrapper.Resample(_MultiPath[i], spacing);
            }
        }

        public PathApiWrapper Join()
        {
            return new PathApiWrapper(_MultiPath.SelectMany(p => p).ToList());
        }

        public PathApiWrapper Longest()
        {
            var empty = new List<TrTransform>();
            var longest = _MultiPath.Aggregate(empty, (max, cur) => max.Count > cur.Count ? max : cur);
            return new PathApiWrapper(longest);
        }
    }
}
