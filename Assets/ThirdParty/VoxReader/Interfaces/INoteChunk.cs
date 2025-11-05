namespace VoxReader.Interfaces
{
    internal interface INoteChunk : IChunk
    {
        /// <summary>
        /// The notes stored in the NOTE chunk.
        /// </summary>
        string[] Notes { get; }
    }
}