using VoxReader.Interfaces;

namespace VoxReader.Chunks
{
    internal class PaletteChunk : Chunk, IPaletteChunk
    {
        public Color[] Colors { get; }

        public PaletteChunk(byte[] data) : base(data)
        {
            var formatParser = new FormatParser(Content);

            Colors = formatParser.ParseColors(256);
        }
    }
}