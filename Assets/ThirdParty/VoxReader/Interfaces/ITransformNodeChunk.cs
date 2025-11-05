namespace VoxReader.Interfaces
{
    internal interface ITransformNodeChunk : INodeChunk
    {
        /// <summary>
        /// The name of the node.
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// True if the node is hidden.
        /// </summary>
        bool IsHidden { get; }
        
        /// <summary>
        /// The id of the child node.
        /// </summary>
        int ChildNodeId { get; }
        
        /// <summary>
        /// Must be '-1'.
        /// </summary>
        int ReservedId { get; }
        
        /// <summary>
        /// The id of the layer this node is on.
        /// </summary>
        int LayerId { get; }

        /// <summary>
        /// The number of frames of this node. Must be '1'.
        /// </summary>
        int FrameCount { get; }
        
        Frame[] Frames { get; }
    }
}