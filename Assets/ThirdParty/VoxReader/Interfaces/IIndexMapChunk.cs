namespace VoxReader.Interfaces
{
    internal interface IIndexMapChunk : IChunk
    {
        /// <summary>
        /// The color indices stored in the IMAP chunk.
        /// </summary>
        int[] ColorIndices { get; }
    }
}