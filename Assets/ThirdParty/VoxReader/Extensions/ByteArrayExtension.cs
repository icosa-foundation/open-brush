using System;

namespace VoxReader.Extensions
{
    internal static class ByteArrayExtension
    {
        public static byte[] GetRange(this byte[] data, int startIndex, int length)
        {
            var output = new byte[length];
            Buffer.BlockCopy(data, startIndex, output, 0, length);
            return output;
        }
        
        public static byte[] GetRange(this byte[] data, int startIndex)
        {
            int length = data.Length - startIndex;

            return data.GetRange(startIndex, length);
        }
    }
}