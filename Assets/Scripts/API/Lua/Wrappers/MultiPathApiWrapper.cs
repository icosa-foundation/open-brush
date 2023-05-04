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
        public List<TrTransform> AsSingleTrList() => _MultiPath.SelectMany(p => p._Path).ToList();

        [MoonSharpHidden]
        public List<List<TrTransform>> AsMultiTrList() => _MultiPath.Select(p => p._Path).ToList();

        [MoonSharpHidden]
        public List<PathApiWrapper> _MultiPath;

        public MultiPathApiWrapper()
        {
            _MultiPath = new List<PathApiWrapper>();
        }

        public MultiPathApiWrapper(IEnumerable<PathApiWrapper> multipath)
        {
            _MultiPath = multipath.ToList();
        }

        public MultiPathApiWrapper(PathApiWrapper path)
        {
            _MultiPath = new List<PathApiWrapper>{path};
        }

        public int count => _MultiPath.Count;
        public int pointCount => _MultiPath.Sum(l => l._Path.Count);
        public void Insert(PathApiWrapper path) => _MultiPath.Add(path);
        public void InsertPoint(TrTransform transform) => _MultiPath.Last().Insert(transform);

        public void Transform(TrTransform transform)
        {
            for (var i = 0; i < _MultiPath.Count; i++)
            {
                _MultiPath[i].Transform(transform);
            }
        }

        public void Translate(Vector3 amount) => Transform(TrTransform.T(amount));
        public void Rotate(Quaternion amount) => Transform(TrTransform.R(amount));
        public void Scale(Vector3 amount)
        {
            for (var i = 0; i < _MultiPath.Count; i++)
            {
                _MultiPath[i].Scale(amount);
            }
        }

        public void Center()
        {
            var allPoints = _MultiPath.SelectMany(p => p._Path).ToList();
            (Vector3 center, float _) = PathApiWrapper._CalculateCenterAndScale(allPoints);

            // Apply the scale factor to each Vector3 in the input list
            for (var i = 0; i < _MultiPath.Count; i++)
            {
                for (var j = 0; j < _MultiPath[i].count; j++)
                {
                    var tr = _MultiPath[i]._Path[j];
                    tr.translation = tr.translation - center;
                    _MultiPath[i]._Path[j] = tr;
                }
            }
        }

        public void Normalize(float scale = 1)
        {
            if (_MultiPath == null) return;
            var allPoints = _MultiPath.SelectMany(p => p._Path).ToList();
            (Vector3 center, float unitScale) = PathApiWrapper._CalculateCenterAndScale(allPoints);
            scale *= unitScale;

            // Apply the scale factor to each Vector3 in the input list
            for (var i = 0; i < _MultiPath.Count; i++)
            {
                for (var j = 0; j < _MultiPath[i]._Path.Count; j++)
                {
                    var tr = _MultiPath[i]._Path[j];
                    tr.translation = (tr.translation - center) * scale;
                    _MultiPath[i]._Path[j] = tr;
                }
            }
        }

        public void Resample(float spacing)
        {
            if (_MultiPath == null || spacing <= 0) return;
            foreach (var path in _MultiPath)
            {
                path.Resample(spacing);
            }
        }

        public PathApiWrapper Join(List<PathApiWrapper> paths)
        {
            return new PathApiWrapper(_MultiPath.SelectMany(p => p._Path).ToList());
        }

        public PathApiWrapper Longest(List<PathApiWrapper> paths)
        {
            var empty = new PathApiWrapper();
            return _MultiPath.Aggregate(empty, (max, cur) => max.count > cur._Path.Count ? max : cur);
        }
    }
}
