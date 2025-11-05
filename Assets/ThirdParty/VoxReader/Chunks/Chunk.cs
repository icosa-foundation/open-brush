using System;
using System.Linq;
using VoxReader.Interfaces;

namespace VoxReader.Chunks
{
    internal class Chunk : IChunk
    {
        public ChunkType Type { get; }
        public byte[] Content { get; }
        public IChunk[] Children { get; }

        public int TotalBytes { get; }

        /// <summary>
        /// Creates a new chunk using the given data.
        /// </summary>
        /// <param name="data">Data starting at the first byte of the chunk</param>
        public Chunk(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data), $"{nameof(data)} is null!");
            if (data.Length == 0)
                throw new ArgumentException($"{nameof(data)} is empty!");
            
            var formatParser = new FormatParser(data);

            Type = ChunkTypeMapping.GetChunkId(formatParser.ParseString(4));
            
            int contentLength = formatParser.ParseInt32();
            int childrenLength = formatParser.ParseInt32();
            
            Content = formatParser.ParseBytes(contentLength);
            Children = formatParser.ParseChunks(childrenLength);
            
            TotalBytes = formatParser.CurrentOffset;
        }

        public static ChunkType GetChunkId(byte[] chunkData)
        {
            return ChunkTypeMapping.GetChunkId(new string(Helper.GetCharArray(chunkData, 0, 4)));
        }

        public T GetChild<T>() where T : class, IChunk
        {
            return Children.FirstOrDefault(c => c is T) as T;
        }

        public IChunk[] GetChildren(ChunkType chunkType)
        {
            return Children.Where(chunk => chunk.Type == chunkType).ToArray();
        }
        
        public T[] GetChildren<T>() where T : class, IChunk
        {
            return Children.Where(c => c is T).Cast<T>().ToArray();
        }

        public override string ToString()
        {
            return $"Id: {Type}, Content Length: {Content.Length}, Children Length: {Children.Length}";
        }
    }
}