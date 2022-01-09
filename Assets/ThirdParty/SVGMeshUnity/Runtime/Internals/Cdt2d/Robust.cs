using UnityEngine;

namespace SVGMeshUnity.Internals.Cdt2d
{
    public static class Robust
    {
        // https://github.com/mikolalysenko/robust-orientation
        // https://github.com/mikolalysenko/robust-in-sphere
        // https://github.com/mikolalysenko/robust-sum
        // https://github.com/mikolalysenko/robust-subtract
        // https://github.com/mikolalysenko/robust-scale
        // https://github.com/mikolalysenko/two-product
        // https://github.com/mikolalysenko/two-sum

        private static readonly float Epsilon = 1.1102230246251565e-16f;
        private static readonly float Errbound3 = (3.0f + 16.0f * Epsilon) * Epsilon;

        private static readonly float Splitter = +(Mathf.Pow(2f, 27f) + 1.0f);

        private static float[][] BufferPool = new float[32][];
        private static int BufferPoolSize = 0;

        private static float[] GetTemporaryBuffer()
        {
            if (BufferPoolSize > 0)
            {
                return BufferPool[--BufferPoolSize];
            }
            return new float[16];
        }

        private static void ReleaseTemporaryBuffer(ref float[] buf)
        {
            if (BufferPool.Length == BufferPoolSize)
            {
                var newBufferPool = new float[BufferPool.Length + 32][];
                BufferPool.CopyTo(newBufferPool, 0);
                BufferPool = newBufferPool;
            }

            BufferPool[BufferPoolSize++] = buf;
            buf = null;
        }

        public static float Orientation(Vector2 a, Vector2 b, Vector2 c)
        {
            var l = (a.y - c.y) * (b.x - c.x);
            var r = (a.x - c.x) * (b.y - c.y);
            var det = l - r;
            var s = 0f;
            if (l > 0)
            {
                if (r <= 0)
                {
                    return det;
                }
                else
                {
                    s = l + r;
                }
            }
            else if (l < 0)
            {
                if (r >= 0)
                {
                    return det;
                }
                else
                {
                    s = -(l + r);
                }
            }
            else
            {
                return det;
            }

            var tol = Errbound3 * s;
            if (det >= tol || det <= -tol)
            {
                return det;
            }
            
            var m0 = a;
            var m1 = b;
            var m2 = c;
            
            var sL = GetTemporaryBuffer();
            var sR = GetTemporaryBuffer();

            var p = GetTemporaryBuffer();
            var n = GetTemporaryBuffer();
            var d = GetTemporaryBuffer();

            var pN = 0;
            var nN = 0;
            var dN = 0;

            var pL = GetTemporaryBuffer();
            var pR = GetTemporaryBuffer();

            var pLN = 0;
            var pRN = 0;

            TwoProd( m1.y, m2.x, sL);
            TwoProd(-m2.y, m1.x, sR);
            Sum(sL, 2, sR, 2, pL, out pLN);

            TwoProd( m0.y, m1.x, sL);
            TwoProd(-m1.y, m0.x, sR);
            Sum(sL, 2, sR, 2, pR, out pRN);
            
            Sum(pL, pLN, pR, pRN, p, out pN);
            
            ReleaseTemporaryBuffer(ref pL);
            ReleaseTemporaryBuffer(ref pR);

            TwoProd( m0.y, m2.x, sL);
            TwoProd(-m2.y, m0.x, sR);
            Sum(sL, 2, sR, 2, n, out nN);
            
            ReleaseTemporaryBuffer(ref sL);
            ReleaseTemporaryBuffer(ref sR);
            
            Sub(p, pN, n, nN, d, out dN);

            var result = d[dN - 1];
            
            ReleaseTemporaryBuffer(ref p);
            ReleaseTemporaryBuffer(ref n);
            ReleaseTemporaryBuffer(ref d);

            return result;
        }

        public static float InSphere(Vector2 m0, Vector2 m1, Vector2 m2, Vector2 m3)
        {
            var w = GetTemporaryBuffer();
            var wL = GetTemporaryBuffer();
            var wR = GetTemporaryBuffer();

            var wN = 0;
            
            var w0m1 = GetTemporaryBuffer();
            var w0m2 = GetTemporaryBuffer();
            var w0m3 = GetTemporaryBuffer();
            var w1m0 = GetTemporaryBuffer();
            var w1m2 = GetTemporaryBuffer();
            var w1m3 = GetTemporaryBuffer();
            var w2m0 = GetTemporaryBuffer();
            var w2m1 = GetTemporaryBuffer();
            var w2m3 = GetTemporaryBuffer();
            var w3m0 = GetTemporaryBuffer();
            var w3m1 = GetTemporaryBuffer();
            var w3m2 = GetTemporaryBuffer();
            
            var w0m1N = 0;
            var w0m2N = 0;
            var w0m3N = 0;
            var w1m0N = 0;
            var w1m2N = 0;
            var w1m3N = 0;
            var w2m0N = 0;
            var w2m1N = 0;
            var w2m3N = 0;
            var w3m0N = 0;
            var w3m1N = 0;
            var w3m2N = 0;
            
            TwoProd(m0[0], m0[0], wL);
            TwoProd(m0[1], m0[1], wR);
            Sum(wL, 2, wR, 2, w, out wN);
            Scale(w, wN, m1[0], w0m1, out w0m1N);
            Scale(w, wN, m2[0], w0m2, out w0m2N);
            Scale(w, wN, m3[0], w0m3, out w0m3N);
            
            TwoProd(m1[0], m1[0], wL);
            TwoProd(m1[1], m1[1], wR);
            Sum(wL, 2, wR, 2, w, out wN);
            Scale(w, wN, m0[0], w1m0, out w1m0N);
            Scale(w, wN, m2[0], w1m2, out w1m2N);
            Scale(w, wN, m3[0], w1m3, out w1m3N);

            TwoProd(m2[0], m2[0], wL);
            TwoProd(m2[1], m2[1], wR);
            Sum(wL, 2, wR, 2, w, out wN);
            Scale(w, wN, m0[0], w2m0, out w2m0N);
            Scale(w, wN, m1[0], w2m1, out w2m1N);
            Scale(w, wN, m3[0], w2m3, out w2m3N);

            TwoProd(m3[0], m3[0], wL);
            TwoProd(m3[1], m3[1], wR);
            Sum(wL, 2, wR, 2, w, out wN);
            Scale(w, wN, m0[0], w3m0, out w3m0N);
            Scale(w, wN, m1[0], w3m1, out w3m1N);
            Scale(w, wN, m2[0], w3m2, out w3m2N);
            
            ReleaseTemporaryBuffer(ref wL);
            ReleaseTemporaryBuffer(ref wR);
            ReleaseTemporaryBuffer(ref w);

            var p = GetTemporaryBuffer();
            var n = GetTemporaryBuffer();
            var d = GetTemporaryBuffer();

            var pN = 0;
            var nN = 0;
            var dN = 0;

            var pL = GetTemporaryBuffer();
            var pR = GetTemporaryBuffer();

            var pLN = 0;
            var pRN = 0;

            var pLLL = GetTemporaryBuffer();
            var pLL = GetTemporaryBuffer();
            var pLRLL = GetTemporaryBuffer();
            var pLRL = GetTemporaryBuffer();
            var pLRRL = GetTemporaryBuffer();
            var pLRR = GetTemporaryBuffer();
            var pLR = GetTemporaryBuffer();

            var pLLLN = 0;
            var pLLN = 0;
            var pLRLLN = 0;
            var pLRLN = 0;
            var pLRRLN = 0;
            var pLRRN = 0;
            var pLRN = 0;
            
            Sub(w3m2, w3m2N, w2m3, w2m3N, pLLL, out pLLLN);
            Scale(pLLL, pLLLN, m1[1], pLL, out pLLN);
            Sub(w3m1, w3m1N, w1m3, w1m3N, pLRLL, out pLRLLN);
            Scale(pLRLL, pLRLLN, -m2[1], pLRL, out pLRLN);
            Sub(w2m1, w2m1N, w1m2, w1m2N, pLRRL, out pLRRLN);
            Scale(pLRRL, pLRRLN, m3[1], pLRR, out pLRRN);
            Sum(pLRL, pLRLN, pLRR, pLRRN, pLR, out pLRN);
            Sum(pLL, pLLN, pLR, pLRN, pL, out pLN);
            
            ReleaseTemporaryBuffer(ref pLLL);
            ReleaseTemporaryBuffer(ref pLL);
            ReleaseTemporaryBuffer(ref pLRLL);
            ReleaseTemporaryBuffer(ref pLRL);
            ReleaseTemporaryBuffer(ref pLRRL);
            ReleaseTemporaryBuffer(ref pLRR);
            ReleaseTemporaryBuffer(ref pLR);

            var pRLL = GetTemporaryBuffer();
            var pRL = GetTemporaryBuffer();
            var pRRLL = GetTemporaryBuffer();
            var pRRL = GetTemporaryBuffer();
            var pRRRL = GetTemporaryBuffer();
            var pRRR = GetTemporaryBuffer();
            var pRR = GetTemporaryBuffer();

            var pRLLN = 0;
            var pRLN = 0;
            var pRRLLN = 0;
            var pRRLN = 0;
            var pRRRLN = 0;
            var pRRRN = 0;
            var pRRN = 0;
            
            Sub(w3m1, w3m1N, w1m3, w1m3N, pRLL, out pRLLN);
            Scale(pRLL, pRLLN, m0[1], pRL, out pRLN);
            Sub(w3m0, w3m0N, w0m3, w0m3N, pRRLL, out pRRLLN);
            Scale(pRRLL, pRRLLN, -m1[1], pRRL, out pRRLN);
            Sub(w1m0, w1m0N, w0m1, w0m1N, pRRRL, out pRRRLN);
            Scale(pRRRL, pRRRLN, m3[1], pRRR, out pRRRN);
            Sum(pRRL, pRRLN, pRRR, pRRRN, pRR, out pRRN);
            Sum(pRL, pRLN, pRR, pRRN, pR, out pRN);
            
            ReleaseTemporaryBuffer(ref pRLL);
            ReleaseTemporaryBuffer(ref pRL);
            ReleaseTemporaryBuffer(ref pRRLL);
            ReleaseTemporaryBuffer(ref pRRL);
            ReleaseTemporaryBuffer(ref pRRRL);
            ReleaseTemporaryBuffer(ref pRRR);
            ReleaseTemporaryBuffer(ref pRR);
            
            Sum(pL, pLN, pR, pRN, p, out pN);
            
            ReleaseTemporaryBuffer(ref pL);
            ReleaseTemporaryBuffer(ref pR);

            var nL = GetTemporaryBuffer();
            var nR = GetTemporaryBuffer();

            var nLN = 0;
            var nRN = 0;

            var nLLL = GetTemporaryBuffer();
            var nLL = GetTemporaryBuffer();
            var nLRLL = GetTemporaryBuffer();
            var nLRL = GetTemporaryBuffer();
            var nLRRL = GetTemporaryBuffer();
            var nLRR = GetTemporaryBuffer();
            var nLR = GetTemporaryBuffer();

            var nLLLN = 0;
            var nLLN = 0;
            var nLRLLN = 0;
            var nLRLN = 0;
            var nLRRLN = 0;
            var nLRRN = 0;
            var nLRN = 0;

            Sub(w3m2, w3m2N, w2m3, w2m3N, nLLL, out nLLLN);
            Scale(nLLL, nLLLN, m0[1], nLL, out nLLN);
            Sub(w3m0, w3m0N, w0m3, w0m3N, nLRLL, out nLRLLN);
            Scale(nLRLL, nLRLLN, -m2[1], nLRL, out nLRLN);
            Sub(w2m0, w2m0N, w0m2, w0m2N, nLRRL, out nLRRLN);
            Scale(nLRRL, nLRRLN, m3[1], nLRR, out nLRRN);
            Sum(nLRL, nLRLN, nLRR, nLRRN, nLR, out nLRN);
            Sum(nLL, nLLN, nLR, nLRN, nL, out nLN);
            
            ReleaseTemporaryBuffer(ref nLLL);
            ReleaseTemporaryBuffer(ref nLL);
            ReleaseTemporaryBuffer(ref nLRLL);
            ReleaseTemporaryBuffer(ref nLRL);
            ReleaseTemporaryBuffer(ref nLRRL);
            ReleaseTemporaryBuffer(ref nLRR);
            ReleaseTemporaryBuffer(ref nLR);

            var nRLL = GetTemporaryBuffer();
            var nRL = GetTemporaryBuffer();
            var nRRLL = GetTemporaryBuffer();
            var nRRL = GetTemporaryBuffer();
            var nRRRL = GetTemporaryBuffer();
            var nRRR = GetTemporaryBuffer();
            var nRR = GetTemporaryBuffer();

            var nRLLN = 0;
            var nRLN = 0;
            var nRRLLN = 0;
            var nRRLN = 0;
            var nRRRLN = 0;
            var nRRRN = 0;
            var nRRN = 0;

            Sub(w2m1, w2m1N, w1m2, w1m2N, nRLL, out nRLLN);
            Scale(nRLL, nRLLN, m0[1], nRL, out nRLN);
            Sub(w2m0, w2m0N, w0m2, w0m2N, nRRLL, out nRRLLN);
            Scale(nRRLL, nRRLLN, -m1[1], nRRL, out nRRLN);
            Sub(w1m0, w1m0N, w0m1, w0m1N, nRRRL, out nRRRLN);
            Scale(nRRRL, nRRRLN, m2[1], nRRR, out nRRRN);
            Sum(nRRL, nRRLN, nRRR, nRRRN, nRR, out nRRN);
            Sum(nRL, nRLN, nRR, nRRN, nR, out nRN);
            
            ReleaseTemporaryBuffer(ref nRLL);
            ReleaseTemporaryBuffer(ref nRL);
            ReleaseTemporaryBuffer(ref nRRLL);
            ReleaseTemporaryBuffer(ref nRRL);
            ReleaseTemporaryBuffer(ref nRRRL);
            ReleaseTemporaryBuffer(ref nRRR);
            ReleaseTemporaryBuffer(ref nRR);
            
            Sum(nL, nLN, nR, nRN, n, out nN);
            
            ReleaseTemporaryBuffer(ref nL);
            ReleaseTemporaryBuffer(ref nR);
            
            ReleaseTemporaryBuffer(ref w0m1);
            ReleaseTemporaryBuffer(ref w0m2);
            ReleaseTemporaryBuffer(ref w0m3);
            ReleaseTemporaryBuffer(ref w1m0);
            ReleaseTemporaryBuffer(ref w1m2);
            ReleaseTemporaryBuffer(ref w1m3);
            ReleaseTemporaryBuffer(ref w2m0);
            ReleaseTemporaryBuffer(ref w2m1);
            ReleaseTemporaryBuffer(ref w2m3);
            ReleaseTemporaryBuffer(ref w3m0);
            ReleaseTemporaryBuffer(ref w3m1);
            ReleaseTemporaryBuffer(ref w3m2);
            
            Sub(p, pN, n, nN, d, out dN);

            var result = d[dN - 1];
            
            ReleaseTemporaryBuffer(ref p);
            ReleaseTemporaryBuffer(ref n);
            ReleaseTemporaryBuffer(ref d);

            return result;
        }

        private static void TwoProd(float a, float b, float[] result)
        {
            var x = a * b;

            var c = Splitter * a;
            var abig = c - a;
            var ahi = c - abig;
            var alo = a - ahi;

            var d = Splitter * b;
            var bbig = d - b;
            var bhi = d - bbig;
            var blo = b - bhi;

            var err1 = x - (ahi * bhi);
            var err2 = err1 - (alo * bhi);
            var err3 = err2 - (ahi * blo);

            var y = alo * blo - err3;
            
            result[0] = y;
            result[1] = x;
        }

        private static void TwoSum(float a, float b, float[] result)
        {
            var x = a + b;
            var bv = x - a;
            var av = x - bv;
            var br = b - bv;
            var ar = a - av;
            
            result[0] = ar + br;
            result[1] = x;
        }
        
        private static void Sum(float[] e, int eN, float[] f, int fN, float[] result, out int resultN)
        {
            if(eN == 1 && fN == 1)
            {
                ScalarScalar(e[0], f[0], result, out resultN);
                return;
            }

            resultN = 0;
            
            var eptr = 0;
            var fptr = 0;
            var ei = e[eptr];
            var ea = Mathf.Abs(ei);
            var fi = f[fptr];
            var fa = Mathf.Abs(fi);
            var a = 0f;
            var b = 0f;
            if (ea < fa)
            {
                b = ei;
                eptr += 1;
                if (eptr < eN)
                {
                    ei = e[eptr];
                    ea = Mathf.Abs(ei);
                }
            }
            else
            {
                b = fi;
                fptr += 1;
                if (fptr < fN)
                {
                    fi = f[fptr];
                    fa = Mathf.Abs(fi);
                }
            }

            if ((eptr < eN && ea < fa) || (fptr >= fN))
            {
                a = ei;
                eptr += 1;
                if (eptr < eN)
                {
                    ei = e[eptr];
                    ea = Mathf.Abs(ei);
                }
            }
            else
            {
                a = fi;
                fptr += 1;
                if (fptr < fN)
                {
                    fi = f[fptr];
                    fa = Mathf.Abs(fi);
                }
            }

            var x = a + b;
            var bv = x - a;
            var y = b - bv;
            var q0 = y;
            var q1 = x;
            var _x = 0f;
            var _bv = 0f;
            var _av = 0f;
            var _br = 0f;
            var _ar = 0f;
            while (eptr < eN && fptr < fN)
            {
                if (ea < fa)
                {
                    a = ei;
                    eptr += 1;
                    if (eptr < eN)
                    {
                        ei = e[eptr];
                        ea = Mathf.Abs(ei);
                    }
                }
                else
                {
                    a = fi;
                    fptr += 1;
                    if (fptr < fN)
                    {
                        fi = f[fptr];
                        fa = Mathf.Abs(fi);
                    }
                }

                b = q0;
                x = a + b;
                bv = x - a;
                y = b - bv;
                if (y != 0f)
                {
                    result[resultN++] = y;
                }

                _x = q1 + x;
                _bv = _x - q1;
                _av = _x - _bv;
                _br = x - _bv;
                _ar = q1 - _av;
                q0 = _ar + _br;
                q1 = _x;
            }

            while (eptr < eN)
            {
                a = ei;
                b = q0;
                x = a + b;
                bv = x - a;
                y = b - bv;
                if (y != 0f)
                {
                    result[resultN++] = y;
                }

                _x = q1 + x;
                _bv = _x - q1;
                _av = _x - _bv;
                _br = x - _bv;
                _ar = q1 - _av;
                q0 = _ar + _br;
                q1 = _x;
                eptr += 1;
                if (eptr < eN)
                {
                    ei = e[eptr];
                }
            }

            while (fptr < fN)
            {
                a = fi;
                b = q0;
                x = a + b;
                bv = x - a;
                y = b - bv;
                if (y != 0f)
                {
                    result[resultN++] = y;
                }

                _x = q1 + x;
                _bv = _x - q1;
                _av = _x - _bv;
                _br = x - _bv;
                _ar = q1 - _av;
                q0 = _ar + _br;
                q1 = _x;
                fptr += 1;
                if (fptr < fN)
                {
                    fi = f[fptr];
                }
            }

            if (q0 != 0f)
            {
                result[resultN++] = q0;
            }

            if (q1 != 0f)
            {
                result[resultN++] = q1;
            }

            if (resultN == 0)
            {
                result[resultN++] = 0f;
            }
        }

        private static void Sub(float[] e, int eN, float[] f, int fN, float[] result, out int resultN)
        {
            if (eN == 1 && fN == 1)
            {
                ScalarScalar(e[0], -f[0], result, out resultN);
                return;
            }

            resultN = 0;

            var eptr = 0;
            var fptr = 0;
            var ei = e[eptr];
            var ea = Mathf.Abs(ei);
            var fi = -f[fptr];
            var fa = Mathf.Abs(fi);
            var a = 0f;
            var b = 0f;
            if (ea < fa)
            {
                b = ei;
                eptr += 1;
                if (eptr < eN)
                {
                    ei = e[eptr];
                    ea = Mathf.Abs(ei);
                }
            }
            else
            {
                b = fi;
                fptr += 1;
                if (fptr < fN)
                {
                    fi = -f[fptr];
                    fa = Mathf.Abs(fi);
                }
            }

            if ((eptr < eN && ea < fa) || (fptr >= fN))
            {
                a = ei;
                eptr += 1;
                if (eptr < eN)
                {
                    ei = e[eptr];
                    ea = Mathf.Abs(ei);
                }
            }
            else
            {
                a = fi;
                fptr += 1;
                if (fptr < fN)
                {
                    fi = -f[fptr];
                    fa = Mathf.Abs(fi);
                }
            }

            var x = a + b;
            var bv = x - a;
            var y = b - bv;
            var q0 = y;
            var q1 = x;
            var _x = 0f;
            var _bv = 0f;
            var _av = 0f;
            var _br = 0f;
            var _ar = 0f;
            while (eptr < eN && fptr < fN)
            {
                if (ea < fa)
                {
                    a = ei;
                    eptr += 1;
                    if (eptr < eN)
                    {
                        ei = e[eptr];
                        ea = Mathf.Abs(ei);
                    }
                }
                else
                {
                    a = fi;
                    fptr += 1;
                    if (fptr < fN)
                    {
                        fi = -f[fptr];
                        fa = Mathf.Abs(fi);
                    }
                }

                b = q0;
                x = a + b;
                bv = x - a;
                y = b - bv;
                if (y != 0f)
                {
                    result[resultN++] = y;
                }

                _x = q1 + x;
                _bv = _x - q1;
                _av = _x - _bv;
                _br = x - _bv;
                _ar = q1 - _av;
                q0 = _ar + _br;
                q1 = _x;
            }

            while (eptr < eN)
            {
                a = ei;
                b = q0;
                x = a + b;
                bv = x - a;
                y = b - bv;
                if (y != 0f)
                {
                    result[resultN++] = y;
                }

                _x = q1 + x;
                _bv = _x - q1;
                _av = _x - _bv;
                _br = x - _bv;
                _ar = q1 - _av;
                q0 = _ar + _br;
                q1 = _x;
                eptr += 1;
                if (eptr < eN)
                {
                    ei = e[eptr];
                }
            }

            while (fptr < fN)
            {
                a = fi;
                b = q0;
                x = a + b;
                bv = x - a;
                y = b - bv;
                if (y != 0f)
                {
                    result[resultN++] = y;
                }

                _x = q1 + x;
                _bv = _x - q1;
                _av = _x - _bv;
                _br = x - _bv;
                _ar = q1 - _av;
                q0 = _ar + _br;
                q1 = _x;
                fptr += 1;
                if (fptr < fN)
                {
                    fi = -f[fptr];
                }
            }

            if (q0 != 0f)
            {
                result[resultN++] = q0;
            }

            if (q1 != 0f)
            {
                result[resultN++] = q1;
            }

            if (resultN == 0)
            {
                result[resultN++] = 0.0f;
            }
        }

        private static void Scale(float[] e, int eN, float scale, float[] result, out int resultN)
        {
            if (eN == 1)
            {
                var ts = GetTemporaryBuffer();
                
                TwoProd(e[0], scale, ts);
                
                if (ts[0] != 0f)
                {
                    result[0] = ts[0];
                    result[1] = ts[1];
                    resultN = 2;
                    ReleaseTemporaryBuffer(ref ts);
                    return;
                }

                result[0] = ts[1];
                resultN = 1;
                ReleaseTemporaryBuffer(ref ts);
                return;
            }

            var q = GetTemporaryBuffer();
            var t = GetTemporaryBuffer();

            q[0] = 0.1f;
            q[1] = 0.1f;
            t[0] = 0.1f;
            t[1] = 0.1f;

            resultN = 0;
            
            TwoProd(e[0], scale, q);
            
            if (q[0] != 0f)
            {
                result[resultN++] = q[0];
            }

            for (var i = 1; i < eN; ++i)
            {
                TwoProd(e[i], scale, t);
                
                var pq = q[1];
                
                TwoSum(pq, t[0], q);
                
                if (q[0] != 0f)
                {
                    result[resultN++] = q[0];
                }

                var a = t[1];
                var b = q[1];
                var x = a + b;
                var bv = x - a;
                var y = b - bv;
                q[1] = x;
                
                if (y != 0f)
                {
                    result[resultN++] = y;
                }
            }

            if (q[1] != 0f)
            {
                result[resultN++] = q[1];
            }
            
            ReleaseTemporaryBuffer(ref q);
            ReleaseTemporaryBuffer(ref t);

            if (resultN == 0)
            {
                result[resultN++] = 0.0f;
            }
        }

        //Easy case: Add two scalars
        private static void ScalarScalar(float a, float b, float[] result, out int resultN)
        {
            var x = a + b;
            var bv = x - a;
            var av = x - bv;
            var br = b - bv;
            var ar = a - av;
            var y = ar + br;
            if (y != 0f)
            {
                result[0] = y;
                result[1] = x;
                resultN = 2;
                return;
            }
            result[0] = x;
            resultN = 1;
        }
    }
}