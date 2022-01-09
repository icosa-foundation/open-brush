using System;

namespace SVGMeshUnity.Internals
{
    public struct Int2 : IComparable<Int2>
    {
        public Int2(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
            
        public int x;
        public int y;
        
        public int CompareTo(Int2 b)
        {
            var d = x - b.x;
            if (d != 0) return d;
            return y - b.y;
        }
    }
}