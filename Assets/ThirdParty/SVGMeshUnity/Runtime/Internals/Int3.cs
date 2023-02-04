using System;

namespace SVGMeshUnity.Internals
{
    public struct Int3 : IComparable<Int3>
    {
        public Int3(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
        
        public int x;
        public int y;
        public int z;
        
        public int CompareTo(Int3 b)
        {
            var d = 0;
            
            d = x - b.x;
            if (d != 0) return d;
            
            d = y - b.y;
            if (d != 0) return d;

            return z - b.z;
        }
    }
}