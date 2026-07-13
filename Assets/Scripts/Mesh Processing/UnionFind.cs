namespace TiltBrush
{
    class UnionFind
    {
        int[] parent;

        public UnionFind(int size)
        {
            parent = new int[size];
            for (int i = 0; i < size; i++) parent[i] = i;
        }

        public int Find(int x)
        {
            if (parent[x] != x) parent[x] = Find(parent[x]);
            return parent[x];
        }

        public void Union(int x, int y)
        {
            int px = Find(x);
            int py = Find(y);
            if (px != py) parent[px] = py;
        }
    }
}