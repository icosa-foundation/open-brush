using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SVGMeshUnity.Internals
{
    public class BezierToPolyline
    {
        // https://github.com/mattdesl/svg-path-contours

        public float Scale = 1f;
        public float PathDistanceEpsilon = 1f;
        public int RecursionLimit = 8;
        public float FLTEpsilon = 1.19209290e-7f;
        public float AngleEpsilon = 0.01f;
        public float AngleTolerance = 0f;
        public float CuspLimit = 0f;
        public WorkBufferPool WorkBufferPool;

        private WorkBuffer<Vector2> WorkVertices;
        
        public void GetContours(SVGData svg, List<List<Vector2>> data)
        {
            WorkBufferPool.Get(ref WorkVertices);
            
            var pen = Vector2.zero;
            
            var curves = svg.Curves;
            var l = curves.Count;
            for (var i = 0; i < l; ++i)
            {
                var curve = curves[i];
                if (curve.IsMove)
                {
                    EmitWorkVerticesIfNeeded(data);
                }
                else
                {
                    FillBezier(pen, curve.InControl, curve.OutControl, curve.Position);
                }
                pen = curve.Position;
            }
            
            EmitWorkVerticesIfNeeded(data);
            WorkBufferPool.Release(ref WorkVertices);
        }

        private void EmitWorkVerticesIfNeeded(List<List<Vector2>> data)
        {
            if (WorkVertices.UsedSize == 0) return;
            // TODO: Simplify
            data.Add(WorkVertices.Data.ToList());
            WorkVertices.Clear();
        }

        ////// Based on:
        ////// https://github.com/pelson/antigrain/blob/master/agg-2.4/src/agg_curves.cpp

        private void FillBezier(Vector2 start, Vector2 c1, Vector2 c2, Vector2 end)
        {
            var distanceTolerance = PathDistanceEpsilon / Scale;
            distanceTolerance *= distanceTolerance;
            BeginFillBezier(start, c1, c2, end, distanceTolerance);
        }
        
        private void BeginFillBezier(Vector2 start, Vector2 c1, Vector2 c2, Vector2 end, float distanceTolerance)
        {
            WorkVertices.Push(ref start);
            RecursiveFillBezier(start, c1, c2, end, distanceTolerance, 0);
            WorkVertices.Push(ref end);
        }

        private void RecursiveFillBezier(Vector2 v1, Vector2 v2, Vector2 v3, Vector2 v4, float distanceTolerance, int level)
        {
            if (level > RecursionLimit) return;
            var pi = Mathf.PI;

            // Calculate all the mid-points of the line segments
            //----------------------
            var v12 = (v1 + v2) / 2f;
            var v23 = (v2 + v3) / 2f;
            var v34 = (v3 + v4) / 2f;
            var v123 = (v12 + v23) / 2f;
            var v234 = (v23 + v34) / 2f;
            var v1234 = (v123 + v234) / 2f;
            
            // Enforce subdivision first time
            if (level > 0)
            {
                // Try to approximate the full cubic curve by a single straight line
                //------------------
                var d = v4 - v1;

                var d2 = Mathf.Abs((v2.x - v4.x) * d.y - (v2.y - v4.y) * d.x);
                var d3 = Mathf.Abs((v3.x - v4.x) * d.y - (v3.y - v4.y) * d.x);

                if (d2 > FLTEpsilon && d3 > FLTEpsilon)
                {
                    // Regular care
                    //-----------------
                    if ((d2 + d3) * (d2 + d3) <= distanceTolerance * (d.x * d.x + d.y * d.y))
                    {
                        // If the curvature doesn't exceed the distanceTolerance value
                        // we tend to finish subdivisions.
                        //----------------------
                        if (AngleTolerance < AngleEpsilon)
                        {
                            WorkVertices.Push(ref v1234);
                            return;
                        }

                        // Angle & Cusp Condition
                        //----------------------
                        var a23 = Mathf.Atan2(v3.y - v2.y, v3.x - v2.x);
                        var da1 = Mathf.Abs(a23 - Mathf.Atan2(v2.y - v1.y, v2.x - v1.x));
                        var da2 = Mathf.Abs(Mathf.Atan2(v4.y - v3.y, v4.x - v3.x) - a23);

                        if (da1 >= pi)
                        {
                            da1 = 2 * pi - da1;
                        }

                        if (da2 >= pi)
                        {
                            da2 = 2 * pi - da2;
                        }

                        if (da1 + da2 < AngleTolerance)
                        {
                            // Finally we can stop the recursion
                            //----------------------
                            WorkVertices.Push(ref v1234);
                            return;
                        }

                        if (CuspLimit > 0f)
                        {
                            if (da1 > CuspLimit)
                            {
                                WorkVertices.Push(ref v2);
                                return;
                            }

                            if (da2 > CuspLimit)
                            {
                                WorkVertices.Push(ref v3);
                                return;
                            }
                        }
                    }
                }
                else
                {
                    if (d2 > FLTEpsilon)
                    {
                        // p1,p3,p4 are collinear, p2 is considerable
                        //----------------------
                        if (d2 * d2 <= distanceTolerance * (d.x * d.x + d.y * d.y))
                        {
                            if (AngleTolerance < AngleEpsilon)
                            {
                                WorkVertices.Push(ref v1234);
                                return;
                            }

                            // Angle Condition
                            //----------------------
                            var da1 = Mathf.Abs(Mathf.Atan2(v3.y - v2.y, v3.x - v2.x) -
                                                Mathf.Atan2(v2.y - v1.y, v2.x - v1.x));
                            if (da1 >= pi)
                            {
                                da1 = 2 * pi - da1;
                            }

                            if (da1 < AngleTolerance)
                            {
                                WorkVertices.Push(ref v2);
                                WorkVertices.Push(ref v3);
                                return;
                            }

                            if (CuspLimit > 0f)
                            {
                                if (da1 > CuspLimit)
                                {
                                    WorkVertices.Push(ref v2);
                                    return;
                                }
                            }
                        }
                    }
                    else if (d3 > FLTEpsilon)
                    {
                        // p1,p2,p4 are collinear, p3 is considerable
                        //----------------------
                        if (d3 * d3 <= distanceTolerance * (d.x * d.x + d.y * d.y))
                        {
                            if (AngleTolerance < AngleEpsilon)
                            {
                                WorkVertices.Push(ref v1234);
                                return;
                            }

                            // Angle Condition
                            //----------------------
                            var da1 = Mathf.Abs(Mathf.Atan2(v4.y - v3.y, v4.x - v3.x) -
                                                Mathf.Atan2(v3.y - v2.y, v3.x - v2.x));
                            if (da1 >= pi)
                            {
                                da1 = 2 * pi - da1;
                            }

                            if (da1 < AngleTolerance)
                            {
                                WorkVertices.Push(ref v2);
                                WorkVertices.Push(ref v3);
                                return;
                            }

                            if (CuspLimit > 0f)
                            {
                                if (da1 > CuspLimit)
                                {
                                    WorkVertices.Push(ref v3);
                                    return;
                                }
                            }
                        }
                    }
                    else
                    {
                        // Collinear case
                        //-----------------
                        var dx = v1234.x - (v1.x + v4.x) / 2f;
                        var dy = v1234.y - (v1.y + v4.y) / 2f;
                        if (dx * dx + dy * dy <= distanceTolerance)
                        {
                            WorkVertices.Push(ref v1234);
                            return;
                        }
                    }
                }
            }

            // Continue subdivision
            //----------------------
            RecursiveFillBezier(v1, v12, v123, v1234, distanceTolerance, level + 1);
            RecursiveFillBezier(v1234, v234, v34, v4, distanceTolerance, level + 1);
        }
    }
}