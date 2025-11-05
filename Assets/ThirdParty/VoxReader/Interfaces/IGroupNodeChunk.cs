namespace VoxReader.Interfaces
{
    internal interface IGroupNodeChunk : INodeChunk
    {
        /// <summary>
        /// The number of children nodes.
        /// </summary>
        int ChildrenCount { get; }
        
        /// <summary>
        /// The ids of the children.
        /// </summary>
        int[] ChildrenNodes { get; }
    }
}