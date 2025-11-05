namespace VoxReader
{
    internal readonly struct RawVoxel
    {
        /// <summary>
        /// The position of the voxel.
        /// </summary>
        public Vector3 Position { get; }

        /// <summary>
        /// The color index of the voxel.
        /// </summary>
        public int ColorIndex { get; }

        public RawVoxel(Vector3 position, int colorIndex)
        {
            Position = position;
            ColorIndex = colorIndex;
        }

        public override string ToString()
        {
            return $"Position: [{Position}], Color Index: [{ColorIndex}]";
        }
    }
}