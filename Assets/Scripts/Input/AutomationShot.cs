// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy at http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    /// Actions declare the world-space subjects for a whole shot. Framing is solved once,
    /// with fixed optics and a bounded offset from the preferred first-person viewpoint.
    public sealed class AutomationShot
    {
        [Serializable]
        public class FramingSettings
        {
            public bool Enabled = true;
            [Range(0, 0.25f)] public float Margin = 0.08f;
            public float MaximumPullbackMeters = 0.15f;
            public float MaximumAngleDegrees = 40;
        }

        public sealed class Subject
        {
            public string Name { get; }
            internal readonly List<Vector3> Points = new List<Vector3>();
            public IReadOnlyList<Vector3> WorldPoints => Points;
            internal Subject(string name) { Name = name; }
            public void AddPoint(Vector3 point)
            {
                if (float.IsNaN(point.x) || float.IsInfinity(point.x) ||
                    float.IsNaN(point.y) || float.IsInfinity(point.y) ||
                    float.IsNaN(point.z) || float.IsInfinity(point.z))
                {
                    throw new ArgumentException($"Framing subject '{Name}' contains a non-finite position.");
                }
                Points.Add(point);
            }

            public void AddPose(UnityEngine.Pose pose, IEnumerable<Vector3> localPoints)
            {
                foreach (Vector3 point in localPoints) { AddPoint(pose.position + pose.rotation * point); }
            }

            public void AddBounds(Bounds bounds)
            {
                for (int i = 0; i < 8; ++i) { AddPoint(Corner(bounds, i)); }
            }
        }

        public struct FramingResult
        {
            public UnityEngine.Pose CameraPose;
            public Rect ViewportBounds;
            public float PullbackMeters;
        }

        private readonly string m_Name;
        private readonly List<Subject> m_Subjects = new List<Subject>();
        public IReadOnlyList<Subject> Subjects => m_Subjects;
        public AutomationShot(string name) { m_Name = name; }

        public Subject AddSubject(string name)
        {
            var subject = new Subject(name);
            m_Subjects.Add(subject);
            return subject;
        }

        public static Vector3 Corner(Bounds bounds, int index) => new Vector3(
            (index & 1) == 0 ? bounds.min.x : bounds.max.x,
            (index & 2) == 0 ? bounds.min.y : bounds.max.y,
            (index & 4) == 0 ? bounds.min.z : bounds.max.z);

        public static List<Vector3> CaptureLocalBounds(Transform root, IEnumerable<Renderer> renderers, bool includeDisabled = false)
        {
            var points = new List<Vector3>();
            Quaternion inverse = Quaternion.Inverse(root.rotation);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || renderer is LineRenderer ||
                    (!includeDisabled && (!renderer.enabled || !renderer.gameObject.activeInHierarchy))) { continue; }
                for (int i = 0; i < 8; ++i)
                {
                    points.Add(inverse * (renderer.transform.TransformPoint(Corner(renderer.localBounds, i)) - root.position));
                }
            }
            if (points.Count == 0) { points.Add(Vector3.zero); }
            return points;
        }

        // Exact extrema of the cubic centreline, padded for the brush footprint. This
        // includes the complete stroke without framing unused Bezier control points.
        public static Bounds CubicBounds(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float padding)
        {
            var bounds = new Bounds(a, Vector3.zero);
            bounds.Encapsulate(d);
            for (int axis = 0; axis < 3; ++axis)
            {
                float qa = -a[axis] + 3 * b[axis] - 3 * c[axis] + d[axis];
                float qb = 2 * (a[axis] - 2 * b[axis] + c[axis]);
                float qc = b[axis] - a[axis];
                if (Mathf.Abs(qa) < 0.00001f)
                {
                    if (Mathf.Abs(qb) > 0.00001f) { AddExtremum(ref bounds, a, b, c, d, -qc / qb); }
                }
                else
                {
                    float discriminant = qb * qb - 4 * qa * qc;
                    if (discriminant < 0) { continue; }
                    float root = Mathf.Sqrt(discriminant);
                    AddExtremum(ref bounds, a, b, c, d, (-qb - root) / (2 * qa));
                    AddExtremum(ref bounds, a, b, c, d, (-qb + root) / (2 * qa));
                }
            }
            bounds.Expand(2 * padding);
            return bounds;
        }

        private static void AddExtremum(ref Bounds bounds, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            if (t > 0 && t < 1) { bounds.Encapsulate(ProceduralArm.Bezier(a, b, c, d, t)); }
        }

        public FramingResult Frame(Camera camera, UnityEngine.Pose preferred, FramingSettings settings)
        {
            if (settings.Margin < 0 || settings.Margin > 0.25f || float.IsNaN(settings.Margin) ||
                settings.MaximumPullbackMeters < 0 || settings.MaximumPullbackMeters > 0.5f || float.IsNaN(settings.MaximumPullbackMeters) ||
                settings.MaximumAngleDegrees <= 0 || settings.MaximumAngleDegrees > 60 || float.IsNaN(settings.MaximumAngleDegrees))
            {
                throw new ArgumentException("Invalid framing limits: margin 0–0.25, pullback 0–0.5 metres, angle 0–60 degrees.");
            }
            var points = new List<Vector3>();
            foreach (Subject subject in m_Subjects) { points.AddRange(subject.Points); }
            if (points.Count == 0) { throw new InvalidOperationException($"Shot '{m_Name}' has no framing subjects."); }
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (Vector3 point in points) { bounds.Encapsulate(point); }
            float tangent = Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            Vector3 back = -(Quaternion.Euler(0, preferred.rotation.eulerAngles.y, 0) * Vector3.forward);
            // A small bounded pullback preserves the first-person perspective. No zooming
            // or chasing the action: all candidates use the same fixed field of view.
            for (int step = 0; step <= 6; ++step)
            {
                float pullback = settings.MaximumPullbackMeters * step / 6;
                Vector3 eye = preferred.position + back * (pullback * App.METERS_TO_UNITS);
                Quaternion rotation = Quaternion.LookRotation(bounds.center - eye, Vector3.up);
                bool valid = true;
                Rect viewport = default;
                for (int iteration = 0; iteration < 24; ++iteration)
                {
                    valid = Project(points, eye, rotation, camera.nearClipPlane, tangent, camera.aspect, out viewport);
                    if (!valid) { break; }
                    Vector2 centre = viewport.center - Vector2.one * 0.5f;
                    if (centre.sqrMagnitude < 0.00000001f) { break; }
                    Vector3 direction = rotation * new Vector3(centre.x * 2 * tangent * camera.aspect, centre.y * 2 * tangent, 1);
                    // Damping avoids oscillation when opposite edges are at very
                    // different depths (for example a nearby controller and far stroke).
                    rotation = Quaternion.LookRotation(Vector3.Slerp(rotation * Vector3.forward,
                        direction.normalized, 0.5f), Vector3.up);
                }
                if (valid && Quaternion.Angle(preferred.rotation, rotation) <= settings.MaximumAngleDegrees &&
                    Project(points, eye, rotation, camera.nearClipPlane, tangent, camera.aspect, out viewport) &&
                    (viewport.center - Vector2.one * 0.5f).sqrMagnitude < 0.000004f &&
                    viewport.xMin >= settings.Margin - 0.0001f && viewport.xMax <= 1 - settings.Margin + 0.0001f &&
                    viewport.yMin >= settings.Margin - 0.0001f && viewport.yMax <= 1 - settings.Margin + 0.0001f)
                {
                    return new FramingResult
                    {
                        CameraPose = new UnityEngine.Pose(eye, rotation), ViewportBounds = viewport, PullbackMeters = pullback
                    };
                }
            }
            throw new InvalidOperationException($"Shot '{m_Name}' cannot fit its subjects inside the framing margin and comfort limits. Split it into separate shots or adjust the declared action positions.");
        }

        private static bool Project(List<Vector3> points, Vector3 eye, Quaternion rotation,
            float near, float tangent, float aspect, out Rect rect)
        {
            Vector2 minimum = Vector2.one * float.PositiveInfinity;
            Vector2 maximum = Vector2.one * float.NegativeInfinity;
            Quaternion inverse = Quaternion.Inverse(rotation);
            foreach (Vector3 point in points)
            {
                Vector3 local = inverse * (point - eye);
                if (local.z <= near || float.IsNaN(local.z)) { rect = default; return false; }
                Vector2 screen = new Vector2(local.x / (local.z * tangent * aspect), local.y / (local.z * tangent)) * 0.5f + Vector2.one * 0.5f;
                minimum = Vector2.Min(minimum, screen);
                maximum = Vector2.Max(maximum, screen);
            }
            rect = Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
            return true;
        }
    }
}
