using System;

namespace SVGMeshUnity.Internals.Cdt2d
{
    public static class BinarySearch
    {
        // https://github.com/mikolalysenko/binary-search-bounds

        public interface IComparer<G, E>
        {
            int Compare(G x, E y);
        }

        public static int GE<G, E>(G[] a, E y, IComparer<G, E> c, int l, int h)
        {
            var i = h + 1;
            while (l <= h)
            {
                var m = (int) (uint) (l + h) >> 1;
                var x = a[m];
                if (c.Compare(x, y) >= 0)
                {
                    i = m;
                    h = m - 1;
                }
                else
                {
                    l = m + 1;
                }
            }

            return i;
        }

        public static int GE<G>(G[] a, G y, int l, int h) where G : IComparable<G>
        {
            var i = h + 1;
            while (l <= h)
            {
                var m = (int) (uint) (l + h) >> 1;
                var x = a[m];
                if (x.CompareTo(y) >= 0)
                {
                    i = m;
                    h = m - 1;
                }
                else
                {
                    l = m + 1;
                }
            }

            return i;
        }

        public static int GT<G, E>(G[] a, E y, IComparer<G, E> c, int l, int h)
        {
            var i = h + 1;
            while (l <= h)
            {
                var m = (int) (uint) (l + h) >> 1;
                var x = a[m];
                if (c.Compare(x, y) > 0)
                {
                    i = m;
                    h = m - 1;
                }
                else
                {
                    l = m + 1;
                }
            }

            return i;
        }

        public static int GT<G>(G[] a, G y, int l, int h) where G : IComparable<G>
        {
            var i = h + 1;
            while (l <= h)
            {
                var m = (int) (uint) (l + h) >> 1;
                var x = a[m];
                if (x.CompareTo(y) > 0)
                {
                    i = m;
                    h = m - 1;
                }
                else
                {
                    l = m + 1;
                }
            }

            return i;
        }

        public static int LT<G, E>(G[] a, E y, IComparer<G, E> c, int l, int h)
        {
            var i = l - 1;
            while (l <= h)
            {
                var m = (int) (uint) (l + h) >> 1;
                var x = a[m];
                if (c.Compare(x, y) < 0)
                {
                    i = m;
                    l = m + 1;
                }
                else
                {
                    h = m - 1;
                }
            }

            return i;
        }

        public static int LT<G>(G[] a, G y, int l, int h) where G : IComparable<G>
        {
            var i = l - 1;
            while (l <= h)
            {
                var m = (int) (uint) (l + h) >> 1;
                var x = a[m];
                if (x.CompareTo(y) < 0)
                {
                    i = m;
                    l = m + 1;
                }
                else
                {
                    h = m - 1;
                }
            }

            return i;
        }

        public static int LE<G, E>(G[] a, E y, IComparer<G, E> c, int l, int h)
        {
            var i = l - 1;
            while (l <= h)
            {
                var m = (int) (uint) (l + h) >> 1;
                var x = a[m];
                if (c.Compare(x, y) <= 0)
                {
                    i = m;
                    l = m + 1;
                }
                else
                {
                    h = m - 1;
                }
            }

            return i;
        }

        public static int LE<G>(G[] a, G y, int l, int h) where G : IComparable<G>
        {
            var i = l - 1;
            while (l <= h)
            {
                var m = (int) (uint) (l + h) >> 1;
                var x = a[m];
                if (x.CompareTo(y) <= 0)
                {
                    i = m;
                    l = m + 1;
                }
                else
                {
                    h = m - 1;
                }
            }

            return i;
        }

        public static int EQ<G, E>(G[] a, E y, IComparer<G, E> c, int l, int h)
        {
            while (l <= h)
            {
                var m = (int) (uint) (l + h) >> 1;
                var x = a[m];
                var p = c.Compare(x, y);
                if (p == 0)
                {
                    return m;
                }

                if (p <= 0)
                {
                    l = m + 1;
                }
                else
                {
                    h = m - 1;
                }
            }

            return -1;
        }

        public static int EQ<G>(G[] a, G y, int l, int h) where G : IComparable<G>
        {
            while (l <= h)
            {
                var m = (int) (uint) (l + h) >> 1;
                var x = a[m];
                var p = x.CompareTo(y);
                if (p == 0)
                {
                    return m;
                }

                if (p <= 0)
                {
                    l = m + 1;
                }
                else
                {
                    h = m - 1;
                }
            }

            return -1;
        }
    }
}