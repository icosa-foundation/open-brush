﻿using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    [LuaDocsDescription("An axis-aligned 3D bounding box")]
    [MoonSharpUserData]
    public class BoundsApiWrapper
    {
        [MoonSharpHidden] public Bounds _Bounds;

        public BoundsApiWrapper()
        {
            _Bounds = new Bounds(Vector3.zero, Vector3.zero);
        }

        public BoundsApiWrapper(Bounds bounds)
        {
            _Bounds = bounds;
        }

        [LuaDocsDescription("Creates bounds from a center and size")]
        [LuaDocsExample("bounds = Bounds:New(center, size)")]
        [LuaDocsParameter("center", "The center of the bounds")]
        [LuaDocsParameter("size", "The full size of the bounds")]
        [LuaDocsReturnValue("The new bounds")]
        public static BoundsApiWrapper New(Vector3 center, Vector3 size) => new(new Bounds(center, size));

        [LuaDocsDescription("Creates bounds from minimum and maximum corners")]
        [LuaDocsExample("bounds = Bounds:FromMinMax(min, max)")]
        [LuaDocsParameter("min", "The minimum corner of the bounds")]
        [LuaDocsParameter("max", "The maximum corner of the bounds")]
        [LuaDocsReturnValue("The new bounds")]
        public static BoundsApiWrapper FromMinMax(Vector3 min, Vector3 max)
        {
            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return new BoundsApiWrapper(bounds);
        }

        [LuaDocsDescription("Calculates bounds that contain all points in a path")]
        [LuaDocsExample("bounds = Bounds:FromPath(path)")]
        [LuaDocsParameter("path", "The path to measure")]
        [LuaDocsReturnValue("The calculated bounds")]
        public static BoundsApiWrapper FromPath(PathApiWrapper path) => path.bounds;

        [LuaDocsDescription("Calculates bounds that contain all points in a path list")]
        [LuaDocsExample("bounds = Bounds:FromPaths(paths)")]
        [LuaDocsParameter("paths", "The paths to measure")]
        [LuaDocsReturnValue("The calculated bounds")]
        public static BoundsApiWrapper FromPaths(PathListApiWrapper paths) => paths.bounds;

        [LuaDocsDescription("The bounds center")]
        public Vector3 center
        {
            get => _Bounds.center;
            set => _Bounds.center = value;
        }

        [LuaDocsDescription("The bounds full size")]
        public Vector3 size
        {
            get => _Bounds.size;
            set => _Bounds.size = value;
        }

        [LuaDocsDescription("Half of the bounds size")]
        public Vector3 extents
        {
            get => _Bounds.extents;
            set => _Bounds.extents = value;
        }

        [LuaDocsDescription("The minimum corner of the bounds")]
        public Vector3 min => _Bounds.min;

        [LuaDocsDescription("The maximum corner of the bounds")]
        public Vector3 max => _Bounds.max;

        [LuaDocsDescription("The radius of the smallest sphere centered on this bounds that contains all corners")]
        public float radius => _Bounds.extents.magnitude;

        [LuaDocsDescription("The longest side of the bounds")]
        public float longestSide => Mathf.Max(_Bounds.size.x, Mathf.Max(_Bounds.size.y, _Bounds.size.z));

        [LuaDocsDescription("The shortest side of the bounds")]
        public float shortestSide => Mathf.Min(_Bounds.size.x, Mathf.Min(_Bounds.size.y, _Bounds.size.z));

        [LuaDocsDescription("Returns whether the bounds contains a point")]
        [LuaDocsExample("inside = bounds:Contains(point)")]
        [LuaDocsParameter("point", "The point to test")]
        [LuaDocsReturnValue("True if the point is inside the bounds")]
        public bool Contains(Vector3 point) => _Bounds.Contains(point);

        [MoonSharpHidden]
        public static Bounds Calculate(IEnumerable<Vector3> points)
        {
            var pointList = points.ToList();
            if (pointList.Count == 0)
            {
                return new Bounds(Vector3.zero, Vector3.zero);
            }

            var bounds = new Bounds(pointList[0], Vector3.zero);
            for (var i = 1; i < pointList.Count; i++)
            {
                bounds.Encapsulate(pointList[i]);
            }
            return bounds;
        }
    }
}
