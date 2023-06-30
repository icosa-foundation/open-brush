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

        [LuaDocsDescription(@"Creates a new empty MultiPath")]
        [LuaDocsExample(@"MultiPathApiWrapper:New()")]
        public static MultiPathApiWrapper New() => new MultiPathApiWrapper();

        [LuaDocsDescription(@"Creates a new MultiPath from a list of Paths")]
        [LuaDocsExample(@"MultiPathApiWrapper:New(pathList)")]
        [LuaDocsParameter(@"pathList", "A list of pathApiWrapper objects.")]
        public static MultiPathApiWrapper New(List<PathApiWrapper> pathList) => new MultiPathApiWrapper(pathList);

        [LuaDocsDescription("Draws this multipath using current settings")]
        [LuaDocsExample("myPaths:Draw()")]
        public void Draw() => LuaApiMethods.DrawPath(this);

        [LuaDocsDescription("Creates a new MultiPath from a text")]
        [LuaDocsExample(@"MultiPathApiWrapper.FromText('example')")]
        [LuaDocsParameter(@"text", "Input text to generate a path.")]
        public static MultiPathApiWrapper FromText(string text)
        {
            var builder = new TextToStrokes(ApiManager.Instance.TextFont);
            return new MultiPathApiWrapper(builder.Build(text));
        }

        [LuaDocsDescription("Gets the number of paths in the multipath")]
        public int count => _MultiPath?.Count ?? 0;

        [LuaDocsDescription("Gets the number of points in all paths in the multipath")]
        public int pointCount => _MultiPath?.Sum(l => l.Count) ?? 0;

        public override string ToString()
        {
            return _MultiPath == null ? "Empty Path" : $"MultiPath with {count} paths and {pointCount} points";
        }

        [LuaDocsDescription("Inserts a path at the end of the multipath")]
        [LuaDocsExample(@"myPaths:Insert(myPath)")]
        [LuaDocsParameter(@"path", "The path to be inserted.")]
        public void Insert(PathApiWrapper path) => _MultiPath.Add(path._Path);

        [LuaDocsDescription("Inserts a path at the specified index of the multipath")]
        [LuaDocsExample(@"myPaths:Insert(myPath, 3)")]
        [LuaDocsParameter(@"path", "The path to be inserted")]
        [LuaDocsParameter(@"index", "Inserts the new path at this position in the list of paths")]
        public void Insert(PathApiWrapper path, int index) => _MultiPath.Insert(index, path._Path);

        [LuaDocsDescription("Inserts a point at the end of the last path in the multipath")]
        [LuaDocsExample(@"myPaths:InsertPoint(myTransform)")]
        [LuaDocsParameter(@"transform", "The point to be inserted")]
        public void InsertPoint(TrTransform transform) => _MultiPath[^1].Add(transform);

        [LuaDocsDescription("Inserts a point at the specified index of the specified path")]
        [LuaDocsExample(@"myPaths:InsertPoint(myTransform, 3, 0)")]
        [LuaDocsParameter(@"transform", "The point to be inserted")]
        [LuaDocsParameter(@"pathIndex", "Index of the path to add the point to")]
        [LuaDocsParameter(@"pointIndex", "Inserts the point at this index in the list of points")]
        public void InsertPoint(TrTransform transform, int pathIndex, int pointIndex) => _MultiPath[pathIndex].Insert(pointIndex, transform);

        [LuaDocsDescription("Transforms the whole set of paths")]
        [LuaDocsExample(@"myPaths:TransformBy(myTransform)")]
        [LuaDocsParameter(@"transform", "A Transform specifying the translation, rotation and scale to apply")]
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

        [LuaDocsDescription("Translates the whole set of paths by a given amount")]
        [LuaDocsExample(@"myPaths:TranslateBy(Vector3.up:Multiply(4))")]
        [LuaDocsParameter(@"amount", "The amount to move the paths by")]
        public void TranslateBy(Vector3 amount) => TransformBy(TrTransform.T(amount));

        [LuaDocsDescription("Rotates the whole set of paths by a specified amount")]
        [LuaDocsExample(@"myPaths:RotateBy(Rotation.anticlockwise)")]
        [LuaDocsParameter(@"rotation", "The amount to rotate the paths by")]
        public void RotateBy(Quaternion rotation) => TransformBy(TrTransform.R(rotation));

        [LuaDocsDescription("Scales the whole set of paths by a specified factor")]
        [LuaDocsExample(@"myPaths:ScaleBy(vector3)")]
        [LuaDocsParameter(@"scale", "The amount to scale the paths by")]
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
        [LuaDocsExample(@"myPaths:Center()")]
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

        [LuaDocsDescription("Scales the whole multipath to fit inside a cube of given size at the origin")]
        [LuaDocsExample(@"myPaths:Normalize(1.5)")]
        [LuaDocsParameter(@"size", "The size of the cube to fit inside")]
        public void Normalize(float size = 1)
        {
            if (_MultiPath == null) return;
            var allPoints = _MultiPath.SelectMany(p => p).ToList();
            (Vector3 center, float unitScale) = PathApiWrapper._CalculateCenterAndScale(allPoints);
            size *= unitScale;

            // Apply the scale factor to each Vector3 in the input list
            for (var i = 0; i < _MultiPath.Count; i++)
            {
                var path = _MultiPath[i];
                for (var j = 0; j < _MultiPath[i].Count; j++)
                {
                    var tr = path[j];
                    tr.translation = (tr.translation - center) * size;
                    path[j] = tr;
                }
            }
        }

        [LuaDocsDescription("Resamples all paths with a specified spacing between points")]
        [LuaDocsExample(@"myPaths:Resample(0.2)")]
        [LuaDocsParameter(@"spacing", "The distance between each new point")]
        public void Resample(float spacing)
        {
            if (_MultiPath == null || spacing <= 0) return;
            for (var i = 0; i < _MultiPath.Count; i++)
            {
                _MultiPath[i] = PathApiWrapper._Resample(_MultiPath[i], spacing);
            }
        }

        [LuaDocsDescription("Joins all the paths in order connecting each end to the following start")]
        [LuaDocsExample(@"myPaths:Join()")]
        [LuaDocsReturnValue(@"A single path")]
        public PathApiWrapper Join()
        {
            return new PathApiWrapper(_MultiPath.SelectMany(p => p).ToList());
        }

        [LuaDocsDescription("Returns the longest path in the multipath")]
        [LuaDocsExample(@"path = myPaths:Longest()")]
        [LuaDocsReturnValue(@"The path with the most control points")]
        public PathApiWrapper Longest()
        {
            var empty = new List<TrTransform>();
            var longest = _MultiPath.Aggregate(empty, (max, cur) => max.Count > cur.Count ? max : cur);
            return new PathApiWrapper(longest);
        }
    }
}
