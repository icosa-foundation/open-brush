using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    public interface IPathApiWrapper
    {
        public ScriptCoordSpace Space { get; set; }
        public List<TrTransform> AsSingleTrList();
        public List<List<TrTransform>> AsMultiTrList();

    }

    [MoonSharpUserData]
    public class PathApiWrapper : IPathApiWrapper
    {

        [MoonSharpHidden]
        public ScriptCoordSpace Space { get; set; }

        [MoonSharpHidden]
        public List<TrTransform> AsSingleTrList() => _Path;

        [MoonSharpHidden]
        public List<List<TrTransform>> AsMultiTrList() => new List<List<TrTransform>> { _Path };

        public enum Axis {X, Y, Z}

        [MoonSharpHidden]
        public List<TrTransform> _Path;

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
                transformList[i] = TrTransform.T(positionList[i]);
            }
            _Path = transformList;
        }

        public static PathApiWrapper New() => new PathApiWrapper();
        public static PathApiWrapper New(List<TrTransform> transformList) => new PathApiWrapper(transformList);
        public static PathApiWrapper New(List<Vector3> positionList) => new PathApiWrapper(positionList);

        public int count => _Path.Count;
        public void Insert(TrTransform transform) => _Path.Add(transform);

        public void Transform(TrTransform transform)
        {
            LuaApiMethods.TransformPath(this, transform);
        }
        public void Translate(Vector3 amount) => Transform(TrTransform.T(amount));
        public void Rotate(Quaternion amount) => Transform(TrTransform.R(amount));
        public void Scale(Vector3 scale)
        {
            // Supports non-uniform scaling
            for (var i = 0; i < _Path.Count; i++)
            {
                var tr = _Path[i];
                tr.translation = new Vector3(
                    tr.translation.x * scale.x,
                    tr.translation.y * scale.y,
                    tr.translation.z * scale.z
                );
                _Path[i] = tr;
            }
        }

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

        public void StartingFrom(int index)
        {
            if (_Path == null) return;
            _Path = _Path.Skip(index).Concat(_Path.Take(index)).ToList();
        }

        public int FindClosest(Vector3 point)
        {
            if (_Path == null) return 0;
            return _Path.Select((x, i) => new {i, x}).Aggregate(
                (acc, x) => (x.x.translation - point).sqrMagnitude < (acc.x.translation - point).sqrMagnitude ? x : acc
            ).i;
        }

        public int FindMinimumX() => _FindMinimum(Axis.X);
        public int FindMinimumY() => _FindMinimum(Axis.Y);
        public int FindMinimumZ() => _FindMinimum(Axis.Z);
        public int FindMaximumX() => _FindMaximum(Axis.X);
        public int FindMaximumY() => _FindMaximum(Axis.Y);
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

        public void Normalize(float scale = 1)
        {
            if (_Path == null) return;
            (Vector3 center, float unitScale) = _CalculateCenterAndScale(_Path);
            scale *= unitScale;

            // Apply the scale factor to each Vector3 in the input list
            for (var i = 0; i < _Path.Count; i++)
            {
                var tr = _Path[i];
                tr.translation = (tr.translation - center) * scale;
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

        public void Resample(float spacing)
        {
            if (_Path == null || _Path.Count < 2 || spacing <= 0) return;
            List<TrTransform> resampledPath = new List<TrTransform>();
            resampledPath.Add(_Path[0]);

            float accumulatedDistance = 0f;
            int originalPathIndex = 0;
            var startPoint = _Path[0];

            while (originalPathIndex < _Path.Count - 1)
            {
                var endPoint = _Path[originalPathIndex + 1];
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
            resampledPath.Add(_Path[^1]);
            _Path = resampledPath;
        }
    }
}
