namespace ObjLoader.Loader.Data.VertexData
{
    public struct Texture
    {
        public Texture(float x, float y) : this()
        {
            X = x;
            Y = y;
        }

        public float X { get; private set; }
        public float Y { get; private set; }
    }
}