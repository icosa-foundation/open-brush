using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    public interface IPathApiWrapper
    {
        public ScriptCoordSpace _Space { get; set; }
        public List<TrTransform> AsSingleTrList();
        public List<List<TrTransform>> AsMultiTrList();

    }

    [LuaDocsDescription("A list of transforms that usually represents a path in 3D space. These form the basis for brush strokes and camera paths")]
    [MoonSharpUserData]
    public class PathApiWrapper : IPathApiWrapper
    {

        [MoonSharpHidden]
        public ScriptCoordSpace _Space { get; set; }

        [MoonSharpHidden]
        public List<TrTransform> AsSingleTrList() => _Path;

        [MoonSharpHidden]
        public List<List<TrTransform>> AsMultiTrList() => new List<List<TrTransform>> { _Path };

        public enum Axis { X, Y, Z }

        [MoonSharpHidden]
        public List<TrTransform> _Path;

        private List<Vector3> _DirectionVectors;
        private List<Vector3> _Normals;
        private List<Vector3> _Tangents;

        public PathApiWrapper()
        {
            _Path = new List<TrTransform>();
        }

        public PathApiWrapper(params TrTransform[] transformList)
        {
            _Path = transformList.ToList();
        }

        public PathApiWrapper(List<TrTransform> transformList)
        {
            _Path = transformList;
        }

        public PathApiWrapper(List<Vector3> positionList)
        {
            int count = positionList.Count;
            var transformList = new List<TrTransform>(count);
            for (int i = 0; i < count; i++)
            {
                transformList.Add(TrTransform.T(positionList[i]));
            }
            _Path = transformList;
        }

        [LuaDocsDescription("Creates a new empty Path")]
        [LuaDocsExample("myPath = Path:New()")]
        public static PathApiWrapper New() => new PathApiWrapper();

        [LuaDocsDescription("Creates a path from a list of Transforms")]
        [LuaDocsExample("myPath = Path:New({transform1, transform2, transform3})")]
        [LuaDocsParameter("transformList", "The list of transforms")]
        public static PathApiWrapper New(List<TrTransform> transformList) => new PathApiWrapper(transformList);

        [LuaDocsDescription("Creates a path from a list of Vector3 positions")]
        [LuaDocsExample("myPath = Path:New({position1, position2, position3})")]
        [LuaDocsParameter("positionList", "The list of positions")]
        public static PathApiWrapper New(List<Vector3> positionList) => new PathApiWrapper(positionList);

        public override string ToString()
        {
            return _Path == null ? "Empty Path" : $"Path with {count} points)";
        }

        [MoonSharpHidden]
        private void _CalculateVectors()
        {
            _Normals = new List<Vector3>(_Path.Count);
            _Tangents = new List<Vector3>(_Path.Count);
            _DirectionVectors = new List<Vector3>(_Path.Count);
            var nTangent = _CalculatePathVector(_Path, 0);
            Vector3 preferredRight = Vector3.Cross(_Path[0].rotation * Vector3.forward, nTangent);
            for (var i = 0; i < _Path.Count; i++)
            {
                Vector3 pathVector = _CalculatePathVector(_Path, i);
                BaseBrushScript.ComputeSurfaceFrameNew(preferredRight, pathVector, _Path[i].rotation, out Vector3 nright, out Vector3 nNormal);
                preferredRight = nright;

                _DirectionVectors.Add(pathVector);
                _Normals.Add(nNormal);
                _Tangents.Add(nright);
            }
        }

        private static Vector3 _CalculatePathVector(List<TrTransform> path, int i)
        {
            if (i == 0 && path.Count <= 1) { return path[i].rotation * Vector3.forward; } // A path with only one point
            if (i == 0) { return (path[i + 1].translation - path[i].translation).normalized; }
            if (i + 1 >= path.Count) { return (path[i].translation - path[i - 1].translation).normalized; }

            Vector3 toPrevious = (path[i].translation - path[i - 1].translation).normalized;
            Vector3 toNext = (path[i + 1].translation - path[i].translation).normalized;
            return ((toPrevious + toNext) / 2).normalized;
        }

        [LuaDocsDescription("Returns a vector representing the direction of the path at the given point")]
        [LuaDocsExample("myPath:GetDirection(3)")]
        [LuaDocsParameter("index", "Index of control point to use")]
        public Vector3 GetDirection(int index)
        {
            if (_DirectionVectors == null) _CalculateVectors();
            return _DirectionVectors[index];
        }

        [LuaDocsDescription("Returns a vector representing the normal of the path at the given point")]
        [LuaDocsExample("myPath:GetNormal(3)")]
        [LuaDocsParameter("index", "Index of control point to use")]
        public Vector3 GetNormal(int index)
        {
            if (_Normals == null) _CalculateVectors();
            return _Normals[index];
        }

        [LuaDocsDescription("Returns a vector representing the tangent of the path at the given point")]
        [LuaDocsExample("myPath:GetTangent(3)")]
        [LuaDocsParameter("index", "Index of control point to use")]
        public Vector3 GetTangent(int index)
        {
            if (_Normals == null) _CalculateVectors();
            return _Tangents[index];
        }

        [LuaDocsDescription("Draws this path as a brush stroke using current settings")]
        [LuaDocsExample("myPath:Draw()")]
        public void Draw() => LuaApiMethods.DrawPath(this);

        [LuaDocsDescription("Returns the number of points in this path")]
        public int count => _Path?.Count ?? 0;

        [LuaDocsDescription("Returns the point at the specified index")]
        public TransformApiWrapper this[int index]
        {
            get => new(_Path[index]);
            set => _Path[index] = value._TrTransform;
        }

        [LuaDocsDescription("Returns the last point in this path")]
        public TransformApiWrapper last => new TransformApiWrapper(_Path[^1]);

        [LuaDocsDescription("Inserts a new point at the end of the path")]
        [LuaDocsExample("myPath:Insert(myTransform")]
        [LuaDocsParameter("transform", "The transform to be inserted at the end of the path")]
        public void Insert(TrTransform transform) => _Path.Add(transform);

        [LuaDocsDescription("Inserts a new point at the specified index")]
        [LuaDocsExample("myPath:Insert(transform, index)")]
        [LuaDocsParameter("transform", "The transform to be inserted")]
        [LuaDocsParameter("index", "The index at which to insert the transform")]
        public void Insert(TrTransform transform, int index) => _Path.Insert(index, transform);

        [LuaDocsDescription("Transforms all points in the path by the specific amount")]
        [LuaDocsExample("myPath:TransformBy(transform)")]
        [LuaDocsParameter("transform", "The transform to be applied to all points in the path")]
        public void TransformBy(TrTransform transform)
        {
            LuaApiMethods.TransformPath(this, transform);
        }

        [LuaDocsDescription("Changes the position of all points in the path by a given amount")]
        [LuaDocsExample("myPath:TranslateBy(Vector3:up)")]
        [LuaDocsParameter("amount", "The distance to move the points")]
        public void TranslateBy(Vector3 amount) => TransformBy(TrTransform.T(amount));

        [LuaDocsDescription("Rotates all points in the path around the origin by a given amount")]
        [LuaDocsExample("myPath:RotateBy(Rotation.New(45, 0, 0)")]
        [LuaDocsParameter("amount", "The amount by which to rotate the path")]
        public void RotateBy(Quaternion amount) => TransformBy(TrTransform.R(amount));

        [LuaDocsDescription("Scales the path")]
        [LuaDocsExample("myPath:ScaleBy(Vector3:New(2, 1, 1)")]
        [LuaDocsParameter("scale", "The scaling factor to apply to the path")]
        public void ScaleBy(Vector3 scale)
        {
            // Supports non-uniform scaling
            for (var i = 0; i < _Path.Count; i++)
            {
                _Path[i].translation.Scale(scale);
            }
        }

        [LuaDocsDescription("Moves all points on the path so that their common center is the origin")]
        [LuaDocsExample("myPath:Center()")]
        public void Center()
        {
            (Vector3 center, float _) = _CalculateCenterAndScale(_Path);

            // Apply the scale factor to each Vector3 in the input list
            for (var i = 0; i < _Path.Count; i++)
            {
                var tr = _Path[i];
                tr.translation = tr.translation - center;
                _Path[i] = tr;
            }
        }

        [LuaDocsDescription("Reorders the points so that point at the given index is shifted to be the first point")]
        [LuaDocsExample("myPath:StartingFrom(3)")]
        [LuaDocsParameter(@"index", "The index of the point to make the new first point")]
        public void StartingFrom(int index)
        {
            if (_Path == null) return;
            _Path = _Path.Skip(index).Concat(_Path.Take(index)).ToList();
        }

        [LuaDocsDescription("Returns the index of the point closest to the given position")]
        [LuaDocsExample("myPath:FindClosest(Vector3:New(10, 2, 4)")]
        [LuaDocsParameter("point", "The 3D position that we are seeking the closest to")]
        public int FindClosest(Vector3 point)
        {
            if (_Path == null) return 0;
            return _Path.Select((x, i) => new { i, x }).Aggregate(
                (acc, x) => (x.x.translation - point).sqrMagnitude < (acc.x.translation - point).sqrMagnitude ? x : acc
            ).i;
        }

        [LuaDocsDescription("Returns the index of the point with the smallest X value")]
        [LuaDocsExample("myPath:FindMinimumX()")]
        public int FindMinimumX() => _FindMinimum(Axis.X);

        [LuaDocsDescription("Returns the index of the point with the smallest Y value")]
        [LuaDocsExample("myPath:FindMinimumY()")]
        public int FindMinimumY() => _FindMinimum(Axis.Y);

        [LuaDocsDescription("Returns the index of the point with the smallest Z value")]
        [LuaDocsExample("myPath:FindMinimumZ()")]
        public int FindMinimumZ() => _FindMinimum(Axis.Z);

        [LuaDocsDescription("Returns the index of the point with the biggest X value")]
        [LuaDocsExample("myPath:FindMaximumX()")]
        public int FindMaximumX() => _FindMaximum(Axis.X);

        [LuaDocsDescription("Returns the index of the point with the biggest Y value")]
        [LuaDocsExample("myPath:FindMaximumY()")]
        public int FindMaximumY() => _FindMaximum(Axis.Y);

        [LuaDocsDescription("Returns the index of the point with the biggest Z value")]
        [LuaDocsExample("myPath:FindMaximumZ()")]
        public int FindMaximumZ() => _FindMaximum(Axis.Z);

        [MoonSharpHidden]
        public int _FindMinimum(Axis axis)
        {
            if (_Path == null) return 0;
            return _Path
                .Select((v, i) => (translation: v.translation[(int)axis], index: i))
                .Aggregate((a, b) => a.translation < b.translation ? a : b)
                .index;
        }

        [MoonSharpHidden]
        public int _FindMaximum(Axis axis)
        {
            return _Path
                .Select((v, i) => (translation: v.translation[(int)axis], index: i))
                .Aggregate((a, b) => a.translation > b.translation ? a : b)
                .index;
        }

        [LuaDocsDescription("Scales and shifts all points so that they fit in a cube of the given size at the origin")]
        [LuaDocsExample("myPath:Normalize(size)")]
        [LuaDocsParameter("size", "The size of the cube to fit the path into")]
        public void Normalize(float size = 1)
        {
            if (_Path == null) return;
            (Vector3 center, float unitScale) = _CalculateCenterAndScale(_Path);
            size *= unitScale;

            // Apply the scale factor to each Vector3 in the input list
            for (var i = 0; i < _Path.Count; i++)
            {
                var tr = _Path[i];
                tr.translation = (tr.translation - center) * size;
                _Path[i] = tr;
            }
        }

        [MoonSharpHidden]
        public static (Vector3 center, float scale) _CalculateCenterAndScale(List<TrTransform> path)
        {
            // Find the min and max values for each axis
            float minX = path.Min(v => v.translation.x);
            float minY = path.Min(v => v.translation.y);
            float minZ = path.Min(v => v.translation.z);

            float maxX = path.Max(v => v.translation.x);
            float maxY = path.Max(v => v.translation.y);
            float maxZ = path.Max(v => v.translation.z);

            // Compute the range for each axis
            float rangeX = maxX - minX;
            float rangeY = maxY - minY;
            float rangeZ = maxZ - minZ;

            // Calculate the center of the original path
            Vector3 center = new Vector3(
                (minX + maxX) / 2,
                (minY + maxY) / 2,
                (minZ + maxZ) / 2
            );

            // Find the largest range to maintain the aspect ratio
            float largestRange = Mathf.Max(rangeX, rangeY, rangeZ);

            // Don't scale if the largest range is zero to avoid division by zero
            float scale = largestRange == 0 ? 1 : 1 / largestRange;

            return (center, scale);
        }

        [MoonSharpHidden]
        public static List<TrTransform> _SampleByCount(List<TrTransform> trs, int parts)
        {
            if (trs == null || trs.Count < 2 || parts < 1) return trs;
            List<TrTransform> subdividedPath = new List<TrTransform>();

            float totalDistance = 0;
            for (int i = 0; i < trs.Count - 1; i++)
            {
                totalDistance += Vector3.Distance(trs[i].translation, trs[i + 1].translation);
            }

            float partDistance = totalDistance / parts;

            subdividedPath.Add(trs[0]);

            float accumulatedDistance = 0f;
            int originalPathIndex = 0;
            var startPoint = trs[0];

            while (originalPathIndex < trs.Count - 1)
            {
                var endPoint = trs[originalPathIndex + 1];
                float segmentDistance = Vector3.Distance(startPoint.translation, endPoint.translation);

                if (accumulatedDistance + segmentDistance >= partDistance)
                {
                    float interpolationFactor = (partDistance - accumulatedDistance) / segmentDistance;
                    Vector3 newTranslation = Vector3.Lerp(startPoint.translation, endPoint.translation, interpolationFactor);
                    Quaternion newRotation = Quaternion.Lerp(startPoint.rotation, endPoint.rotation, interpolationFactor);
                    float newScale = Mathf.Lerp(startPoint.scale, endPoint.scale, interpolationFactor);
                    var newPoint = TrTransform.TRS(newTranslation, newRotation, newScale);
                    subdividedPath.Add(newPoint);
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
            subdividedPath.Add(trs[^1]);
            return subdividedPath;
        }

        [MoonSharpHidden]
        public static List<TrTransform> _SubdivideSegments(List<TrTransform> trs, int parts)
        {
            if (parts < 1 || trs == null || trs.Count < 2)
            {
                return trs;
            }

            var newPath = new List<TrTransform>();

            for (int i = 0; i < trs.Count - 1; i++)
            {
                var startPoint = trs[i];
                var endPoint = trs[i + 1];

                // Include the starting point of the segment
                newPath.Add(startPoint);

                for (int j = 1; j < parts; j++)
                {
                    float interpolationFactor = (float)j / parts;
                    Vector3 newTranslation = Vector3.Lerp(startPoint.translation, endPoint.translation, interpolationFactor);
                    Quaternion newRotation = Quaternion.Lerp(startPoint.rotation, endPoint.rotation, interpolationFactor);
                    float newScale = Mathf.Lerp(startPoint.scale, endPoint.scale, interpolationFactor);
                    newPath.Add(TrTransform.TRS(newTranslation, newRotation, newScale));
                }
            }

            // Add the last point of the input path
            newPath.Add(trs[^1]);

            return newPath;
        }

        [MoonSharpHidden]
        public static List<TrTransform> _SampleByDistance(List<TrTransform> trs, float spacing)
        {
            if (trs == null || trs.Count < 2 || spacing <= 0) return trs;
            List<TrTransform> resampledPath = new List<TrTransform>();
            resampledPath.Add(trs[0]);

            float accumulatedDistance = 0f;
            int originalPathIndex = 0;
            var startPoint = trs[0];

            while (originalPathIndex < trs.Count - 1)
            {
                var endPoint = trs[originalPathIndex + 1];
                float segmentDistance = Vector3.Distance(startPoint.translation, endPoint.translation);

                if (accumulatedDistance + segmentDistance >= spacing)
                {
                    float interpolationFactor = (spacing - accumulatedDistance) / segmentDistance;
                    Vector3 newTranslation = Vector3.Lerp(startPoint.translation, endPoint.translation, interpolationFactor);
                    Quaternion newRotation = Quaternion.Lerp(startPoint.rotation, endPoint.rotation, interpolationFactor);
                    float newScale = Mathf.Lerp(startPoint.scale, endPoint.scale, interpolationFactor);
                    var newPoint = TrTransform.TRS(newTranslation, newRotation, newScale);
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
            resampledPath.Add(trs[^1]);
            return resampledPath;
        }

        [LuaDocsDescription("Resamples the path evenly by distance")]
        [LuaDocsExample(@"myPath:SampleByDistance(spacing)")]
        [LuaDocsParameter(@"spacing", "The space between points in the new path")]
        [LuaDocsReturnValue(@"The new path")]
        public void SampleByDistance(float spacing)
        {
            _Path = _SampleByDistance(_Path, spacing);
        }

        [LuaDocsDescription("Resamples the path evenly into the specified number of points")]
        [LuaDocsExample(@"myPath:SampleByCount(count)")]
        [LuaDocsParameter(@"count", "The number of points in the new path")]
        [LuaDocsReturnValue(@"The new path")]
        public void SampleByCount(int count)
        {
            _Path = _SampleByCount(_Path, count);
        }

        [LuaDocsDescription("Subdivides each path segment into the specified number of parts")]
        [LuaDocsExample(@"myPath:SubdivideSegments(parts)")]
        [LuaDocsParameter(@"parts", "Number of parts to subdivide into")]
        [LuaDocsReturnValue(@"The new path")]
        public void SubdivideSegments(int parts)
        {
            _Path = _SubdivideSegments(_Path, parts);
        }

        [LuaDocsDescription(@"Generates a hermite spline")]
        [LuaDocsExample(@"myPath:Hermite(startTransform, endTransform, startTangent, endTangent, resolution, tangentStrength)")]
        [LuaDocsParameter(@"startTransform", "Starting transformation")]
        [LuaDocsParameter(@"endTransform", "End transformation")]
        [LuaDocsParameter(@"startTangent", "Starting tangent")]
        [LuaDocsParameter(@"endTangent", "End tangent")]
        [LuaDocsParameter(@"resolution", "Resolution of the spline")]
        [LuaDocsParameter(@"tangentStrength", "Strength of the tangent")]
        [LuaDocsReturnValue(@"A new Path")]
        public static PathApiWrapper Hermite(TrTransform startTransform, TrTransform endTransform, Vector3 startTangent, Vector3 endTangent, int resolution, float tangentStrength = 1f)
        {

            Vector3 tangentInDirection(TrTransform p1, TrTransform p2, Vector3 tangent)
            {
                // Flips tangent based on direction towards p2
                Vector3 dir = p2.translation - p1.translation;
                float dotRight = Vector3.Dot(dir, p1.right);
                float dotLeft = Vector3.Dot(dir, -p1.right);
                return dotRight > dotLeft ? tangent : -tangent;
            }

            List<TrTransform> path = new List<TrTransform>(resolution + 2);

            startTangent = tangentInDirection(startTransform, endTransform, startTangent) * tangentStrength;
            endTangent = tangentInDirection(endTransform, startTransform, endTangent) * tangentStrength;

            Quaternion startOrientation = Quaternion.LookRotation(startTangent.normalized);
            TrTransform trStart = TrTransform.TR(startTransform.translation, startOrientation);
            path.Add(trStart);

            for (int i = 0; i <= resolution; i++)
            {
                float t = (float)i / resolution; // calculate t

                float t2 = t * t;
                float t3 = t2 * t;
                float h00 = 2 * t3 - 3 * t2 + 1;
                float h10 = t3 - 2 * t2 + t;
                float h01 = -2 * t3 + 3 * t2;
                float h11 = t3 - t2;
                Vector3 position = h00 * startTransform.translation + h10 * startTangent + h01 * endTransform.translation + h11 * endTangent;

                // TODO this ain't right
                Quaternion orientation = Quaternion.LookRotation(path.Count < 1 ?
                    startTangent.normalized :
                    (path[^1].translation - position).normalized);

                float scale = Mathf.Lerp(startTransform.scale, endTransform.scale, t);

                path.Add(TrTransform.TRS(position, orientation, scale));
            }

            Quaternion finalOrientation = Quaternion.LookRotation(endTangent.normalized);
            TrTransform trEnd = TrTransform.TR(endTransform.translation, finalOrientation);
            path.Add(trEnd);

            return new PathApiWrapper(path);
        }
    }
}
