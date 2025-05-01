using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{

    [LuaDocsDescription("A set of Vector2 points forming a 2D path")]
    [MoonSharpUserData]
    public class Path2dApiWrapper : IPathApiWrapper
    {

        [MoonSharpHidden]
        public ScriptCoordSpace _Space { get; set; }

        [MoonSharpHidden]
        public List<TrTransform> AsSingleTrList() => _Path2d.Select(v => TrTransform.T(new Vector3(v.x, v.y, 0))).ToList();

        [MoonSharpHidden]
        public List<List<TrTransform>> AsMultiTrList() => new List<List<TrTransform>> { AsSingleTrList() };

        public enum Axis { X, Y, Z }

        [MoonSharpHidden]
        public List<Vector2> _Path2d;

        private List<Vector3> _DirectionVectors;
        private List<Vector3> _Normals;
        private List<Vector3> _Tangents;

        public Path2dApiWrapper()
        {
            _Path2d = new List<Vector2>();
        }

        public Path2dApiWrapper(params Vector2[] transformList)
        {
            _Path2d = transformList.ToList();
        }

        public Path2dApiWrapper(List<Vector2> transformList)
        {
            _Path2d = transformList;
        }

        public Path2dApiWrapper(List<Vector3> positionList)
        {
            int count = positionList.Count;
            var transformList = new List<Vector2>(count);
            for (int i = 0; i < count; i++)
            {
                transformList.Add(positionList[i]);
            }
            _Path2d = transformList;
        }

        [LuaDocsDescription("Creates a new empty 2d Path")]
        [LuaDocsExample("myPath = Path2d:New()")]
        public static Path2dApiWrapper New() => new Path2dApiWrapper();

        [LuaDocsDescription("Creates a 2d path from a list of Vector2 points")]
        [LuaDocsExample("myPath = Path2d:New({point1, point2, point3})")]
        [LuaDocsParameter("positionList", "The list of points")]
        public static Path2dApiWrapper New(List<Vector2> positionList) => new Path2dApiWrapper(positionList);

        [LuaDocsDescription("Creates a path from a list of Vector3 points")]
        [LuaDocsExample("myPath = Path:New({point1, point2, point3})")]
        [LuaDocsParameter("positionList", "The list of points")]
        public static Path2dApiWrapper New(List<Vector3> positionList) => new Path2dApiWrapper(positionList);

        [LuaDocsDescription("Returns the number of points in this path")]
        public int count => _Path2d?.Count ?? 0;

        [LuaDocsDescription("Returns the point at the specified index")]
        public TransformApiWrapper this[int index] => new TransformApiWrapper(_Path2d[index]);

        [LuaDocsDescription("Returns the last point in this path")]
        public TransformApiWrapper last => new TransformApiWrapper(_Path2d[^1]);

        [LuaDocsDescription("Inserts a new point at the end of the path")]
        [LuaDocsExample("myPath:Insert(Transform:New(pos, rot)")]
        [LuaDocsParameter("point", "The point to be inserted at the end of the path")]
        public void Insert(Vector2 point) => _Path2d.Add(point);

        [LuaDocsDescription("Inserts a new point at the specified index")]
        [LuaDocsExample("myPath:Insert(transform, index)")]
        [LuaDocsParameter("point", "The point to be inserted")]
        [LuaDocsParameter("index", "The index at which to insert the point")]
        public void Insert(Vector2 point, int index) => _Path2d.Insert(index, point);

        [LuaDocsDescription("Converts the 2D path to a 3D path on the YZ plane (i.e. with all x values set to 0)")]
        [LuaDocsExample("my3dPath = my2dPath:OnX()")]
        [LuaDocsReturnValue("A 3D Path based on the input but with all x as 0: (0, inX, inY)")]
        public PathApiWrapper OnX() => PathApiWrapper.New(_Path2d.Select(v => new Vector3(0, v.x, v.y)).ToList());

        [LuaDocsDescription("Converts the 2D path to a 3D path on the XZ plane (i.e. with all y values set to 0)")]
        [LuaDocsExample("my3dPath = my2dPath:OnY()")]
        [LuaDocsReturnValue("A 3D Path based on the input but with all y as 0: (inX, 0, inY)")]
        public PathApiWrapper OnY() => PathApiWrapper.New(_Path2d.Select(v => new Vector3(v.x, 0, v.y)).ToList());

        [LuaDocsDescription("Converts the 2D path to a 3D path on the XY plane (i.e. with all z values set to 0)")]
        [LuaDocsExample("my3dPath = my2dPath:OnZ()")]
        [LuaDocsReturnValue("A 3D Path based on the input but with all z as 0: (inX, inY, 0)")]
        public PathApiWrapper OnZ() => PathApiWrapper.New(_Path2d.Select(v => new Vector3(v.x, v.y, 0)).ToList());

        [LuaDocsDescription("Transforms all points in the path by the specific amount")]
        [LuaDocsExample("myPath:TransformBy(transform)")]
        [LuaDocsParameter("transform", "The transform to be applied to all points in the path")]
        public void TransformBy(TrTransform transform)
        {
            for (int i = 0; i < _Path2d.Count; i++)
            {
                _Path2d[i] = transform * _Path2d[i];
            }
        }

        [LuaDocsDescription("Changes the position of all points in the path by a given amount")]
        [LuaDocsExample("myPath:TranslateBy(Vector3:up)")]
        [LuaDocsParameter("amount", "The distance to move the points")]
        public void TranslateBy(Vector2 amount) => TransformBy(TrTransform.T(amount));

        [LuaDocsDescription("Rotates all points in the path around the origin by a given amount")]
        [LuaDocsExample("myPath:RotateBy(Rotation.New(45, 0, 0)")]
        [LuaDocsParameter("amount", "The amount by which to rotate the path")]
        public void RotateBy(Quaternion amount) => TransformBy(TrTransform.R(amount));

        [LuaDocsDescription("Scales the path")]
        [LuaDocsExample("myPath:ScaleBy(2)")]
        [LuaDocsParameter("scale", "The scaling factor to apply to the path")]
        public void ScaleBy(float scale)
        {
            ScaleBy(new Vector2(scale, scale));
        }

        [LuaDocsDescription("Scales the path")]
        [LuaDocsExample("myPath:ScaleBy(2, 1)")]
        [LuaDocsParameter("x", "The x scaling factor to apply to the path")]
        [LuaDocsParameter("y", "The y scaling factor to apply to the path")]
        public void ScaleBy(float x, float y)
        {
            ScaleBy(new Vector2(x, y));
        }

        [LuaDocsDescription("Scales the path")]
        [LuaDocsExample("myPath:ScaleBy(myVector2")]
        [LuaDocsParameter("scale", "The scaling factor to apply to the path")]
        public void ScaleBy(Vector2 scale)
        {
            // Supports non-uniform scaling
            for (var i = 0; i < _Path2d.Count; i++)
            {
                var tr = _Path2d[i];
                tr = new Vector2(
                    tr.x * scale.x,
                    tr.y * scale.y
                );
                _Path2d[i] = tr;
            }
        }

        [LuaDocsDescription("Moves all points on the path so that their common center is the origin")]
        [LuaDocsExample("myPath:Center()")]
        public void Center()
        {
            (Vector2 center, float _) = _CalculateCenterAndScale(_Path2d);

            // Apply the scale factor to each Vector2 in the input list
            for (var i = 0; i < _Path2d.Count; i++)
            {
                var v = _Path2d[i];
                v -= center;
                _Path2d[i] = v;
            }
        }

        [LuaDocsDescription("Reorders the points so that point at the given index is shifted to be the first point")]
        [LuaDocsExample("myPath:StartingFrom(3)")]
        [LuaDocsParameter(@"index", "The index of the point to make the new first point")]
        public void StartingFrom(int index)
        {
            if (_Path2d == null) return;
            _Path2d = _Path2d.Skip(index).Concat(_Path2d.Take(index)).ToList();
        }

        [LuaDocsDescription("Returns the index of the point closest to the given position")]
        [LuaDocsExample("myPath:FindClosest(Vector3:New(10, 2, 4)")]
        [LuaDocsParameter("point", "The 3D position that we are seeking the closest to")]
        public int FindClosest(Vector2 point)
        {
            if (_Path2d == null) return 0;
            return _Path2d.Select((x, i) => new { i, x }).Aggregate(
                (acc, v) => (v.x - point).sqrMagnitude < (acc.x - point).sqrMagnitude ? v : acc
            ).i;
        }

        [LuaDocsDescription("Returns the index of the point with the smallest X value")]
        [LuaDocsExample("myPath:FindMinimumX()")]
        public int FindMinimumX() => _FindMinimum(Axis.X);

        [LuaDocsDescription("Returns the index of the point with the smallest Y value")]
        [LuaDocsExample("myPath:FindMinimumY()")]
        public int FindMinimumY() => _FindMinimum(Axis.Y);

        [LuaDocsDescription("Returns the index of the point with the biggest X value")]
        [LuaDocsExample("myPath:FindMaximumX()")]
        public int FindMaximumX() => _FindMaximum(Axis.X);

        [LuaDocsDescription("Returns the index of the point with the biggest Y value")]
        [LuaDocsExample("myPath:FindMaximumY()")]
        public int FindMaximumY() => _FindMaximum(Axis.Y);

        [MoonSharpHidden]
        public int _FindMinimum(Axis axis)
        {
            if (_Path2d == null) return 0;
            return _Path2d
                .Select((v, i) => (translation: v[(int)axis], index: i))
                .Aggregate((a, b) => a.translation < b.translation ? a : b)
                .index;
        }

        [MoonSharpHidden]
        public int _FindMaximum(Axis axis)
        {
            return _Path2d
                .Select((v, i) => (translation: v[(int)axis], index: i))
                .Aggregate((a, b) => a.translation > b.translation ? a : b)
                .index;
        }

        [LuaDocsDescription("Scales and shifts all points so that they fit in a 1 unit square at the origin")]
        [LuaDocsExample("myPath:Normalize(size)")]
        [LuaDocsParameter("size", "The size of the square to fit the path into")]
        public void Normalize(float size = 1)
        {
            if (_Path2d == null) return;
            (Vector2 center, float unitScale) = _CalculateCenterAndScale(_Path2d);
            size *= unitScale;

            // Apply the scale factor to each Vector2 in the input list
            for (var i = 0; i < _Path2d.Count; i++)
            {
                var v = _Path2d[i];
                v = (v - center) * size;
                _Path2d[i] = v;
            }
        }

        [MoonSharpHidden]
        public static (Vector2 center, float scale) _CalculateCenterAndScale(List<Vector2> path)
        {
            // Find the min and max values for each axis
            float minX = path.Min(v => v.x);
            float minY = path.Min(v => v.y);

            float maxX = path.Max(v => v.x);
            float maxY = path.Max(v => v.y);

            // Compute the range for each axis
            float rangeX = maxX - minX;
            float rangeY = maxY - minY;

            // Calculate the center of the original path
            Vector2 center = new Vector2(
                (minX + maxX) / 2,
                (minY + maxY) / 2
            );

            // Find the largest range to maintain the aspect ratio
            float largestRange = Mathf.Max(rangeX, rangeY);

            // Don't scale if the largest range is zero to avoid division by zero
            float scale = largestRange == 0 ? 1 : 1 / largestRange;

            return (center, scale);
        }

        [LuaDocsDescription(@"Generates a regular polygon path")]
        [LuaDocsExample(@"myPath = Path2d:Polygon(6)")]
        [LuaDocsParameter("sides", "The number of sides for the polygon")]
        [LuaDocsReturnValue(@"The new path")]
        public static Path2dApiWrapper Polygon(int sides)
        {
            var path = new List<Vector2>(sides);
            for (float i = 0; i <= sides; i++)
            {
                var theta = Mathf.PI * (i / sides) * 2f;
                theta += Mathf.Deg2Rad;
                var point = new Vector3(
                    Mathf.Cos(theta),
                    Mathf.Sin(theta),
                    0
                );
                point = ApiManager.Instance.BrushRotation * point;
                path.Add(point);
            }
            return new Path2dApiWrapper(path);
        }

        [LuaDocsDescription(@"Resamples the path at a specified spacing")]
        [LuaDocsExample(@"myPath:Resample(spacing)")]
        [LuaDocsParameter(@"spacing", "The space between points in the new pat")]
        [LuaDocsReturnValue(@"The resampled path")]
        public void Resample(float spacing)
        {
            if (_Path2d == null || _Path2d.Count < 2 || spacing <= 0) return;
            List<Vector2> resampledPath = new List<Vector2>();
            resampledPath.Add(_Path2d[0]);

            float accumulatedDistance = 0f;
            int originalPathIndex = 0;
            var startPoint = _Path2d[0];

            while (originalPathIndex < _Path2d.Count - 1)
            {
                var endPoint = _Path2d[originalPathIndex + 1];
                float segmentDistance = Vector2.Distance(startPoint, endPoint);

                if (accumulatedDistance + segmentDistance >= spacing)
                {
                    float interpolationFactor = (spacing - accumulatedDistance) / segmentDistance;
                    Vector2 newPoint = Vector2.Lerp(startPoint, endPoint, interpolationFactor);
                    resampledPath.Add(newPoint);
                    startPoint = newPoint;
                    accumulatedDistance = 0f;
                }
                else
                {
                    accumulatedDistance += segmentDistance;
                    startPoint = endPoint;
                    originalPathIndex++;
                }
            }
            resampledPath.Add(_Path2d[^1]);
            _Path2d = resampledPath;
        }
    }
}
