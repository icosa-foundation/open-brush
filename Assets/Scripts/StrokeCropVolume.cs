using UnityEngine;

namespace TiltBrush
{
    internal readonly struct StrokeCropVolume
    {
        internal enum Shape { Sphere, Box, Capsule, Ellipsoid, Plane }

        private readonly Shape m_Shape;
        private readonly Vector3 m_HalfSize;
        public readonly TrTransform Pose;
        public readonly float BoundingRadius;

        public StrokeCropVolume(Shape shape, TrTransform pose, Vector3 halfSize)
        {
            if (!Finite(pose.translation) || !Finite(halfSize) ||
                !Finite(new Vector3(pose.rotation.x, pose.rotation.y, pose.rotation.z)) ||
                float.IsNaN(pose.rotation.w) || float.IsInfinity(pose.rotation.w))
                throw new System.ArgumentException("Crop dimensions and transform must be finite");
            m_Shape = shape;
            Pose = pose;
            m_HalfSize = halfSize;
            BoundingRadius = shape == Shape.Sphere ? halfSize.x :
                shape == Shape.Box ? halfSize.magnitude :
                shape == Shape.Capsule ? halfSize.y : shape == Shape.Plane ? float.PositiveInfinity :
                Mathf.Max(halfSize.x, halfSize.y, halfSize.z);
        }

        private static bool Finite(Vector3 v) =>
            !float.IsNaN(v.x) && !float.IsInfinity(v.x) &&
            !float.IsNaN(v.y) && !float.IsInfinity(v.y) &&
            !float.IsNaN(v.z) && !float.IsInfinity(v.z);

        public bool Contains(Vector3 p)
        {
            if (m_Shape == Shape.Plane) return p.y >= 0;
            if (m_Shape == Shape.Sphere) return p.sqrMagnitude <= m_HalfSize.x * m_HalfSize.x;
            if (m_Shape == Shape.Capsule)
            {
                p.y -= Mathf.Clamp(p.y, -BodyHalfHeight, BodyHalfHeight);
                return p.sqrMagnitude <= m_HalfSize.x * m_HalfSize.x;
            }
            if (m_Shape == Shape.Ellipsoid) return NormalizeEllipsoid(p).sqrMagnitude <= 1;
            return Mathf.Abs(p.x) <= m_HalfSize.x && Mathf.Abs(p.y) <= m_HalfSize.y &&
                Mathf.Abs(p.z) <= m_HalfSize.z;
        }

        // Returns the part of a segment inside the closed, convex volume.
        public bool ClipSegment(Vector3 a, Vector3 b, out float enter, out float exit)
        {
            enter = 0;
            exit = 1;
            var d = b - a;
            if (m_Shape == Shape.Plane)
            {
                if (d.y == 0) return a.y >= 0;
                float crossing = -a.y / d.y;
                if (d.y > 0) enter = Mathf.Max(enter, crossing);
                else exit = Mathf.Min(exit, crossing);
                return enter <= exit;
            }
            if (m_Shape == Shape.Box)
                return Slab(a.x, d.x, m_HalfSize.x, ref enter, ref exit) &&
                    Slab(a.y, d.y, m_HalfSize.y, ref enter, ref exit) &&
                    Slab(a.z, d.z, m_HalfSize.z, ref enter, ref exit);

            if (m_Shape == Shape.Capsule)
            {
                bool hit = false;
                enter = 1; exit = 0;
                // The capsule is the union of its cylindrical body and two spherical ends.
                float first = 0, last = 1;
                var radialA = new Vector3(a.x, 0, a.z);
                var radialD = new Vector3(d.x, 0, d.z);
                if (Slab(a.y, d.y, BodyHalfHeight, ref first, ref last) &&
                    Ball(radialA, radialD, m_HalfSize.x, ref first, ref last))
                { enter = first; exit = last; hit = true; }
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    first = 0; last = 1;
                    if (!Ball(a - Vector3.up * (sign * BodyHalfHeight), d, m_HalfSize.x,
                        ref first, ref last)) continue;
                    enter = Mathf.Min(enter, first); exit = Mathf.Max(exit, last); hit = true;
                }
                return hit;
            }
            if (m_Shape == Shape.Ellipsoid)
                return Ball(NormalizeEllipsoid(a), NormalizeEllipsoid(d), 1, ref enter, ref exit);
            return Ball(a, d, m_HalfSize.x, ref enter, ref exit);
        }

        private float BodyHalfHeight => m_HalfSize.y - m_HalfSize.x;
        private Vector3 NormalizeEllipsoid(Vector3 p) => new(
            p.x / m_HalfSize.x, p.y / m_HalfSize.y, p.z / m_HalfSize.z);

        private static bool Ball(Vector3 a, Vector3 d, float radius, ref float enter, ref float exit)
        {
            // Double precision avoids losing the intersection of long segments with small volumes.
            double aa = (double)d.x * d.x + (double)d.y * d.y + (double)d.z * d.z;
            double bb = (double)a.x * d.x + (double)a.y * d.y + (double)a.z * d.z;
            double cc = (double)a.x * a.x + (double)a.y * a.y + (double)a.z * a.z -
                (double)radius * radius;
            if (aa == 0) return cc <= 0;
            double discriminant = bb * bb - aa * cc;
            if (discriminant < 0) return false;
            double root = System.Math.Sqrt(discriminant);
            enter = Mathf.Max(enter, (float)((-bb - root) / aa));
            exit = Mathf.Min(exit, (float)((-bb + root) / aa));
            return enter <= exit;
        }

        private static bool Slab(float a, float d, float halfSize, ref float enter, ref float exit)
        {
            if (d == 0) return Mathf.Abs(a) <= halfSize;
            float first = (-halfSize - a) / d;
            float last = (halfSize - a) / d;
            enter = Mathf.Max(enter, Mathf.Min(first, last));
            exit = Mathf.Min(exit, Mathf.Max(first, last));
            return enter <= exit;
        }
    }
}
