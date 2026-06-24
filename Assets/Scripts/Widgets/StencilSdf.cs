using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    /// <summary>
    /// Utility to build and query a signed distance field for active stencil widgets.
    /// Cache is rebuilt on demand and should be invalidated when stencils are added or removed.
    /// </summary>
    public static class StencilSdf
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
                if (stencil != null && stencil.gameObject.activeInHierarchy &&
                    stencil.Type != StencilType.Custom)
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
            if (sm_dirty) { RebuildCache(); }
            float min = float.PositiveInfinity;
            foreach (var c in sm_cache)
            {
                float d = DistanceToStencil(c.widget, worldPos);
                if (d < min) { min = d; }
            }
            return min;
        }

        /// <summary>
        /// Steps along the SDF isosurface from a starting point, moving in the given direction
        /// at the specified velocity. The candidate step is projected back to the surface using
        /// the field's gradient.
        /// </summary>
        public static Vector3 NextPointOnSurface(Vector3 point, float velocity, Vector3 direction, float epsilon = 0.01f)
        {
            Vector3 candidate = point + direction.normalized * velocity;
            float dist = SignedDistance(candidate);
            Vector3 normal = EstimateNormal(candidate, epsilon);
            return candidate - normal * dist;
        }

        static Vector3 EstimateNormal(Vector3 p, float eps)
        {
            Vector3 x = new Vector3(eps, 0f, 0f);
            Vector3 y = new Vector3(0f, eps, 0f);
            Vector3 z = new Vector3(0f, 0f, eps);
            float dx = SignedDistance(p + x) - SignedDistance(p - x);
            float dy = SignedDistance(p + y) - SignedDistance(p - y);
            float dz = SignedDistance(p + z) - SignedDistance(p - z);
            Vector3 grad = new Vector3(dx, dy, dz);
            return grad.normalized;
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
                        float r = radii.x;
                        float half = radii.y - r;
                        return SdCapsule(p, half, r);
                    }
                case StencilType.Custom:
                    return float.PositiveInfinity;
                default:
                    return DistanceViaCollider(s, worldPos);
            }
        }

        static float DistanceViaCollider(StencilWidget s, Vector3 worldPos)
        {
            Collider col = s.GetComponentInChildren<Collider>();
            if (col == null) { return float.PositiveInfinity; }
            Vector3 closest = col.ClosestPoint(worldPos);
            float d = Vector3.Distance(worldPos, closest);
            if ((worldPos - s.transform.position).sqrMagnitude <
                (closest - s.transform.position).sqrMagnitude)
            {
                d = -d;
            }
            return d;
        }

        static float SdEllipsoid(Vector3 p, Vector3 r)
        {
            Vector3 p2 = new Vector3(p.x / r.x, p.y / r.y, p.z / r.z);
            Vector3 p3 = new Vector3(p.x / (r.x * r.x), p.y / (r.y * r.y), p.z / (r.z * r.z));
            float k0 = p2.magnitude;
            float k1 = p3.magnitude;
            return k0 * (k0 - 1f) / k1;
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
            float t = Mathf.Clamp(Vector3.Dot(pa, ba) / Vector3.Dot(ba, ba), 0f, 1f);
            Vector3 x = pa - ba * t;
            return x.magnitude - r;
        }
    }
}
