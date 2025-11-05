using VoxReader.Interfaces;

namespace VoxReader.Chunks
{
    internal class IndexMapChunk : Chunk, IIndexMapChunk
    {
        public int[] ColorIndices { get; }

        public IndexMapChunk(byte[] data) : base(data)
        {
            var formatParser = new FormatParser(Content);

            ColorIndices = new int[256];

            for (int i = 0; i < ColorIndices.Length; i++)
            {
                ColorIndices[i] = formatParser.ParseInt8();
            }
        }
    }
}