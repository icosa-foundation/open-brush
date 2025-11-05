namespace VoxReader.Interfaces
{
    internal interface IShapeNodeChunk : INodeChunk
    {
        /// <summary>
        /// The number of models.
        /// </summary>
        int ModelCount { get; }
        
        /// <summary>
        /// The ids of the models.
        /// </summary>
        int[] Models { get; }
    }
}