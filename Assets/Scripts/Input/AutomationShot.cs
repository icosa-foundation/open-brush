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
            public float PanelGap = 0.025f;
        }

        public enum SubjectRole { Framing, Artwork, Panel }

        public sealed class Subject
        {
            public string Name { get; }
            internal readonly SubjectRole Role;
            internal readonly List<Vector3> Points = new List<Vector3>();
            internal Subject(string name, SubjectRole role) { Name = name; Role = role; }
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

        private readonly string m_Name;
        private string m_Overlap;
        private readonly List<Subject> m_Subjects = new List<Subject>();
        public AutomationShot(string name) { m_Name = name; }

        public Subject AddSubject(string name, SubjectRole role = SubjectRole.Framing)
        {
            var subject = new Subject(name, role);
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

        public UnityEngine.Pose Frame(Camera camera, UnityEngine.Pose preferred, FramingSettings settings, float? outputAspect = null)
        {
            float aspect = outputAspect ?? camera.aspect;
            if (aspect <= 0 || float.IsNaN(aspect) || float.IsInfinity(aspect))
                throw new ArgumentException("Output aspect ratio must be finite and positive.");
            if (settings.Margin < 0 || settings.Margin > 0.25f || float.IsNaN(settings.Margin) ||
                settings.MaximumPullbackMeters < 0 || settings.MaximumPullbackMeters > 0.5f || float.IsNaN(settings.MaximumPullbackMeters) ||
                settings.MaximumAngleDegrees <= 0 || settings.MaximumAngleDegrees > 60 || float.IsNaN(settings.MaximumAngleDegrees) ||
                settings.PanelGap < 0 || settings.PanelGap > 0.15f || float.IsNaN(settings.PanelGap))
            {
                throw new ArgumentException("Invalid framing limits: margin 0–0.25, pullback 0–0.5 metres, angle 0–60 degrees, panelGap 0–0.15.");
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
                    valid = Project(points, eye, rotation, camera.nearClipPlane, tangent, aspect, out viewport);
                    if (!valid) { break; }
                    Vector2 centre = viewport.center - Vector2.one * 0.5f;
                    if (centre.sqrMagnitude < 0.00000001f) { break; }
                    Vector3 direction = rotation * new Vector3(centre.x * 2 * tangent * aspect, centre.y * 2 * tangent, 1);
                    // Damping avoids oscillation when opposite edges are at very
                    // different depths (for example a nearby controller and far stroke).
                    rotation = Quaternion.LookRotation(Vector3.Slerp(rotation * Vector3.forward,
                        direction.normalized, 0.5f), Vector3.up);
                }
                if (valid && Quaternion.Angle(preferred.rotation, rotation) <= settings.MaximumAngleDegrees &&
                    Project(points, eye, rotation, camera.nearClipPlane, tangent, aspect, out viewport) &&
                    (viewport.center - Vector2.one * 0.5f).sqrMagnitude < 0.000004f &&
                    viewport.xMin >= settings.Margin - 0.0001f && viewport.xMax <= 1 - settings.Margin + 0.0001f &&
                    viewport.yMin >= settings.Margin - 0.0001f && viewport.yMax <= 1 - settings.Margin + 0.0001f &&
                    PanelsClearArtwork(eye, rotation, camera.nearClipPlane, tangent, aspect, settings.PanelGap))
                {
                    return new UnityEngine.Pose(eye, rotation);
                }
            }
            throw new InvalidOperationException($"Shot '{m_Name}' cannot fit its subjects with clear space between panels and artwork inside the framing margin and comfort limits. {m_Overlap} Split it into separate shots or adjust the declared action positions.");
        }

        private bool PanelsClearArtwork(Vector3 eye, Quaternion rotation, float near, float tangent, float aspect, float gap)
        {
            foreach (var panel in m_Subjects)
            {
                if (panel.Role != SubjectRole.Panel || panel.Points.Count == 0) { continue; }
                if (!Project(panel.Points, eye, rotation, near, tangent, aspect, out var panelRect)) { return false; }
                panelRect = Rect.MinMaxRect(panelRect.xMin - gap, panelRect.yMin - gap,
                    panelRect.xMax + gap, panelRect.yMax + gap);
                foreach (var artwork in m_Subjects)
                {
                    if (artwork.Role != SubjectRole.Artwork || artwork.Points.Count == 0) { continue; }
                    if (!Project(artwork.Points, eye, rotation, near, tangent, aspect, out var artworkRect) ||
                        panelRect.Overlaps(artworkRect))
                    {
                        m_Overlap = $"Panel '{panel.Name}' overlaps '{artwork.Name}'.";
                        return false;
                    }
                }
            }
            return true;
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
