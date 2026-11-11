namespace ObjLoader.Loader.Data
{
    public struct Vec3
    {
        public Vec3(float x, float y, float z) : this()
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; private set; }
        public float Y { get; private set; }
        public float Z { get; private set; }
    }
}