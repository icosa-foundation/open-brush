/* Reference:
https://github.com/ephtracy/voxel-model/blob/master/MagicaVoxel-file-format-vox.txt
https://github.com/ephtracy/voxel-model/blob/master/MagicaVoxel-file-format-vox-extension.txt
*/

using System;
using System.IO;
using System.Linq;
using VoxReader.Extensions;
using VoxReader.Interfaces;

namespace VoxReader
{
    /// <summary>
    /// Used to read data from .vox files.
    /// </summary>
    public static class VoxReader
    {
        /// <summary>
        /// Reads the file at the provided path.
        /// </summary>
        public static IVoxFile Read(string filePath)
        {
            byte[] data = File.ReadAllBytes(filePath);

            return Read(data);
        }

        /// <summary>
        /// Reads the data from the provided byte array.
        /// </summary>
        public static IVoxFile Read(byte[] data)
        {
            int versionNumber = BitConverter.ToInt32(data, 4);

            IChunk mainChunk = ChunkFactory.Parse(data.GetRange(8));

            var noteChunk = mainChunk.GetChild<INoteChunk>();

            var rawColors = mainChunk.GetChild<IPaletteChunk>().Colors;
            var indexMapChunk = mainChunk.GetChild<IIndexMapChunk>();
            var mappedColors = new Color[rawColors.Length - 1];

            for (int i = 0; i < mappedColors.Length; i++)
            {
                mappedColors[i] = rawColors[Helper.GetMappedColorIndex(indexMapChunk, i)];
            }

            var palette = new Palette(rawColors, mappedColors, noteChunk?.Notes ?? Array.Empty<string>());

            var models = Helper.ExtractModels(mainChunk, palette).ToArray();

            return new VoxFile(versionNumber, models, palette, mainChunk.Children);
        }
    }
}