using VoxReader.Interfaces;

namespace VoxReader.Chunks
{
    internal class ShapeNodeChunk : NodeChunk, IShapeNodeChunk
    {
        public int ModelCount => Models.Length;
        public int[] Models { get; }

        public ShapeNodeChunk(byte[] data) : base(data)
        {
            int modelCount = FormatParser.ParseInt32();

            Models = new int[modelCount];
            
            for (int i = 0; i < modelCount; i++)
            {
                Models[i] = FormatParser.ParseInt32();

                var modelAttributes = FormatParser.ParseDictionary(); //TODO: parse attributes
            }
        }
    }
}