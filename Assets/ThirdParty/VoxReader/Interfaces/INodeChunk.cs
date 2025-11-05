using System.Collections.Generic;

namespace VoxReader.Interfaces
{
    internal interface INodeChunk : IChunk
    {
        /// <summary>
        /// The id of the node.
        /// </summary>
        int NodeId { get; }
        
        /// <summary>
        /// The attributes assigned to the node.
        /// </summary>
        IDictionary<string, string> Attributes { get; }
    }
}