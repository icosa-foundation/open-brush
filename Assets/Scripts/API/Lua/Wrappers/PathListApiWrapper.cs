using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    [LuaDocsDescription("Multiple disconnected path segments")]
    [MoonSharpUserData]
    public class PathListApiWrapper : IPathApiWrapper
    {

        [MoonSharpHidden]
        public ScriptCoordSpace _Space { get; set; }

        [MoonSharpHidden]
        public List<TrTransform> AsSingleTrList() => _PathList.SelectMany(p => p).ToList();

        [MoonSharpHidden]
        public List<List<TrTransform>> AsMultiTrList() => _PathList;

        [MoonSharpHidden]
        public List<List<TrTransform>> _PathList;

        [MoonSharpHidden]
        public List<Color> _Colors;

        public PathListApiWrapper()
        {
            _PathList = new List<List<TrTransform>>();
        }

        public PathListApiWrapper(IEnumerable<PathApiWrapper> paths, List<Color> colors = null)
        {
            _PathList = paths.Select(p => p._Path).ToList();
            _Colors = colors;
        }

        public PathListApiWrapper(IEnumerable<IEnumerable<TrTransform>> nestedTrs, List<Color> colors = null)
        {
            _PathList = nestedTrs.Select(trList => trList.ToList()).ToList();
            _Colors = colors;
        }

        public PathListApiWrapper(PathApiWrapper path)
        {
            _PathList = new List<List<TrTransform>> { path._Path };
        }

        [LuaDocsDescription(@"Creates a new empty PathList")]
        [LuaDocsExample(@"PathList:New()")]
        public static PathListApiWrapper New() => new PathListApiWrapper();

        [LuaDocsDescription(@"Creates a new PathList from a list of Paths")]
        [LuaDocsExample(@"PathList:New(pathList)")]
        [LuaDocsParameter(@"pathList", "A list of Paths .")]
        public static PathListApiWrapper New(List<PathApiWrapper> pathList) => new PathListApiWrapper(pathList);

        [LuaDocsDescription("Draws this PathList using current settings")]
        [LuaDocsExample("myPaths:Draw()")]
        public void Draw() => LuaApiMethods.DrawPaths(this);

        [LuaDocsDescription("Creates a new PathList from a text")]
        [LuaDocsExample(@"PathList.FromText('example')")]
        [LuaDocsParameter(@"text", "Input text to generate a path.")]
        public static PathListApiWrapper FromText(string text)
        {
            TextToStrokes builder = new TextToStrokes(ApiManager.Instance.TextFont);
            return new PathListApiWrapper(builder.Build(text));
        }

        [LuaDocsDescription("Gets the number of paths in the PathList")]
        public int count => _PathList?.Count ?? 0;

        [LuaDocsDescription("Gets the number of points in all paths in the PathList")]
        public int pointCount => _PathList?.Sum(l => l.Count) ?? 0;

        public override string ToString()
        {
            return _PathList == null ? "Empty Path" : $"PathList with {count} paths and {pointCount} points";
        }

        [LuaDocsDescription("Inserts a path at the end of the PathList")]
        [LuaDocsExample(@"myPaths:Insert(myPath)")]
        [LuaDocsParameter(@"path", "The path to be inserted.")]
        public void Insert(PathApiWrapper path) => _PathList.Add(path._Path);

        [LuaDocsDescription("Inserts a path at the specified index of the PathList")]
        [LuaDocsExample(@"myPaths:Insert(myPath, 3)")]
        [LuaDocsParameter(@"path", "The path to be inserted")]
        [LuaDocsParameter(@"index", "Inserts the new path at this position in the list of paths")]
        public void Insert(PathApiWrapper path, int index) => _PathList.Insert(index, path._Path);

        [LuaDocsDescription("Inserts a point at the end of the last path in the PathList")]
        [LuaDocsExample(@"myPaths:InsertPoint(myTransform)")]
        [LuaDocsParameter(@"transform", "The point to be inserted")]
        public void InsertPoint(TrTransform transform) => _PathList[^1].Add(transform);

        [LuaDocsDescription("Inserts a point at the specified index of the specified path")]
        [LuaDocsExample(@"myPaths:InsertPoint(myTransform, 3, 0)")]
        [LuaDocsParameter(@"transform", "The point to be inserted")]
        [LuaDocsParameter(@"pathIndex", "Index of the path to add the point to")]
        [LuaDocsParameter(@"pointIndex", "Inserts the point at this index in the list of points")]
        public void InsertPoint(TrTransform transform, int pathIndex, int pointIndex) => _PathList[pathIndex].Insert(pointIndex, transform);

        [LuaDocsDescription("Transforms the whole set of paths")]
        [LuaDocsExample(@"myPaths:TransformBy(myTransform)")]
        [LuaDocsParameter(@"transform", "A Transform specifying the translation, rotation and scale to apply")]
        public void TransformBy(TrTransform transform)
        {
            for (var i = 0; i < _PathList.Count; i++)
            {
                var path = _PathList[i];
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
            for (var i = 0; i < _PathList.Count; i++)
            {
                var path = _PathList[i];
                // Supports non-uniform scaling
                for (var j = 0; j < path.Count; j++)
                {
                    path[j].translation.Scale(scale);
                }
            }
        }

        [LuaDocsDescription("Scales the whole set of paths by a specified factor")]
        [LuaDocsExample(@"myPaths:ScaleBy(float)")]
        [LuaDocsParameter(@"scale", "The amount to scale the paths by")]
        public void ScaleBy(float scale)
        {
            for (var i = 0; i < _PathList.Count; i++)
            {
                var path = _PathList[i];
                // Supports non-uniform scaling
                for (var j = 0; j < path.Count; j++)
                {
                    var tr = path[j];
                    tr.translation *= scale;
                }
            }
        }

        [LuaDocsDescription("Offsets all points on the path so that their common center is at the origin")]
        [LuaDocsExample(@"myPaths:Center()")]
        public void Center()
        {
            var allPoints = _PathList.SelectMany(p => p).ToList();
            (Vector3 center, float _) = PathApiWrapper._CalculateCenterAndScale(allPoints);

            // Apply the scale factor to each Vector3 in the input list
            for (var i = 0; i < _PathList.Count; i++)
            {
                var path = _PathList[i];
                for (var j = 0; j < path.Count; j++)
                {
                    var tr = path[j];
                    tr.translation -= center;
                    path[j] = tr;
                }
            }
        }

        [LuaDocsDescription("Scales the whole PathList to fit inside a cube of given size at the origin")]
        [LuaDocsExample(@"myPaths:Normalize(1.5)")]
        [LuaDocsParameter(@"size", "The size of the cube to fit inside")]
        public void Normalize(float size = 1)
        {
            if (_PathList == null) return;
            var allPoints = _PathList.SelectMany(p => p).ToList();
            (Vector3 center, float unitScale) = PathApiWrapper._CalculateCenterAndScale(allPoints);
            size *= unitScale;

            // Apply the scale factor to each Vector3 in the input list
            for (var i = 0; i < _PathList.Count; i++)
            {
                var path = _PathList[i];
                for (var j = 0; j < _PathList[i].Count; j++)
                {
                    var tr = path[j];
                    tr.translation = (tr.translation - center) * size;
                    path[j] = tr;
                }
            }
        }

        [LuaDocsDescription("Resamples all paths with a specified spacing between points")]
        [LuaDocsExample(@"myPaths:SampleByDistance(0.2)")]
        [LuaDocsParameter(@"spacing", "The distance between each new point")]
        [LuaDocsReturnValue(@"The new PathList")]
        public void SampleByDistance(float spacing)
        {
            if (_PathList == null || spacing <= 0) return;
            for (var i = 0; i < _PathList.Count; i++)
            {
                _PathList[i] = PathApiWrapper._SampleByDistance(_PathList[i], spacing);
            }
        }

        [LuaDocsDescription("Resamples each path evenly into a specified number of points")]
        [LuaDocsExample(@"myPaths:SampleByCount(4)")]
        [LuaDocsParameter(@"count", "Number of points in the new path")]
        [LuaDocsReturnValue(@"The new PathList")]
        public void SampleByCount(int count)
        {
            if (_PathList == null || count <= 0) return;
            for (var i = 0; i < _PathList.Count; i++)
            {
                _PathList[i] = PathApiWrapper._SampleByCount(_PathList[i], count);
            }
        }

        [LuaDocsDescription("For each path in the list subdivide it's path segment into the specified number of parts")]
        [LuaDocsExample(@"myPaths:SubdivideSegments(4)")]
        [LuaDocsParameter(@"parts", "Number of parts to subdivide each path segment into")]
        [LuaDocsReturnValue(@"The new PathList")]
        public void SubdivideSegments(int parts)
        {
            if (_PathList == null || count <= 0) return;
            for (var i = 0; i < _PathList.Count; i++)
            {
                _PathList[i] = PathApiWrapper._SubdivideSegments(_PathList[i], parts);
            }
        }

        [LuaDocsDescription("Joins all the paths in order connecting each end to the following start")]
        [LuaDocsExample(@"myPaths:Join()")]
        [LuaDocsReturnValue(@"A single path")]
        public PathApiWrapper Join()
        {
            return new PathApiWrapper(_PathList.SelectMany(p => p).ToList());
        }

        [LuaDocsDescription("Returns the longest path in the PathList")]
        [LuaDocsExample(@"path = myPaths:Longest()")]
        [LuaDocsReturnValue(@"The path with the most control points")]
        public PathApiWrapper Longest()
        {
            var empty = new List<TrTransform>();
            var longest = _PathList.Aggregate(empty, (max, cur) => max.Count > cur.Count ? max : cur);
            return new PathApiWrapper(longest);
        }
    }
}
