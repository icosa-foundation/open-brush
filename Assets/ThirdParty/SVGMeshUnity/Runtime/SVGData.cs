using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SVGMeshUnity.Internals;
using UnityEngine;

namespace SVGMeshUnity
{
    public class SVGData
    {
        // https://github.com/jkroso/abs-svg-path
        // https://github.com/jkroso/normalize-svg-path
        // https://github.com/colinmeinke/svg-arc-to-cubic-bezier
        
        // https://github.com/jkroso/parse-svg-path
        
        public List<Curve> Curves = new List<Curve>();

        private Vector2 Start;
        private Vector2 Current;
        private Nullable<Vector2> Bezier;
        private Nullable<Vector2> Quad;

        public void Clear()
        {
            Curves.Clear();
            Start = Vector2.zero;
            Current = Vector2.zero;
            Bezier = null;
            Quad = null;
        }

        public void Move(float x, float y)
        {
            Move(new Vector2(x, y));
        }

        public void MoveRelative(float x, float y)
        {
            Move(Current.x + x, Current.y + y);
        }

        public void Move(Vector2 v)
        {
            Start = v;
            Curves.Add(new Curve()
            {
                IsMove = true,
                Position = v,
            });
            Current = v;
            Bezier = null;
            Quad = null;
        }

        public void MoveRelative(Vector2 v)
        {
            Move(Current + v);
        }

        public void Curve(float inX, float inY, float outX, float outY, float x, float y)
        {
            Curve(new Vector2(inX, inY), new Vector2(outX, outY), new Vector2(x, y));
        }

        public void Curve(Vector2 inControl, Vector2 outControl, Vector2 v)
        {
            CurveInternal(inControl, outControl, v);
            Bezier = outControl;
        }

        private void CurveInternal(Vector2 inControl, Vector2 outControl, Vector2 v)
        {
            Curves.Add(new Curve()
            {
                Position = v,
                InControl = inControl,
                OutControl = outControl,
            });
            Current = v;
            Bezier = null;
            Quad = null;
        }

        public void CurveRelative(float inX, float inY, float outX, float outY, float x, float y)
        {
            CurveRelative(new Vector2(inX, inY), new Vector2(outX, outY), new Vector2(x, y));
        }

        public void CurveRelative(Vector2 inControl, Vector2 outControl, Vector2 v)
        {
            Curve(Current + inControl, Current + outControl, Current + v);
        }

        public void CurveSmooth(float controlX, float controlY, float x, float y)
        {
            CurveSmooth(new Vector2(controlX, controlY), new Vector2(x, y));
        }

        public void CurveSmooth(Vector2 control, Vector2 v)
        {
            Curve(Bezier != null ? Current * 2f - Bezier.Value : Current, control, v);
        }

        public void CurveSmoothRelative(float controlX, float controlY, float x, float y)
        {
            CurveSmoothRelative(new Vector2(controlX, controlY), new Vector2(x, y));
        }

        public void CurveSmoothRelative(Vector2 control, Vector2 v)
        {
            CurveSmooth(Current + control, Current + v);
        }

        public void Quadratic(float controlX, float controlY, float x, float y)
        {
            Quadratic(new Vector2(controlX, controlY), new Vector2(x, y));
        }

        public void Quadratic(Vector2 control, Vector2 v)
        {
            CurveInternal(Current / 3f + (2f / 3f) * control, v / 3f + (2f / 3f) * control, v);
            Quad = control;
        }
        
        public void QuadraticRelative(float controlX, float controlY, float x, float y)
        {
            QuadraticRelative(new Vector2(controlX, controlY), new Vector2(x, y));
        }

        public void QuadraticRelative(Vector2 control, Vector2 v)
        {
            Quadratic(Current + control, Current + v);
        }

        public void QuadraticSmooth(float x, float y)
        {
            QuadraticSmooth(new Vector2(x, y));
        }

        public void QuadraticSmooth(Vector2 v)
        {
            Quadratic(Quad != null ? Current * 2f - Quad.Value : Current, v);
        }
        
        public void QuadraticSmoothRelative(float x, float y)
        {
            QuadraticSmooth(Current.x + x, Current.y + y);
        }

        public void QuadraticSmoothRelative(Vector2 v)
        {
            QuadraticSmooth(Current + v);
        }

        public void Arc(float radiusX, float radiusY, float xAxisRotation, bool largeArcFlag, bool sweepFlag, float x, float y)
        {
            Arc(new Vector2(radiusX, radiusY), xAxisRotation, largeArcFlag, sweepFlag, new Vector2(x, y));
        }
        
        public void ArcRelative(float radiusX, float radiusY, float xAxisRotation, bool largeArcFlag, bool sweepFlag, float x, float y)
        {
            ArcRelative(new Vector2(radiusX, radiusY), xAxisRotation, largeArcFlag, sweepFlag, new Vector2(x, y));
        }

        public void ArcRelative(Vector2 radius, float xAxisRotation, bool largeArcFlag, bool sweepFlag, Vector2 v)
        {
            Arc(radius, xAxisRotation, largeArcFlag, sweepFlag, Current + v);
        }

        public void Line(float x, float y)
        {
            Line(new Vector2(x, y));
        }

        public void Line(Vector2 v)
        {
            CurveInternal(Current, v, v);
        }

        public void LineRelative(float x, float y)
        {
            Line(Current.x + x, Current.y + y);
        }

        public void LineRelative(Vector2 v)
        {
            Line(Current + v);
        }

        public void LineHorizontal(float x)
        {
            Line(x, Current.y);
        }

        public void LineHorizontalRelative(float x)
        {
            LineHorizontal(Current.x + x);
        }

        public void LineVertical(float y)
        {
            Line(Current.x, y);
        }

        public void LineVerticalRelative(float y)
        {
            LineVertical(Current.y + y);
        }

        public void Close()
        {
            Line(Start);
        }


        #region Arc

        public void Arc(Vector2 radius, float xAxisRotation, bool largeArcFlag, bool sweepFlag, Vector2 v)
        {
            if (radius.x == 0f || radius.y == 0f)
            {
                return;
            }

            const float TAU = Mathf.PI * 2f;

            var sinphi = Mathf.Sin(xAxisRotation * TAU / 360f);
            var cosphi = Mathf.Cos(xAxisRotation * TAU / 360f);

            var pxp = cosphi * (Current.x - v.x) / 2f + sinphi * (Current.y - v.y) / 2f;
            var pyp = -sinphi * (Current.x - v.x) / 2f + cosphi * (Current.y - v.y) / 2f;

            if (pxp == 0f && pyp == 0f)
            {
                return;
            }

            var rx = Mathf.Abs(radius.x);
            var ry = Mathf.Abs(radius.y);

            var lambda =
                Mathf.Pow(pxp, 2f) / Mathf.Pow(rx, 2f) +
                Mathf.Pow(pyp, 2f) / Mathf.Pow(ry, 2);

            if (lambda > 1f)
            {
                rx *= Mathf.Sqrt(lambda);
                ry *= Mathf.Sqrt(lambda);
            }

            var rxsq = Mathf.Pow(rx, 2f);
            var rysq = Mathf.Pow(ry, 2f);
            var pxpsq = Mathf.Pow(pxp, 2f);
            var pypsq = Mathf.Pow(pyp, 2f);

            var radicant = (rxsq * rysq) - (rxsq * pypsq) - (rysq * pxpsq);

            if (radicant < 0f)
            {
                radicant = 0f;
            }

            radicant /= (rxsq * pypsq) + (rysq * pxpsq);
            radicant = Mathf.Sqrt(radicant) * (largeArcFlag == sweepFlag ? -1f : 1f);

            var centerxp = radicant * rx / ry * pyp;
            var centeryp = radicant * -ry / rx * pxp;

            var centerx = cosphi * centerxp - sinphi * centeryp + (Current.x + v.x) / 2f;
            var centery = sinphi * centerxp + cosphi * centeryp + (Current.y + v.y) / 2f;

            var vx1 = (pxp - centerxp) / rx;
            var vy1 = (pyp - centeryp) / ry;
            var vx2 = (-pxp - centerxp) / rx;
            var vy2 = (-pyp - centeryp) / ry;

            var ang1 = VectorAngle(1, 0f, vx1, vy1);
            var ang2 = VectorAngle(vx1, vy1, vx2, vy2);

            if (sweepFlag == false && ang2 > 0f)
            {
                ang2 -= TAU;
            }

            if (sweepFlag == true && ang2 < 0f)
            {
                ang2 += TAU;
            }

            var segments = Mathf.Max(Mathf.Ceil(Mathf.Abs(ang2) / (TAU / 4f)), 1f);

            ang2 /= segments;

            for (var i = 0; i < segments; ++i)
            {
                var a = 4f / 3f * Mathf.Tan(ang2 / 4f);

                var x1 = Mathf.Cos(ang1);
                var y1 = Mathf.Sin(ang1);
                var x2 = Mathf.Cos(ang1 + ang2);
                var y2 = Mathf.Sin(ang1 + ang2);

                var curve0 = new Vector2(x1 - y1 * a, y1 + x1 * a);
                var curve1 = new Vector2(x2 + y2 * a, y2 - x2 * a);
                var curve2 = new Vector2(x2, y2);

                MapToEllipse(ref curve0, rx, ry, cosphi, sinphi, centerx, centery);
                MapToEllipse(ref curve1, rx, ry, cosphi, sinphi, centerx, centery);
                MapToEllipse(ref curve2, rx, ry, cosphi, sinphi, centerx, centery);
                
                CurveInternal(curve0, curve1, curve2);

                ang1 += ang2;
            }
        }
        
        private float VectorAngle(float ux, float uy, float vx, float vy)
        {
            var sign = Mathf.Sign(ux * vy - uy * vx);
            var umag = Mathf.Sqrt(ux * ux + uy * uy);
            var vmag = Mathf.Sqrt(ux * ux + uy * uy);
            var dot = ux * vx + uy * vy;

            var div = dot / (umag * vmag);

            if (div > 1f)
            {
                div = 1f;
            }

            if (div < -1f)
            {
                div = -1f;
            }

            return sign * Mathf.Acos(div);
        }

        private void MapToEllipse(ref Vector2 v, float rx, float ry, float cosphi, float sinphi, float centerx, float centery)
        {
            var x = v.x * rx;
            var y = v.y * ry;

            v.x = cosphi * x - sinphi * y + centerx;
            v.y = sinphi * x + cosphi * y + centery;
        }

        #endregion


        #region Path Parser
        
        private static readonly Regex Segment = new Regex("([astvzqmhlc])([^astvzqmhlc]*)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        private static readonly Dictionary<string ,int> ArgumentLengthes = new Dictionary<string, int>()
        {
            { "a", 7 }, { "c", 6 }, { "h", 1 }, { "l", 2 }, { "m", 2 }, { "q", 4 }, { "s", 4 }, { "t", 2 }, { "v", 1 }, { "z", 0 }
        };

        public void Path(string data)
        {
            var args = new float[32];
            var numArgs = 0;
            
            foreach (Match seg in Segment.Matches(data))
            {
                var command = seg.Groups[1].Value;
                var type = command.ToLower();
                
                ParseArgs(seg.Groups[2].Value, ref args, out numArgs);

                var argsIndex = 0;
                
                if (type == "m" && numArgs > 2)
                {
                    LoadCommand(command, type, args, numArgs, ref argsIndex);
                    type = "l";
                    command = command == "m" ? "l" : "L";
                }
                
                for (;;)
                {
                    if (LoadCommand(command, type, args, numArgs, ref argsIndex))
                    {
                        break;
                    }
                }
            }
        }

        private bool LoadCommand(string command, string type, float[] args, int numArgs, ref int argsIndex)
        {
            if (numArgs!=0 && argsIndex == numArgs)
            {
                return true;
            }
            
            var len = ArgumentLengthes[type];

            if (argsIndex + len > numArgs)
            {
                throw new ArgumentException("Malformed path data");
            }

            var i = argsIndex;
            switch (command)
            {
                case "A":
                    Arc(args[i + 0], args[i + 1], args[i + 2], args[i + 3] > 0f, args[i + 4] > 0f, args[i + 5],
                        args[i + 6]);
                    break;
                case "a":
                    ArcRelative(args[i + 0], args[i + 1], args[i + 2], args[i + 3] > 0f, args[i + 4] > 0f, args[i + 5],
                        args[i + 6]);
                    break;
                case "C":
                    Curve(args[i + 0], args[i + 1], args[i + 2], args[i + 3], args[i + 4], args[i + 5]);
                    break;
                case "c":
                    CurveRelative(args[i + 0], args[i + 1], args[i + 2], args[i + 3], args[i + 4], args[i + 5]);
                    break;
                case "H":
                    LineHorizontal(args[i + 0]);
                    break;
                case "h":
                    LineHorizontalRelative(args[i + 0]);
                    break;
                case "L":
                    Line(args[i + 0], args[i + 1]);
                    break;
                case "l":
                    LineRelative(args[i + 0], args[i + 1]);
                    break;
                case "M":
                    Move(args[i + 0], args[i + 1]);
                    break;
                case "m":
                    MoveRelative(args[i + 0], args[i + 1]);
                    break;
                case "Q":
                    Quadratic(args[i + 0], args[i + 1], args[i + 2], args[i + 3]);
                    break;
                case "q":
                    QuadraticRelative(args[i + 0], args[i + 1], args[i + 2], args[i + 3]);
                    break;
                case "S":
                    CurveSmooth(args[i + 0], args[i + 1], args[i + 2], args[i + 3]);
                    break;
                case "s":
                    CurveSmoothRelative(args[i + 0], args[i + 1], args[i + 2], args[i + 3]);
                    break;
                case "T":
                    QuadraticSmooth(args[i + 0], args[i + 1]);
                    break;
                case "t":
                    QuadraticSmoothRelative(args[i + 0], args[i + 1]);
                    break;
                case "V":
                    LineVertical(args[i + 0]);
                    break;
                case "v":
                    LineVerticalRelative(args[i + 0]);
                    break;
                case "Z":
                case "z":
                    Close();
                    break;
            }

            argsIndex += len;

            if (numArgs == 0) return true;

            return false;
        }

        private void ParseArgs(string s, ref float[] args, out int numArgs)
        {
            numArgs = 0;

            var l = s.Length;
            var buf = new StringBuilder(16);
            var lastIsE = false;
            var includesDot = false;
            for (var i = 0; i < l; ++i)
            {
                var isBreak = false;
                var c = s[i];
                switch (c)
                {
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        buf.Append(c);
                        lastIsE = false;
                        break;
                    case '.':
                        if (includesDot)
                        {
                            isBreak = true;
                            --i;
                        }
                        else
                        {
                            buf.Append(c);
                            includesDot = true;
                            lastIsE = false;
                        }
                        break;
                    case 'e':
                        buf.Append(c);
                        lastIsE = true;
                        break;
                    case '+':
                    case '-':
                        if (buf.Length > 0 && !lastIsE)
                        {
                            isBreak = true;
                            --i;
                        }
                        else
                        {
                            buf.Append(c);
                            lastIsE = false;
                        }
                        break;
                    default:
                        isBreak = true;
                        break;
                }

                if (isBreak || i == l - 1)
                {
                    if (buf.Length > 0)
                    {
                        if (args.Length == numArgs)
                        {
                            var newArgs = new float[args.Length + 32];
                            args.CopyTo(newArgs, 0);
                            args = newArgs;
                        }

                        args[numArgs] = float.Parse(buf.ToString());
                        numArgs++;
                        buf.Length = 0;
                        lastIsE = false;
                        includesDot = false;
                    }
                }
            }
        }

        #endregion


        #region Debug

        public string Dump()
        {
            return Curves
                .Select(_ =>
                {
                    if (_.IsMove)
                    {
                        return string.Format("M {0} {1}", _.Position.x, _.Position.y);
                    }
                    else
                    {
                        return string.Format("C {0} {1}, {2} {3}, {4} {5}", _.InControl.x, _.InControl.y, _.OutControl.x, _.OutControl.y, _.Position.x, _.Position.y);
                    }
                })
                .Aggregate("", (_, s) => _ + s + " ");
        }

        #endregion
    }
}