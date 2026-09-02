// Copyright 2020 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    /// <summary>
    /// Utility to build and query a signed distance field for active stencil widgets.
    /// Cache is rebuilt on demand and should be invalidated when stencils are added or removed.
    /// </summary>
    public static class GlobalStencilSdfCache
    {
        struct Cached
        {
            public StencilWidget widget;
        }

        static readonly List<Cached> sm_cache = new List<Cached>();
        static bool sm_dirty = true;

        public static void InvalidateCache()
        {
            sm_dirty = true;
        }

        static void RebuildCache()
        {
            sm_cache.Clear();
            if (WidgetManager.m_Instance == null) { sm_dirty = false; return; }
            foreach (var stencil in WidgetManager.m_Instance.StencilWidgets)
            {
                if (stencil != null && stencil.gameObject.activeInHierarchy)
                {
                    sm_cache.Add(new Cached { widget = stencil });
                }
            }
            sm_dirty = false;
        }

        /// <summary>
        /// Returns the minimum signed distance from the given world position to all active stencils.
        /// Negative values are inside a stencil, positive outside.
        /// </summary>
        public static float SignedDistance(Vector3 worldPos)
        {
            return TryFindClosestActiveStencil(worldPos, out _, out float distance)
                ? distance
                : float.PositiveInfinity;
        }

        /// <summary>
        /// Returns the signed distance from the given world position to one stencil.
        /// Negative values are inside the stencil, positive outside.
        /// </summary>
        public static float SignedDistance(StencilWidget stencil, Vector3 worldPos)
        {
            return stencil == null
                ? float.PositiveInfinity
                : DistanceToStencil(stencil, worldPos);
        }

        /// <summary>
        /// Returns the minimum signed distance from the given world position to the supplied stencils.
        /// Negative values are inside their combined volume, positive outside.
        /// </summary>
        public static float SignedDistance(IEnumerable<StencilWidget> stencils, Vector3 worldPos)
        {
            return TryFindClosestStencil(stencils, worldPos, out _, out float distance)
                ? distance
                : float.PositiveInfinity;
        }

        /// <summary>
        /// Steps along the combined isosurface from a starting point, moving in the given direction
        /// at the specified velocity. The candidate step is projected onto the closest contributing
        /// stencil.
        /// </summary>
        public static Vector3 NextPointOnSurface(
            Vector3 point, float stepDistance, Vector3 direction)
        {
            Vector3 candidate = point + direction.normalized * stepDistance;
            if (!TryFindClosestActiveStencil(candidate, out StencilWidget stencil, out _))
            {
                return point;
            }

            return ProjectOntoStencil(stencil, candidate, point);
        }

        /// <summary>
        /// Steps along the combined isosurface of the supplied stencils.
        /// </summary>
        public static Vector3 NextPointOnSurface(
            IEnumerable<StencilWidget> stencils, Vector3 point,
            float stepDistance, Vector3 direction)
        {
            Vector3 candidate = point + direction.normalized * stepDistance;
            if (!TryFindClosestStencil(stencils, candidate, out StencilWidget stencil, out _))
            {
                return point;
            }

            return ProjectOntoStencil(stencil, candidate, point);
        }

        static bool TryFindClosestActiveStencil(
            Vector3 worldPos, out StencilWidget closestStencil, out float closestDistance)
        {
            closestStencil = null;
            closestDistance = float.PositiveInfinity;

            var widgetManager = WidgetManager.m_Instance;
            if (widgetManager == null ||
                (widgetManager.StencilsDisabled &&
                 !App.UserConfig.Flags.GuideToggleVisiblityOnly))
            {
                return false;
            }

            if (sm_dirty) { RebuildCache(); }
            foreach (var cached in sm_cache)
            {
                if (cached.widget == null || !cached.widget.gameObject.activeInHierarchy)
                {
                    sm_dirty = true;
                    continue;
                }

                float distance = DistanceToStencil(cached.widget, worldPos);
                if (distance < closestDistance)
                {
                    closestStencil = cached.widget;
                    closestDistance = distance;
                }
            }

            return closestStencil != null && IsFinite(closestDistance);
        }

        static bool TryFindClosestStencil(
            IEnumerable<StencilWidget> stencils, Vector3 worldPos,
            out StencilWidget closestStencil, out float closestDistance)
        {
            closestStencil = null;
            closestDistance = float.PositiveInfinity;
            if (stencils == null) { return false; }

            foreach (StencilWidget stencil in stencils)
            {
                if (stencil == null || !stencil.gameObject.activeInHierarchy) { continue; }

                float distance = DistanceToStencil(stencil, worldPos);
                if (distance < closestDistance)
                {
                    closestStencil = stencil;
                    closestDistance = distance;
                }
            }

            return closestStencil != null && IsFinite(closestDistance);
        }

        static Vector3 ProjectOntoStencil(
            StencilWidget stencil, Vector3 candidate, Vector3 fallback)
        {
            stencil.FindClosestPointOnSurface(
                candidate, out Vector3 surfacePosition, out _);
            return IsFinite(surfacePosition) ? surfacePosition : fallback;
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        static float DistanceToStencil(StencilWidget s, Vector3 worldPos)
        {
            Vector3 radii = s.Extents * Coords.CanvasPose.scale * 0.5f;
            Quaternion rot = s.transform.rotation;
            Vector3 p = Quaternion.Inverse(rot) * (worldPos - s.transform.position);

            switch (s.Type)
            {
                case StencilType.Sphere:
                case StencilType.Ellipsoid:
                    return SdEllipsoid(p, radii);
                case StencilType.Cube:
                case StencilType.Plane:
                    return SdBox(p, radii);
                case StencilType.Capsule:
                    {
                        float r = Mathf.Abs(radii.x);
                        float half = Mathf.Max(Mathf.Abs(radii.y) - r, 0f);
                        return SdCapsule(p, half, r);
                    }
                default:
                    return DistanceViaSurfaceQuery(s, worldPos);
            }
        }

        static float DistanceViaSurfaceQuery(StencilWidget s, Vector3 worldPos)
        {
            Vector3 surfacePos;
            Vector3 surfaceNormal;
            s.FindClosestPointOnSurface(worldPos, out surfacePos, out surfaceNormal);

            if (IsFinite(surfacePos))
            {
                Vector3 toQuery = worldPos - surfacePos;
                float distance = toQuery.magnitude;
                if (IsFinite(distance))
                {
                    // FindClosestPointOnSurface guarantees an outward-facing normal.
                    // Its dot product with the surface-to-query vector therefore gives
                    // a shape-independent inside/outside test.
                    if (IsFinite(surfaceNormal) &&
                        surfaceNormal.sqrMagnitude > 0.000001f)
                    {
                        float side = Vector3.Dot(toQuery, surfaceNormal);
                        if (Mathf.Abs(side) < 0.000001f) { return 0f; }
                        return side < 0f ? -distance : distance;
                    }

                    // Some legacy/custom stencils cannot provide a normal. Preserve the
                    // useful unsigned distance without guessing the sign from their origin.
                    return distance;
                }
            }

            Collider col = s.GetComponentInChildren<Collider>();
            if (col == null) { return float.PositiveInfinity; }
            Vector3 closest = col.ClosestPoint(worldPos);
            return IsFinite(closest)
                ? Vector3.Distance(worldPos, closest)
                : float.PositiveInfinity;
        }

        static float SdEllipsoid(Vector3 p, Vector3 r)
        {
            const float kMinRadius = 0.000001f;
            r = new Vector3(
                Mathf.Max(Mathf.Abs(r.x), kMinRadius),
                Mathf.Max(Mathf.Abs(r.y), kMinRadius),
                Mathf.Max(Mathf.Abs(r.z), kMinRadius));
            float minRadius = Mathf.Min(r.x, Mathf.Min(r.y, r.z));
            if (p.sqrMagnitude < kMinRadius * kMinRadius)
            {
                return -minRadius;
            }

            Vector3 p2 = new Vector3(p.x / r.x, p.y / r.y, p.z / r.z);
            Vector3 p3 = new Vector3(
                p.x / (r.x * r.x),
                p.y / (r.y * r.y),
                p.z / (r.z * r.z));
            float k0 = p2.magnitude;
            float k1 = p3.magnitude;
            return k1 > kMinRadius
                ? k0 * (k0 - 1f) / k1
                : -minRadius;
        }

        static float SdBox(Vector3 p, Vector3 b)
        {
            Vector3 q = new Vector3(Mathf.Abs(p.x), Mathf.Abs(p.y), Mathf.Abs(p.z)) - b;
            Vector3 maxQ = new Vector3(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f), Mathf.Max(q.z, 0f));
            float outside = maxQ.magnitude;
            float inside = Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0f);
            return outside + inside;
        }

        static float SdCapsule(Vector3 p, float h, float r)
        {
            Vector3 a = new Vector3(0f, -h, 0f);
            Vector3 b = new Vector3(0f, h, 0f);
            Vector3 pa = p - a;
            Vector3 ba = b - a;
            float lengthSquared = Vector3.Dot(ba, ba);
            if (lengthSquared < 0.000000000001f)
            {
                return p.magnitude - r;
            }

            float t = Mathf.Clamp(Vector3.Dot(pa, ba) / lengthSquared, 0f, 1f);
            Vector3 x = pa - ba * t;
            return x.magnitude - r;
        }
    }
}
