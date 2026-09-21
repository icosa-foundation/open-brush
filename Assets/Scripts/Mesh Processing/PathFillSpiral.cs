// Copyright 2025 The Open Brush Authors
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

using System;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{

    /// Surfaces a path that winds around an axis more than once.
    ///
    /// <remarks>
    /// A spiral does not bound a region, so there is no fill to find: projecting it onto a
    /// plane gives an outline wound many times over, and any winding rule applied to that
    /// answers a question nobody asked. But a spiral does suggest an obvious surface -- the
    /// one between each turn and the next -- and that is what this builds. The conical
    /// spiral tool script becomes a cone, the spherical one becomes a shell, and a flat
    /// spiral becomes a disc with a seam. Nothing here is a fill in the 2D sense; it is the
    /// reading a person would give the stroke.
    ///
    /// The construction is a quad strip between the path and itself one revolution later.
    /// For each point P(i) at unwrapped angle A(i), its partner is the point at A(i) + 2pi,
    /// found by interpolating along the path. The strip runs as far as that partner exists,
    /// so the outermost turn is left as a free edge -- which is what a spiral ramp looks
    /// like, and avoids inventing a cap the stroke never described.
    ///
    /// This is chosen over the planar fill only when the path really does wind: more than
    /// <see cref="kMinTurns"/> revolutions, advancing consistently rather than doubling
    /// back. A loop drawn with a little overshoot stays on the planar path, where it
    /// belongs.
    /// </remarks>
    public static class PathFillSpiral
    {
        /// Below this many revolutions a path is a loop that overshot, not a spiral.
        public const float kMinTurns = 1.3f;

        /// How much of the total turning may run backwards before the path is no longer
        /// something a single revolution-indexed strip can describe.
        private const float kMaxBacktrackFraction = 0.15f;

        /// How consistently the path's turning must point the same way before that
        /// direction is trusted as an axis. This compares the length of the summed turning
        /// against the sum of the lengths, so it reads 1 when every step turns the same way
        /// and 0 when the turning cancels out -- and, unlike comparing against the area the
        /// path could have swept, it does not change with how finely the path is sampled.
        private const float kMinTurningCoherence = 0.5f;

        public class Winding
        {
            /// Unwrapped angle about the plane normal, one per path point.
            public float[] Angles;

            /// Total revolutions, signed.
            public float Turns;

            /// False when the path doubles back enough that pairing points by revolution
            /// stops meaning anything.
            public bool Advances;
        }

        /// The axis a path winds about: the direction it sweeps area around, which is
        /// Newell's normal. The best-fit plane normal is the wrong choice here -- for a
        /// spherical spiral the covariance is very nearly isotropic, so that normal is
        /// essentially arbitrary and the measured turning comes out as zero. Newell's normal
        /// accumulates a contribution per turn and lands on the true axis. It vanishes when
        /// the turning cancels out, as in a figure eight, which is exactly when no spiral
        /// surface should be built.
        public static bool FindAxis(IList<Vector3> path, out Vector3 origin, out Vector3 axis)
        {
            origin = Vector3.zero;
            axis = Vector3.up;
            int n = path.Count;
            if (n < 4) { return false; }

            for (int i = 0; i < n; ++i) { origin += path[i]; }
            origin /= n;

            Vector3 newell = Vector3.zero;
            float magnitudes = 0f;
            // The closing step is deliberately excluded: on a spiral it leaps from the outer
            // end back to the inner start and turns the wrong way about the axis.
            for (int i = 0; i + 1 < n; ++i)
            {
                Vector3 a = path[i] - origin;
                Vector3 b = path[i + 1] - origin;
                Vector3 step = Vector3.Cross(a, b);
                newell += step;
                magnitudes += step.magnitude;
            }
            if (magnitudes <= 0f || newell.magnitude < kMinTurningCoherence * magnitudes)
            {
                return false;
            }

            axis = newell.normalized;
            return true;
        }

        /// Measures how the path winds about `normal`, as seen from `origin`.
        public static Winding MeasureWinding(IList<Vector3> path, Vector3 origin, Vector3 normal)
        {
            Vector3 axisU, axisV;
            PathFillGeometry.BasisFromNormal(normal, out axisU, out axisV);

            int n = path.Count;
            var angles = new float[n];
            float previous = 0f;
            float forward = 0f, backward = 0f;

            for (int i = 0; i < n; ++i)
            {
                Vector3 d = path[i] - origin;
                float angle = Mathf.Atan2(Vector3.Dot(d, axisV), Vector3.Dot(d, axisU));
                if (i == 0)
                {
                    angles[0] = angle;
                    previous = angle;
                    continue;
                }
                // Unwrap: take the step to be the one smaller than half a revolution.
                float step = Mathf.Repeat(angle - previous + Mathf.PI, Mathf.PI * 2f) - Mathf.PI;
                angles[i] = angles[i - 1] + step;
                previous = angle;
                if (step >= 0f) { forward += step; } else { backward -= step; }
            }

            float total = angles[n - 1] - angles[0];
            float swept = forward + backward;
            bool advances = swept > 0f &&
                            Mathf.Min(forward, backward) <= kMaxBacktrackFraction * swept;

            return new Winding
            {
                Angles = angles,
                Turns = total / (Mathf.PI * 2f),
                Advances = advances,
            };
        }

        public static bool ShouldLoft(Winding winding)
        {
            return winding != null && winding.Advances && Mathf.Abs(winding.Turns) >= kMinTurns;
        }

        /// Builds the strip between the path and itself one revolution later. Returns false
        /// if the path does not cover a full revolution's worth of pairs.
        public static bool Build(IList<Vector3> path, Winding winding, IList<Color32> colors,
                                 List<Vector3> outVertices, List<int> outTriangles,
                                 List<Vector2> outUvs, List<Color32> outColors)
        {
            int n = path.Count;
            if (n < 4 || winding == null) { return false; }

            bool ascending = winding.Angles[n - 1] > winding.Angles[0];
            float revolution = ascending ? Mathf.PI * 2f : -Mathf.PI * 2f;

            // Force the sweep monotonic before pairing points by revolution. A point sitting
            // on the axis has no meaningful angle at all -- atan2(0, 0) -- and both spiral
            // tool scripts start exactly there, so the first step or two can run backwards.
            // A hand-drawn spiral wobbles for the same reason wherever it passes near the
            // axis. Without this the strip ends at the first backward step; the winding
            // measurement keeps the unclamped angles, so what gets reported stays honest.
            var angles = new float[n];
            Array.Copy(winding.Angles, angles, n);
            for (int i = 1; i < n; ++i)
            {
                angles[i] = ascending
                    ? Mathf.Max(angles[i], angles[i - 1])
                    : Mathf.Min(angles[i], angles[i - 1]);
            }

            outVertices.Clear();
            outTriangles.Clear();
            outUvs.Clear();
            if (outColors != null) { outColors.Clear(); }

            // Walk forward, emitting a rung of the ladder for each point whose partner one
            // revolution later still lies on the path.
            int search = 0;
            int rungs = 0;
            for (int i = 0; i < n; ++i)
            {
                float target = angles[i] + revolution;
                Vector3 partner;
                Color32 partnerColor;
                if (!Sample(path, angles, colors, target, ascending, ref search,
                            out partner, out partnerColor))
                {
                    break;
                }

                float turnFraction = Mathf.Repeat(
                    (angles[i] - angles[0]) / (Mathf.PI * 2f), 1f);
                float alongPath = n > 1 ? i / (float)(n - 1) : 0f;

                outVertices.Add(path[i]);
                outUvs.Add(new Vector2(turnFraction, alongPath));
                outVertices.Add(partner);
                outUvs.Add(new Vector2(turnFraction, alongPath));
                if (outColors != null)
                {
                    outColors.Add(colors != null ? colors[i] : new Color32(255, 255, 255, 255));
                    outColors.Add(partnerColor);
                }
                ++rungs;
            }

            if (rungs < 2) { return false; }

            for (int r = 0; r + 1 < rungs; ++r)
            {
                int a = r * 2, b = r * 2 + 1, c = (r + 1) * 2, d = (r + 1) * 2 + 1;
                // Wound so the surface faces consistently along the direction of travel.
                if (ascending)
                {
                    outTriangles.Add(a); outTriangles.Add(c); outTriangles.Add(b);
                    outTriangles.Add(b); outTriangles.Add(c); outTriangles.Add(d);
                }
                else
                {
                    outTriangles.Add(a); outTriangles.Add(b); outTriangles.Add(c);
                    outTriangles.Add(b); outTriangles.Add(d); outTriangles.Add(c);
                }
            }
            return true;
        }

        /// Point on the path at the given unwrapped angle, by linear interpolation between
        /// the bracketing samples. `search` carries the scan position between calls, so the
        /// whole strip costs one pass rather than one per rung.
        private static bool Sample(IList<Vector3> path, float[] angles, IList<Color32> colors,
                                   float target, bool ascending, ref int search,
                                   out Vector3 position, out Color32 color)
        {
            position = Vector3.zero;
            color = new Color32(255, 255, 255, 255);
            int n = path.Count;

            while (search + 1 < n)
            {
                float lo = angles[search];
                float hi = angles[search + 1];
                bool brackets = ascending ? (target >= lo && target <= hi)
                                          : (target <= lo && target >= hi);
                if (brackets)
                {
                    float span = hi - lo;
                    float t = Mathf.Abs(span) > 1e-6f ? (target - lo) / span : 0f;
                    t = Mathf.Clamp01(t);
                    position = Vector3.Lerp(path[search], path[search + 1], t);
                    if (colors != null)
                    {
                        Color32 c0 = colors[search], c1 = colors[search + 1];
                        color = new Color32(
                            (byte)Mathf.Lerp(c0.r, c1.r, t),
                            (byte)Mathf.Lerp(c0.g, c1.g, t),
                            (byte)Mathf.Lerp(c0.b, c1.b, t),
                            (byte)Mathf.Lerp(c0.a, c1.a, t));
                    }
                    return true;
                }

                bool passed = ascending ? (target > hi) : (target < hi);
                if (!passed) { return false; }
                ++search;
            }
            return false;
        }
    }
} // namespace TiltBrush
