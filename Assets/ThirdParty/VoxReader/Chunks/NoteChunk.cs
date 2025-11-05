using VoxReader.Interfaces;

namespace VoxReader.Chunks
{
    internal class NoteChunk : Chunk, INoteChunk
    {
        public string[] Notes { get; }

        public NoteChunk(byte[] data) : base(data)
        {
            var formatParser = new FormatParser(Content);

            int noteCount = formatParser.ParseInt32();

            Notes = new string[noteCount];

            for (int i = 0; i < noteCount; i++)
            {
                Notes[i] = formatParser.ParseStringAuto();
            }
        }
    }
}