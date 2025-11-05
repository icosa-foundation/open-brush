using VoxReader.Interfaces;

namespace VoxReader.Chunks
{
    internal class VoxelChunk : Chunk, IVoxelChunk
    {
        public RawVoxel[] Voxels { get; }

        public VoxelChunk(byte[] data) : base(data)
        {
            var formatParser = new FormatParser(Content);

            int voxelCount = formatParser.ParseInt32();

            Voxels = formatParser.ParseRawVoxels(voxelCount);
        }

        public override string ToString()
        {
            return $"{base.ToString()}, Voxel Count: {Voxels.Length}";
        }
    }
}