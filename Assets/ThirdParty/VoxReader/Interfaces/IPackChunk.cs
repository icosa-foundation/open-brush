namespace VoxReader.Interfaces
{
    internal interface IPackChunk : IChunk
    {
        /// <summary>
        /// The number of models.
        /// </summary>
        int ModelCount { get; }
    }
}